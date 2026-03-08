using MediatR;
using Microsoft.Extensions.Logging;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Telechargement;

/// <summary>
/// Gestionnaire MediatR pour la requête de téléchargement d'un fichier temporaire.
/// Vérifie l'expiration, déchiffre en streaming et met à jour le compteur d'accès.
/// </summary>
public sealed class GestionnaireTelechargementFichier : IRequestHandler<RequeteTelechargementFichier, ResultatTelechargement>
{
    private readonly IServiceChiffrement _serviceChiffrement;
    private readonly IServiceStockage _serviceStockage;
    private readonly IGestionnaireCles _gestionnaireCles;
    private readonly ILogger<GestionnaireTelechargementFichier> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="GestionnaireTelechargementFichier"/>.
    /// </summary>
    public GestionnaireTelechargementFichier(
        IServiceChiffrement serviceChiffrement,
        IServiceStockage serviceStockage,
        IGestionnaireCles gestionnaireCles,
        ILogger<GestionnaireTelechargementFichier> logger)
    {
        _serviceChiffrement = serviceChiffrement;
        _serviceStockage = serviceStockage;
        _gestionnaireCles = gestionnaireCles;
        _logger = logger;
    }

    /// <summary>
    /// Traite la requête de téléchargement : vérifie l'expiration, déchiffre et met à jour les métadonnées.
    /// </summary>
    /// <param name="requete">La requête contenant l'identifiant du fichier.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns>Le résultat contenant le flux déchiffré et les informations du fichier.</returns>
    /// <exception cref="FichierIntrouvableException">Levée si le fichier n'existe pas.</exception>
    /// <exception cref="FichierExpireException">Levée si le fichier est expiré.</exception>
    public async Task<ResultatTelechargement> Handle(RequeteTelechargementFichier requete, CancellationToken ct)
    {
        _logger.LogDebug("Demande de téléchargement — identifiant={Identifiant}", requete.Identifiant);

        // Lecture des métadonnées (lève FichierIntrouvableException si absent)
        var metadonnees = await _serviceStockage.LireMetadonneesAsync(requete.Identifiant, ct);
        _logger.LogDebug("Métadonnées chargées — identifiant={Identifiant}, accès={AccesCourant}/{AccesMax}, versionCle={VersionCle}",
            requete.Identifiant, metadonnees.NombreAccesCourant, metadonnees.NombreAccesMax?.ToString() ?? "∞", metadonnees.VersionCle);

        // Vérification de l'expiration avant tout déchiffrement
        if (metadonnees.EstExpire(DateTimeOffset.UtcNow))
        {
            _logger.LogWarning("Tentative de téléchargement d'un fichier expiré — identifiant={Identifiant}", requete.Identifiant);
            throw new FichierExpireException(requete.Identifiant);
        }

        // Incrément du compteur d'accès et persistance immédiate
        var metadonneesMAJ = metadonnees.AvecAccesIncremente();
        await _serviceStockage.SauvegarderMetadonneesAsync(metadonneesMAJ, ct);
        _logger.LogDebug("Compteur d'accès incrémenté — identifiant={Identifiant}, nouvel accès={AccesCourant}",
            requete.Identifiant, metadonneesMAJ.NombreAccesCourant);

        // Résolution de la clé maître correspondant à la version du fichier
        var cleMaitre = _gestionnaireCles.ObtenirCle(metadonnees.VersionCle);

        // Déchiffrement en streaming
        _logger.LogDebug("Début du déchiffrement AES-256-GCM pour {Identifiant}", requete.Identifiant);
        using var fluxChiffre = _serviceStockage.OuvrirFichier(requete.Identifiant);
        var fluxDechiffre = new MemoryStream();
        await _serviceChiffrement.DechiffrerAsync(fluxChiffre, fluxDechiffre, metadonnees.Sel, cleMaitre, ct);
        fluxDechiffre.Position = 0;

        _logger.LogDebug("Déchiffrement terminé pour {Identifiant} — taille={Taille}", requete.Identifiant, fluxDechiffre.Length);
        _logger.LogInformation("Téléchargement en cours — identifiant={Identifiant}, fichier={NomFichier}",
            requete.Identifiant, metadonnees.NomOriginal);

        return new ResultatTelechargement(
            fluxDechiffre,
            metadonnees.NomOriginal,
            metadonnees.TypeMime,
            metadonnees.TailleOctets
        );
    }
}
