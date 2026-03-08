using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private readonly string _cheminFichier;
    private readonly ILogger<GestionnaireCles> _logger;
    private readonly ReaderWriterLockSlim _verrou = new();
    private TrousseauJson _trousseau = new();

    /// <summary>
    /// Initialise le gestionnaire : charge le trousseau existant ou crée une première clé aléatoire.
    /// </summary>
    public GestionnaireCles(IOptions<ConfigurationCles> config, ILogger<GestionnaireCles> logger)
    {
        _cheminFichier = config.Value.CheminFichierTrousseau;
        _logger = logger;
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
    /// Charge le trousseau depuis le fichier JSON, ou génère une première clé si le fichier n'existe pas.
    /// </summary>
    private void ChargerOuInitialiser()
    {
        var repertoire = Path.GetDirectoryName(Path.GetFullPath(_cheminFichier));
        if (!string.IsNullOrEmpty(repertoire) && !Directory.Exists(repertoire))
            Directory.CreateDirectory(repertoire);

        if (File.Exists(_cheminFichier))
        {
            var json = File.ReadAllText(_cheminFichier);
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

            File.WriteAllText(_cheminFichier, JsonSerializer.Serialize(_trousseau, OptionsJson));
            _logger.LogInformation("Trousseau initialisé — première clé générée, version={Version}", premiereVersion);
        }
    }

    private async Task PersisterAsync(CancellationToken ct)
    {
        await using var flux = new FileStream(_cheminFichier, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(flux, _trousseau, OptionsJson, ct);
    }

    /// <inheritdoc/>
    public void Dispose() => _verrou.Dispose();
}
