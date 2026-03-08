using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Chiffrement;

/// <summary>
/// Implémentation du service de chiffrement AES-256-GCM avec streaming par chunks.
/// La clé de chiffrement est dérivée par fichier via HKDF (SHA-256) à partir de la clé maître
/// fournie par le gestionnaire de clés et d'un sel aléatoire propre à chaque fichier.
///
/// Format du flux chiffré :
/// [4 octets LE : nombre de chunks]
/// [Pour chaque chunk :]
///   [12 octets : nonce AES-GCM aléatoire]
///   [16 octets : tag d'authentification AES-GCM]
///   [4 octets LE : longueur des données chiffrées]
///   [N octets : données chiffrées]
/// </summary>
public sealed class ServiceChiffrement : IServiceChiffrement
{
    private const int TailleNonce = 12;
    private const int TailleTag = 16;
    private const string InfoHkdf = "fichier-temporaire";
    private const int TailleCle = 32;
    private const int TailleSel = 32;

    private readonly int _tailleChunk;
    private readonly ILogger<ServiceChiffrement> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="ServiceChiffrement"/>.
    /// </summary>
    /// <param name="config">Configuration contenant la taille de chunk.</param>
    /// <param name="logger">Logger générique injecté.</param>
    public ServiceChiffrement(IOptions<ConfigurationChiffrement> config, ILogger<ServiceChiffrement> logger)
    {
        _tailleChunk = config.Value.TailleChunkOctets;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ChiffrerAsync(Stream source, Stream destination, string selBase64, byte[] cleMaitre, CancellationToken ct = default)
    {
        var cle = DeriverCle(selBase64, cleMaitre);
        var tampon = new byte[_tailleChunk];
        var chunks = new List<(byte[] Nonce, byte[] Tag, byte[] Donnees)>();

        _logger.LogDebug("Lecture et chiffrement des chunks (taille chunk={TailleChunk})", _tailleChunk);

        int octetsLus;
        while ((octetsLus = await LireChunkCompletAsync(source, tampon, ct)) > 0)
        {
            var donneesClair = tampon.AsSpan(0, octetsLus).ToArray();
            var nonce = GenererNonce();
            var donneesChiffrees = new byte[octetsLus];
            var tag = new byte[TailleTag];

            using var aesGcm = new AesGcm(cle, TailleTag);
            aesGcm.Encrypt(nonce, donneesClair, donneesChiffrees, tag);

            chunks.Add((nonce, tag, donneesChiffrees));
        }

        _logger.LogDebug("Chiffrement terminé — {NombreChunks} chunk(s) produit(s)", chunks.Count);

        var entete = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(entete, chunks.Count);
        await destination.WriteAsync(entete, ct);

        foreach (var (nonce, tag, donnees) in chunks)
        {
            await destination.WriteAsync(nonce, ct);
            await destination.WriteAsync(tag, ct);

            var longueur = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(longueur, donnees.Length);
            await destination.WriteAsync(longueur, ct);

            await destination.WriteAsync(donnees, ct);
        }

        await destination.FlushAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DechiffrerAsync(Stream source, Stream destination, string selBase64, byte[] cleMaitre, CancellationToken ct = default)
    {
        var cle = DeriverCle(selBase64, cleMaitre);

        var entete = new byte[sizeof(int)];
        await LireExactAsync(source, entete, ct);
        var nombreChunks = BinaryPrimitives.ReadInt32LittleEndian(entete);

        _logger.LogDebug("Déchiffrement de {NombreChunks} chunk(s)", nombreChunks);

        using var aesGcm = new AesGcm(cle, TailleTag);

        for (var i = 0; i < nombreChunks; i++)
        {
            var nonce = new byte[TailleNonce];
            await LireExactAsync(source, nonce, ct);

            var tag = new byte[TailleTag];
            await LireExactAsync(source, tag, ct);

            var longueurBuf = new byte[sizeof(int)];
            await LireExactAsync(source, longueurBuf, ct);
            var longueur = BinaryPrimitives.ReadInt32LittleEndian(longueurBuf);

            var donneesChiffrees = new byte[longueur];
            await LireExactAsync(source, donneesChiffrees, ct);

            var donneesClair = new byte[longueur];
            aesGcm.Decrypt(nonce, donneesChiffrees, tag, donneesClair);

            await destination.WriteAsync(donneesClair, ct);
        }

        await destination.FlushAsync(ct);
        _logger.LogDebug("Déchiffrement de tous les chunks terminé");
    }

    /// <inheritdoc/>
    public string GenererSel()
    {
        var sel = new byte[TailleSel];
        RandomNumberGenerator.Fill(sel);
        return Convert.ToBase64String(sel);
    }

    /// <summary>
    /// Dérive une clé AES de 256 bits à partir de la clé maître et du sel HKDF.
    /// </summary>
    private static byte[] DeriverCle(string selBase64, byte[] cleMaitre)
    {
        var sel = Convert.FromBase64String(selBase64);
        var info = System.Text.Encoding.UTF8.GetBytes(InfoHkdf);
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, cleMaitre, TailleCle, sel, info);
    }

    private static byte[] GenererNonce()
    {
        var nonce = new byte[TailleNonce];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

    private static async Task<int> LireChunkCompletAsync(Stream source, byte[] tampon, CancellationToken ct)
    {
        var totalLu = 0;
        while (totalLu < tampon.Length)
        {
            var lu = await source.ReadAsync(tampon.AsMemory(totalLu, tampon.Length - totalLu), ct);
            if (lu == 0) break;
            totalLu += lu;
        }
        return totalLu;
    }

    private static async Task LireExactAsync(Stream source, byte[] tampon, CancellationToken ct)
    {
        var totalLu = 0;
        while (totalLu < tampon.Length)
        {
            var lu = await source.ReadAsync(tampon.AsMemory(totalLu, tampon.Length - totalLu), ct);
            if (lu == 0)
                throw new EndOfStreamException($"Fin de flux inattendue : attendu {tampon.Length} octets, lu {totalLu}.");
            totalLu += lu;
        }
    }
}
