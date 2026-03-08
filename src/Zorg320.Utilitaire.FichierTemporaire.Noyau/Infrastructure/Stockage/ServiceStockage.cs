using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Stockage;

/// <summary>
/// Implémentation du service de stockage sur le système de fichiers local.
/// Chaque fichier temporaire est composé de deux fichiers :
/// - [identifiant].enc : le contenu chiffré
/// - [identifiant].meta : les métadonnées au format JSON
/// </summary>
public sealed class ServiceStockage : IServiceStockage
{
    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        WriteIndented = true
    };

    private readonly ConfigurationStockage _config;
    private readonly ILogger<ServiceStockage> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="ServiceStockage"/>.
    /// Crée le répertoire racine s'il n'existe pas.
    /// </summary>
    public ServiceStockage(IOptions<ConfigurationStockage> config, ILogger<ServiceStockage> logger)
    {
        _config = config.Value;
        _logger = logger;

        if (!Directory.Exists(_config.CheminRacine))
        {
            Directory.CreateDirectory(_config.CheminRacine);
            _logger.LogInformation("Répertoire de stockage créé : {Chemin}", _config.CheminRacine);
        }
    }

    /// <inheritdoc/>
    public async Task SauvegarderFichierAsync(string identifiant, Stream contenuChiffre, CancellationToken ct = default)
    {
        var chemin = CheminFichier(identifiant);
        _logger.LogDebug("Sauvegarde du fichier chiffré → {Chemin}", chemin);

        await using var fichier = new FileStream(chemin, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        await contenuChiffre.CopyToAsync(fichier, ct);

        _logger.LogDebug("Fichier chiffré sauvegardé : {Chemin}", chemin);
    }

    /// <inheritdoc/>
    public async Task SauvegarderMetadonneesAsync(MetadonneesFichier metadonnees, CancellationToken ct = default)
    {
        var chemin = CheminMetadonnees(metadonnees.Identifiant);
        _logger.LogDebug("Sauvegarde des métadonnées → {Chemin}", chemin);

        await using var fichier = new FileStream(chemin, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(fichier, metadonnees, OptionsJson, ct);

        _logger.LogDebug("Métadonnées sauvegardées : {Chemin}", chemin);
    }

    /// <inheritdoc/>
    public Stream OuvrirFichier(string identifiant)
    {
        var chemin = CheminFichier(identifiant);

        if (!File.Exists(chemin))
        {
            _logger.LogWarning("Fichier chiffré introuvable : {Chemin}", chemin);
            throw new FichierIntrouvableException(identifiant);
        }

        _logger.LogDebug("Ouverture en lecture du fichier chiffré : {Chemin}", chemin);
        return new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
    }

    /// <inheritdoc/>
    public async Task<MetadonneesFichier> LireMetadonneesAsync(string identifiant, CancellationToken ct = default)
    {
        var chemin = CheminMetadonnees(identifiant);

        if (!File.Exists(chemin))
        {
            _logger.LogWarning("Fichier de métadonnées introuvable : {Chemin}", chemin);
            throw new FichierIntrouvableException(identifiant);
        }

        _logger.LogDebug("Lecture des métadonnées : {Chemin}", chemin);

        await using var fichier = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        var metadonnees = await JsonSerializer.DeserializeAsync<MetadonneesFichier>(fichier, OptionsJson, ct);

        if (metadonnees is null)
            throw new InvalidOperationException($"Impossible de désérialiser les métadonnées du fichier '{identifiant}'.");

        return metadonnees;
    }

    /// <inheritdoc/>
    public async Task SupprimerAsync(string identifiant, CancellationToken ct = default)
    {
        var cheminFichier = CheminFichier(identifiant);
        var cheminMeta = CheminMetadonnees(identifiant);

        if (File.Exists(cheminFichier))
        {
            File.Delete(cheminFichier);
            _logger.LogDebug("Fichier chiffré supprimé : {Chemin}", cheminFichier);
        }

        if (File.Exists(cheminMeta))
        {
            File.Delete(cheminMeta);
            _logger.LogDebug("Fichier de métadonnées supprimé : {Chemin}", cheminMeta);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MetadonneesFichier> EnumererTousAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var fichiersMeta = Directory.EnumerateFiles(_config.CheminRacine, $"*{_config.ExtensionMetadonnees}");

        foreach (var chemin in fichiersMeta)
        {
            ct.ThrowIfCancellationRequested();

            MetadonneesFichier? metadonnees = null;
            try
            {
                await using var fichier = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 4096, useAsync: true);
                metadonnees = await JsonSerializer.DeserializeAsync<MetadonneesFichier>(fichier, OptionsJson, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de lire les métadonnées du fichier : {Chemin}", chemin);
            }

            if (metadonnees is not null)
                yield return metadonnees;
        }
    }

    /// <summary>Construit le chemin complet du fichier chiffré pour un identifiant donné.</summary>
    private string CheminFichier(string identifiant)
        => Path.Combine(_config.CheminRacine, $"{identifiant}{_config.ExtensionFichier}");

    /// <summary>Construit le chemin complet du fichier de métadonnées pour un identifiant donné.</summary>
    private string CheminMetadonnees(string identifiant)
        => Path.Combine(_config.CheminRacine, $"{identifiant}{_config.ExtensionMetadonnees}");
}
