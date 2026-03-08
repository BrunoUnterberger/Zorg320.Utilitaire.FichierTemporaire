using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

namespace Zorg320.Utilitaire.FichierTemporaire.Worker;

/// <summary>
/// Worker de nettoyage périodique des fichiers temporaires expirés.
/// Résistant aux pannes : toute erreur est absorbée avec un backoff exponentiel.
/// Conçu pour tourner en instance unique dans un conteneur.
/// </summary>
public sealed class WorkerNettoyage : BackgroundService
{
    private static readonly TimeSpan[] PaliersBackoff =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

    private readonly IServiceStockage _serviceStockage;
    private readonly ConfigurationNettoyage _configNettoyage;
    private readonly ILogger<WorkerNettoyage> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="WorkerNettoyage"/>.
    /// </summary>
    public WorkerNettoyage(
        IServiceStockage serviceStockage,
        IOptions<ConfigurationNettoyage> configNettoyage,
        ILogger<WorkerNettoyage> logger)
    {
        _serviceStockage = serviceStockage;
        _configNettoyage = configNettoyage.Value;
        _logger = logger;
    }

    /// <summary>
    /// Boucle principale : nettoyage immédiat au démarrage, puis périodique.
    /// Ne propage jamais d'exception — backoff exponentiel en cas d'erreur répétée.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de nettoyage démarré — intervalle={IntervalleMinutes} minute(s)",
            _configNettoyage.IntervalleMinutes);

        var indexPalier = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NettoyerFichiersExpiresAsync(stoppingToken);
                indexPalier = 0;
                await Task.Delay(TimeSpan.FromMinutes(_configNettoyage.IntervalleMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                var delai = PaliersBackoff[Math.Min(indexPalier, PaliersBackoff.Length - 1)];
                indexPalier++;
                _logger.LogError(ex, "Erreur lors du nettoyage — nouvelle tentative dans {DelaiSecondes}s",
                    (int)delai.TotalSeconds);

                try { await Task.Delay(delai, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("Worker de nettoyage arrêté");
    }

    /// <summary>
    /// Parcourt tous les fichiers et supprime ceux qui sont expirés.
    /// Les erreurs de suppression individuelles sont loggées sans interrompre le passage.
    /// </summary>
    private async Task NettoyerFichiersExpiresAsync(CancellationToken ct)
    {
        _logger.LogDebug("Début du passage de nettoyage");

        var maintenant = DateTimeOffset.UtcNow;
        var nombreSupprimes = 0;
        var nombreErreurs = 0;

        await foreach (var metadonnees in _serviceStockage.EnumererTousAsync(ct))
        {
            if (!metadonnees.EstExpire(maintenant))
                continue;

            try
            {
                _logger.LogDebug("Suppression du fichier expiré — identifiant={Identifiant}, fichier={NomFichier}",
                    metadonnees.Identifiant, metadonnees.NomOriginal);

                await _serviceStockage.SupprimerAsync(metadonnees.Identifiant, ct);
                nombreSupprimes++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de supprimer le fichier expiré — identifiant={Identifiant}",
                    metadonnees.Identifiant);
                nombreErreurs++;
            }
        }

        if (nombreSupprimes > 0 || nombreErreurs > 0)
            _logger.LogInformation("Nettoyage terminé — {NombreSupprimes} fichier(s) supprimé(s), {NombreErreurs} erreur(s)",
                nombreSupprimes, nombreErreurs);
        else
            _logger.LogDebug("Nettoyage terminé — aucun fichier expiré trouvé");
    }
}
