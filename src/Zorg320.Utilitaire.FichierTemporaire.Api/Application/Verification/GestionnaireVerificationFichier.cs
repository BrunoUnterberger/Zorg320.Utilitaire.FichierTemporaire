using MediatR;
using Microsoft.Extensions.Logging;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Verification;

/// <summary>
/// Gestionnaire MediatR pour la vérification de disponibilité d'un fichier temporaire.
/// Ne modifie pas le compteur d'accès.
/// </summary>
public sealed class GestionnaireVerificationFichier : IRequestHandler<RequeteVerificationFichier, bool>
{
    private readonly IServiceStockage _serviceStockage;
    private readonly ILogger<GestionnaireVerificationFichier> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="GestionnaireVerificationFichier"/>.
    /// </summary>
    public GestionnaireVerificationFichier(
        IServiceStockage serviceStockage,
        ILogger<GestionnaireVerificationFichier> logger)
    {
        _serviceStockage = serviceStockage;
        _logger = logger;
    }

    /// <summary>
    /// Vérifie si le fichier existe et n'est pas expiré, sans modifier le compteur d'accès.
    /// </summary>
    /// <param name="requete">La requête contenant l'identifiant du fichier.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns><c>true</c> si le fichier existe et est disponible ; <c>false</c> sinon.</returns>
    public async Task<bool> Handle(RequeteVerificationFichier requete, CancellationToken ct)
    {
        _logger.LogDebug("Vérification de disponibilité — identifiant={Identifiant}", requete.Identifiant);

        try
        {
            var metadonnees = await _serviceStockage.LireMetadonneesAsync(requete.Identifiant, ct);

            if (metadonnees.EstExpire(DateTimeOffset.UtcNow))
            {
                _logger.LogDebug("Fichier expiré — identifiant={Identifiant}", requete.Identifiant);
                return false;
            }

            _logger.LogDebug("Fichier disponible — identifiant={Identifiant}", requete.Identifiant);
            return true;
        }
        catch (FichierIntrouvableException)
        {
            _logger.LogDebug("Fichier introuvable — identifiant={Identifiant}", requete.Identifiant);
            return false;
        }
    }
}
