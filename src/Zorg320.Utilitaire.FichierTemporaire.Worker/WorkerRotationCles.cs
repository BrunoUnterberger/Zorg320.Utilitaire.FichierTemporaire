using Microsoft.Extensions.Logging;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;

namespace Zorg320.Utilitaire.FichierTemporaire.Worker;

/// <summary>
/// Worker de rotation périodique des clés maîtres (toutes les 24 heures).
/// Après chaque rotation, supprime du trousseau les versions de clés orphelines
/// (non référencées par aucun fichier actif).
/// Résistant aux pannes : les erreurs sont absorbées et loggées.
/// </summary>
public sealed class WorkerRotationCles : BackgroundService
{
    private static readonly TimeSpan IntervalleRotation = TimeSpan.FromHours(24);

    private readonly IGestionnaireCles _gestionnaireCles;
    private readonly IServiceStockage _serviceStockage;
    private readonly ILogger<WorkerRotationCles> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="WorkerRotationCles"/>.
    /// </summary>
    public WorkerRotationCles(
        IGestionnaireCles gestionnaireCles,
        IServiceStockage serviceStockage,
        ILogger<WorkerRotationCles> logger)
    {
        _gestionnaireCles = gestionnaireCles;
        _serviceStockage = serviceStockage;
        _logger = logger;
    }

    /// <summary>
    /// Boucle principale : attend 24 heures, effectue la rotation, puis purge les clés orphelines.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de rotation des clés démarré — intervalle=24h");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(IntervalleRotation, stoppingToken);
                await RoterEtPurgerAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la rotation des clés — prochaine tentative dans 24h");
            }
        }

        _logger.LogInformation("Worker de rotation des clés arrêté");
    }

    /// <summary>
    /// Effectue la rotation puis supprime les versions de clés plus utilisées par aucun fichier.
    /// </summary>
    private async Task RoterEtPurgerAsync(CancellationToken ct)
    {
        await _gestionnaireCles.EffectuerRotationAsync(ct);

        // Collecte des versions encore référencées par des fichiers existants
        var versionsUtilisees = new HashSet<string>();
        await foreach (var meta in _serviceStockage.EnumererTousAsync(ct))
        {
            if (!string.IsNullOrEmpty(meta.VersionCle))
                versionsUtilisees.Add(meta.VersionCle);
        }

        _logger.LogDebug("Versions de clés encore utilisées : {NombreVersions}", versionsUtilisees.Count);
        await _gestionnaireCles.SupprimerVersionsInutilisees(versionsUtilisees, ct);
    }
}
