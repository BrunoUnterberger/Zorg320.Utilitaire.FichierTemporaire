using FastEndpoints;
using MediatR;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Telechargement;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.V1;

/// <summary>
/// Modèle de la requête de téléchargement (paramètre de route).
/// </summary>
public sealed class RequeteTelechargement
{
    /// <summary>Identifiant unique du fichier à télécharger.</summary>
    public string Identifiant { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint FastEndpoints pour le téléchargement d'un fichier temporaire par son identifiant.
/// Route : GET /v1/fichiers/{identifiant}
/// Retourne le fichier déchiffré en streaming.
/// En cas d'indisponibilité (introuvable ou expiré) : 404.
/// </summary>
public sealed class TelechargementFichierEndpoint : Endpoint<RequeteTelechargement>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TelechargementFichierEndpoint> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="TelechargementFichierEndpoint"/>.
    /// </summary>
    public TelechargementFichierEndpoint(IMediator mediator, ILogger<TelechargementFichierEndpoint> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Configure la route, le verbe HTTP et les options de l'endpoint.
    /// </summary>
    public override void Configure()
    {
        Get("/v1/fichiers/{identifiant}");
        AllowAnonymous();
        Options(o => o
            .WithTags("Fichiers")
            .WithSummary("Télécharger un fichier temporaire")
            .WithDescription("Télécharge et déchiffre un fichier temporaire. Retourne 404 si le fichier est introuvable ou expiré.")
            .Produces(200)
            .Produces(404));
    }

    /// <summary>
    /// Traite la requête de téléchargement : résout le fichier, vérifie l'expiration et streame le contenu déchiffré.
    /// </summary>
    public override async Task HandleAsync(RequeteTelechargement req, CancellationToken ct)
    {
        _logger.LogDebug("Réception d'une requête de téléchargement — identifiant={Identifiant}", req.Identifiant);

        if (string.IsNullOrWhiteSpace(req.Identifiant))
        {
            await SendStringAsync("L'identifiant du fichier est obligatoire.", statusCode: 400, cancellation: ct);
            return;
        }

        try
        {
            var resultat = await _mediator.Send(new RequeteTelechargementFichier(req.Identifiant), ct);

            _logger.LogDebug("Envoi du fichier en streaming — identifiant={Identifiant}, type={TypeMime}",
                req.Identifiant, resultat.TypeMime);

            // Envoi du fichier déchiffré en streaming avec les headers appropriés
            await SendStreamAsync(
                stream: resultat.Contenu,
                fileName: resultat.NomFichier,
                fileLengthBytes: resultat.TailleOctets,
                contentType: resultat.TypeMime,
                enableRangeProcessing: false,
                cancellation: ct);
        }
        catch (FichierIntrouvableException ex)
        {
            _logger.LogWarning("Fichier indisponible (introuvable) — identifiant={Identifiant}", ex.Identifiant);
            await SendNotFoundAsync(ct);
        }
        catch (FichierExpireException ex)
        {
            _logger.LogWarning("Fichier indisponible (expiré) — identifiant={Identifiant}", ex.Identifiant);
            await SendNotFoundAsync(ct);
        }
    }
}
