using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Cles;

/// <summary>
/// Structure interne sérialisée en JSON pour persister le trousseau de clés.
/// </summary>
internal sealed class TrousseauJson
{
    [JsonPropertyName("versionCourante")]
    public string VersionCourante { get; set; } = string.Empty;

    /// <summary>Dictionnaire version (GUID) → clé maître encodée en Base64.</summary>
    [JsonPropertyName("cles")]
    public Dictionary<string, string> Cles { get; set; } = new();
}

/// <summary>
/// Gestionnaire de clés maîtres rotatif, persisté dans un fichier JSON.
/// Thread-safe : lectures concurrentes autorisées, écriture exclusive lors de la rotation.
/// </summary>
public sealed class GestionnaireCles : IGestionnaireCles, IDisposable
{
    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };
    private const int TailleCle = 32;
    private const string ObjectifProtection = "GestionnaireCles.Trousseau.v1";

    private readonly string _cheminFichier;
    private readonly ILogger<GestionnaireCles> _logger;
    private readonly IDataProtector _protecteur;
    private readonly ReaderWriterLockSlim _verrou = new();
    private TrousseauJson _trousseau = new();

    /// <summary>
    /// Initialise le gestionnaire : charge le trousseau existant ou crée une première clé aléatoire.
    /// </summary>
    public GestionnaireCles(
        IOptions<ConfigurationCles> config,
        ILogger<GestionnaireCles> logger,
        IDataProtectionProvider protectionProvider)
    {
        _cheminFichier = config.Value.CheminFichierTrousseau;
        _logger = logger;
        _protecteur = protectionProvider.CreateProtector(ObjectifProtection);
        ChargerOuInitialiser();
    }

    /// <inheritdoc/>
    public (string Version, byte[] CleMaitre) ObtenirCleCourante()
    {
        _verrou.EnterReadLock();
        try
        {
            var version = _trousseau.VersionCourante;
            var cle = Convert.FromBase64String(_trousseau.Cles[version]);
            return (version, cle);
        }
        finally { _verrou.ExitReadLock(); }
    }

    /// <inheritdoc/>
    public byte[] ObtenirCle(string version)
    {
        _verrou.EnterReadLock();
        try
        {
            if (!_trousseau.Cles.TryGetValue(version, out var cleBase64))
                throw new InvalidOperationException($"Version de clé inconnue : '{version}'.");

            return Convert.FromBase64String(cleBase64);
        }
        finally { _verrou.ExitReadLock(); }
    }

    /// <inheritdoc/>
    public async Task EffectuerRotationAsync(CancellationToken ct = default)
    {
        var nouvelleVersion = Guid.NewGuid().ToString("N");
        var nouvelleCle = new byte[TailleCle];
        RandomNumberGenerator.Fill(nouvelleCle);

        _verrou.EnterWriteLock();
        try
        {
            _trousseau.Cles[nouvelleVersion] = Convert.ToBase64String(nouvelleCle);
            _trousseau.VersionCourante = nouvelleVersion;
            await PersisterAsync(ct);
        }
        finally { _verrou.ExitWriteLock(); }

        _logger.LogInformation("Rotation de clé effectuée — nouvelle version={Version}", nouvelleVersion);
    }

    /// <inheritdoc/>
    public async Task SupprimerVersionsInutilisees(IReadOnlySet<string> versionsUtilisees, CancellationToken ct = default)
    {
        _verrou.EnterWriteLock();
        try
        {
            var obsoletes = _trousseau.Cles.Keys
                .Where(v => v != _trousseau.VersionCourante && !versionsUtilisees.Contains(v))
                .ToList();

            if (obsoletes.Count == 0) return;

            foreach (var v in obsoletes)
            {
                _trousseau.Cles.Remove(v);
                _logger.LogInformation("Clé obsolète supprimée du trousseau — version={Version}", v);
            }

            await PersisterAsync(ct);
        }
        finally { _verrou.ExitWriteLock(); }
    }

    /// <summary>
    /// Charge le trousseau depuis le fichier protégé, ou génère une première clé si le fichier n'existe pas.
    /// Si un ancien fichier en clair (JSON) est détecté, il est migré automatiquement vers le format protégé.
    /// </summary>
    private void ChargerOuInitialiser()
    {
        var repertoire = Path.GetDirectoryName(Path.GetFullPath(_cheminFichier));
        if (!string.IsNullOrEmpty(repertoire) && !Directory.Exists(repertoire))
            Directory.CreateDirectory(repertoire);

        if (File.Exists(_cheminFichier))
        {
            string json;
            try
            {
                var donnees = File.ReadAllBytes(_cheminFichier);
                json = Encoding.UTF8.GetString(_protecteur.Unprotect(donnees));
            }
            catch (CryptographicException)
            {
                // Migration depuis l'ancien format JSON en clair vers le format protégé
                _logger.LogWarning("Trousseau en clair détecté — migration vers le format protégé en cours.");
                json = File.ReadAllText(_cheminFichier);
                File.WriteAllBytes(_cheminFichier, _protecteur.Protect(Encoding.UTF8.GetBytes(json)));
                _logger.LogInformation("Migration du trousseau terminée.");
            }

            _trousseau = JsonSerializer.Deserialize<TrousseauJson>(json, OptionsJson)
                ?? throw new InvalidOperationException("Le fichier de trousseau de clés est corrompu.");

            _logger.LogInformation("Trousseau chargé — {NombreCles} version(s), version courante={Version}",
                _trousseau.Cles.Count, _trousseau.VersionCourante);
        }
        else
        {
            var premiereVersion = Guid.NewGuid().ToString("N");
            var premiereCle = new byte[TailleCle];
            RandomNumberGenerator.Fill(premiereCle);

            _trousseau = new TrousseauJson
            {
                VersionCourante = premiereVersion,
                Cles = new Dictionary<string, string>
                {
                    [premiereVersion] = Convert.ToBase64String(premiereCle)
                }
            };

            var json = JsonSerializer.Serialize(_trousseau, OptionsJson);
            File.WriteAllBytes(_cheminFichier, _protecteur.Protect(Encoding.UTF8.GetBytes(json)));
            _logger.LogInformation("Trousseau initialisé — première clé générée, version={Version}", premiereVersion);
        }
    }

    private async Task PersisterAsync(CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(_trousseau, OptionsJson);
        var donnees = _protecteur.Protect(Encoding.UTF8.GetBytes(json));
        await File.WriteAllBytesAsync(_cheminFichier, donnees, ct);
    }

    /// <inheritdoc/>
    public void Dispose() => _verrou.Dispose();
}
