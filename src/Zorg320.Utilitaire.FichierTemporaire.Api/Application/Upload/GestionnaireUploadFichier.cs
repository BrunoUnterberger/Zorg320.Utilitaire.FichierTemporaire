using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Upload;

/// <summary>
/// Gestionnaire MediatR pour la commande d'upload d'un fichier temporaire.
/// Orchestre la génération d'identifiant, le chiffrement et la persistance.
/// </summary>
public sealed class GestionnaireUploadFichier : IRequestHandler<CommandeUploadFichier, ResultatUpload>
{
    private readonly IServiceChiffrement _serviceChiffrement;
    private readonly IServiceStockage _serviceStockage;
    private readonly IGestionnaireCles _gestionnaireCles;
    private readonly ConfigurationStockage _configStockage;
    private readonly ILogger<GestionnaireUploadFichier> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="GestionnaireUploadFichier"/>.
    /// </summary>
    public GestionnaireUploadFichier(
        IServiceChiffrement serviceChiffrement,
        IServiceStockage serviceStockage,
        IGestionnaireCles gestionnaireCles,
        IOptions<ConfigurationStockage> configStockage,
        ILogger<GestionnaireUploadFichier> logger)
    {
        _serviceChiffrement = serviceChiffrement;
        _serviceStockage = serviceStockage;
        _gestionnaireCles = gestionnaireCles;
        _configStockage = configStockage.Value;
        _logger = logger;
    }

    /// <summary>
    /// Traite la commande d'upload : génère l'identifiant, chiffre le fichier et persiste les métadonnées.
    /// </summary>
    /// <param name="commande">La commande d'upload contenant le flux et les paramètres.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns>Le résultat contenant l'identifiant et l'URL de téléchargement.</returns>
    public async Task<ResultatUpload> Handle(CommandeUploadFichier commande, CancellationToken ct)
    {
        var identifiant = Guid.NewGuid().ToString("N");
        _logger.LogDebug("Upload démarré — identifiant={Identifiant}, fichier={NomFichier}, taille={Taille}",
            identifiant, commande.NomFichier, commande.TailleOctets);

        // Récupération de la version de clé courante
        var (versionCle, cleMaitre) = _gestionnaireCles.ObtenirCleCourante();
        _logger.LogDebug("Clé maître sélectionnée — version={VersionCle}", versionCle);

        // Génération du sel HKDF propre à ce fichier
        var sel = _serviceChiffrement.GenererSel();
        _logger.LogDebug("Sel HKDF généré pour le fichier {Identifiant}", identifiant);

        // Chiffrement et écriture en streaming
        _logger.LogDebug("Début du chiffrement AES-256-GCM pour {Identifiant}", identifiant);
        using var fluxChiffre = new MemoryStream();
        await _serviceChiffrement.ChiffrerAsync(commande.Contenu, fluxChiffre, sel, cleMaitre, ct);
        fluxChiffre.Position = 0;

        _logger.LogDebug("Chiffrement terminé pour {Identifiant} — taille chiffrée={TailleChiffree}",
            identifiant, fluxChiffre.Length);

        // Persistance du fichier chiffré
        _logger.LogDebug("Écriture du fichier chiffré sur le disque pour {Identifiant}", identifiant);
        await _serviceStockage.SauvegarderFichierAsync(identifiant, fluxChiffre, ct);

        // Calcul de la date d'expiration
        DateTimeOffset? dateExpiration = commande.DureeVieMinutes.HasValue
            ? DateTimeOffset.UtcNow.AddMinutes(commande.DureeVieMinutes.Value)
            : null;

        // Persistance des métadonnées
        var metadonnees = new MetadonneesFichier
        {
            Identifiant = identifiant,
            NomOriginal = commande.NomFichier,
            TypeMime = commande.TypeMime,
            DateCreation = DateTimeOffset.UtcNow,
            DateExpiration = dateExpiration,
            NombreAccesMax = commande.NombreAccesMax,
            NombreAccesCourant = 0,
            TailleOctets = commande.TailleOctets,
            Sel = sel,
            VersionCle = versionCle
        };

        _logger.LogDebug("Écriture des métadonnées pour {Identifiant}", identifiant);
        await _serviceStockage.SauvegarderMetadonneesAsync(metadonnees, ct);

        var urlTelechargement = $"/v1/fichiers/{identifiant}";
        _logger.LogInformation("Upload réussi — identifiant={Identifiant}, expiration={DateExpiration}, accesMax={NombreAccesMax}, versionCle={VersionCle}",
            identifiant, dateExpiration?.ToString("O") ?? "aucune", commande.NombreAccesMax?.ToString() ?? "illimité", versionCle);

        return new ResultatUpload(identifiant, urlTelechargement);
    }
}
