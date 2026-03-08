using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Upload;
using Zorg320.Utilitaire.FichierTemporaire.Api.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.Commun;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.V1;

/// <summary>
/// Modèle de la requête d'upload d'un fichier temporaire (multipart/form-data).
/// </summary>
public sealed class RequeteUpload
{
    /// <summary>Fichier à uploader (obligatoire).</summary>
    public IFormFile? Fichier { get; set; }

    /// <summary>Durée de vie en minutes à partir du moment de l'upload (optionnel).</summary>
    public int? DureeVieMinutes { get; set; }

    /// <summary>Nombre maximal d'accès en téléchargement (optionnel).</summary>
    public int? NombreAccesMax { get; set; }
}

/// <summary>
/// Données retournées dans la réponse suite à un upload réussi.
/// </summary>
public sealed record DonneesUpload(
    /// <summary>Identifiant unique attribué au fichier uploadé.</summary>
    string Identifiant
);

/// <summary>
/// Endpoint FastEndpoints pour l'upload d'un fichier temporaire.
/// Route : POST /v1/fichiers
/// Accepte un fichier multipart/form-data avec des paramètres de durée de vie optionnels.
/// </summary>
public sealed class UploadFichierEndpoint : Endpoint<RequeteUpload, ReponseBase<DonneesUpload>>
{
    private readonly IMediator _mediator;
    private readonly ILogger<UploadFichierEndpoint> _logger;
    private readonly ConfigurationAuthentification _configAuth;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="UploadFichierEndpoint"/>.
    /// </summary>
    public UploadFichierEndpoint(
        IMediator mediator,
        ILogger<UploadFichierEndpoint> logger,
        IOptions<ConfigurationAuthentification> configAuth)
    {
        _mediator = mediator;
        _logger = logger;
        _configAuth = configAuth.Value;
    }

    /// <summary>
    /// Configure la route, le verbe HTTP et les options de l'endpoint.
    /// L'authentification est requise uniquement si <see cref="ConfigurationAuthentification.Activer"/> est <c>true</c>.
    /// </summary>
    public override void Configure()
    {
        Post("/v1/fichiers");
        if (!_configAuth.Activer)
            AllowAnonymous();
        AllowFileUploads();
        Options(o => o
            .WithTags("Fichiers")
            .WithSummary("Uploader un fichier temporaire")
            .WithDescription("""
                Dépose un fichier en **multipart/form-data**. Le fichier est immédiatement chiffré côté serveur (AES-256-GCM) et stocké avec ses métadonnées d'expiration.

                **Champs du formulaire :**
                - `Fichier` *(obligatoire)* — fichier à uploader.
                - `DureeVieMinutes` *(optionnel)* — nombre de minutes avant expiration (à partir de l'upload).
                - `NombreAccesMax` *(optionnel)* — nombre maximal de téléchargements autorisés.

                **Comportement par défaut :**
                - Si `NombreAccesMax` est fourni sans `DureeVieMinutes`, une durée de 60 minutes est appliquée automatiquement.
                - Si aucune limite n'est fournie, une durée de 30 minutes est appliquée automatiquement.

                **Réponse 201 :** contient l'`identifiant` du fichier et un lien direct de téléchargement dans `liens.telechargement`.

                **Réponse 400 :** si le fichier est absent ou si les paramètres sont invalides.
                """)
            .Produces<ReponseBase<DonneesUpload>>(201)
            .Produces<ReponseBase<DonneesUpload>>(400));
    }

    /// <summary>
    /// Traite la requête d'upload : valide les paramètres, puis délègue à MediatR.
    /// </summary>
    public override async Task HandleAsync(RequeteUpload req, CancellationToken ct)
    {
        _logger.LogDebug("Réception d'une requête d'upload");

        // Vérification de la présence du fichier
        if (req.Fichier is null || req.Fichier.Length == 0)
        {
            _logger.LogWarning("Upload refusé — aucun fichier fourni");
            await SendAsync(
                ReponseBase<DonneesUpload>.Erreur("ERR001", "Le fichier est obligatoire et ne doit pas être vide."),
                statusCode: 400,
                cancellation: ct);
            return;
        }

        // Normalisation : une chaîne vide soumise en form-data est liée comme 0 — on le traite comme null
        var dureeVieMinutes = req.DureeVieMinutes > 0 ? req.DureeVieMinutes : null;
        var nombreAccesMax = req.NombreAccesMax > 0 ? req.NombreAccesMax : null;

        // Durée de vie par défaut si non fournie :
        //   - 60 minutes quand un nombre d'accès max est précisé
        //   - 30 minutes quand aucune limite n'est fournie
        string? messageDureeDefaut = null;
        if (dureeVieMinutes is null && nombreAccesMax is not null)
        {
            dureeVieMinutes = 60;
            messageDureeDefaut = "Aucune durée de vie fournie — durée par défaut appliquée : 60 minutes.";
            _logger.LogDebug("Nombre d'accès max fourni sans durée — durée de vie par défaut appliquée : 60 minutes");
        }
        else if (dureeVieMinutes is null && nombreAccesMax is null)
        {
            dureeVieMinutes = 30;
            messageDureeDefaut = "Aucune limite fournie — durée de vie par défaut appliquée : 30 minutes.";
            _logger.LogDebug("Aucune limite fournie — durée de vie par défaut appliquée : 30 minutes");
        }

        // Construction et validation de la commande MediatR
        var commande = new CommandeUploadFichier
        {
            Contenu = req.Fichier.OpenReadStream(),
            NomFichier = req.Fichier.FileName,
            TypeMime = req.Fichier.ContentType,
            TailleOctets = req.Fichier.Length,
            DureeVieMinutes = dureeVieMinutes,
            NombreAccesMax = nombreAccesMax
        };

        var validateur = new ValidateurUploadFichier();
        var resultatValidation = await validateur.ValidateAsync(commande, ct);

        if (!resultatValidation.IsValid)
        {
            var erreurs = resultatValidation.Errors
                .Select((e, i) => ($"ERR{(i + 1):D3}", e.ErrorMessage));

            _logger.LogWarning("Upload refusé — erreurs de validation : {Erreurs}",
                string.Join("; ", resultatValidation.Errors.Select(e => e.ErrorMessage)));

            await SendAsync(
                ReponseBase<DonneesUpload>.ValidationInvalide(erreurs),
                statusCode: 400,
                cancellation: ct);
            return;
        }

        // Exécution de la commande
        var resultat = await _mediator.Send(commande, ct);

        // Construction de la réponse avec lien de téléchargement
        var reponse = ReponseBase<DonneesUpload>.Succes(
            new DonneesUpload(resultat.Identifiant),
            "INF001",
            "Le fichier a été déposé avec succès.");
        if (messageDureeDefaut is not null)
            reponse.Informations.Add(new MessageInformation("INF002", messageDureeDefaut));
        reponse.Liens.Add(new Dictionary<string, string> { ["telechargement"] = resultat.UrlTelechargement });

        _logger.LogDebug("Réponse 201 envoyée pour upload — identifiant={Identifiant}", resultat.Identifiant);
        await SendAsync(reponse, statusCode: 201, cancellation: ct);
    }
}
