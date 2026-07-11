using FastEndpoints;
using MediatR;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Verification;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.V1;

/// <summary>
/// Modèle de la requête de vérification (paramètre de route).
/// </summary>
public sealed class RequeteVerification
{
    /// <summary>Identifiant unique du fichier à vérifier.</summary>
    public string Identifiant { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint FastEndpoints pour vérifier la disponibilité d'un fichier temporaire.
/// Route : HEAD /v1/fichiers/{identifiant}
/// Ne modifie pas le compteur d'accès.
/// Retourne 200 si disponible, 404 sinon.
/// </summary>
public sealed class VerificationFichierEndpoint : Endpoint<RequeteVerification>
{
    private readonly IMediator _mediator;
    private readonly ILogger<VerificationFichierEndpoint> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="VerificationFichierEndpoint"/>.
    /// </summary>
    public VerificationFichierEndpoint(IMediator mediator, ILogger<VerificationFichierEndpoint> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Configure la route, le verbe HTTP et les options de l'endpoint.
    /// </summary>
    public override void Configure()
    {
        Head("/v1/fichiers/{identifiant:guid}");
        AllowAnonymous();
        Options(o => o
            .WithTags("Fichiers")
            .WithSummary("Vérifier la disponibilité d'un fichier temporaire")
            .WithDescription("""
                Vérifie si un fichier temporaire identifié par son `identifiant` existe et est encore disponible.

                **N'incrémente pas le compteur d'accès.**

                **Réponse 200 :** le fichier existe et n'est pas expiré.

                **Réponse 404 :** le fichier est introuvable ou expiré (durée dépassée ou nombre d'accès épuisé).

                > Aucune authentification requise — cet endpoint est toujours anonyme.
                """)
            .Produces(200)
            .Produces(404));
    }

    /// <summary>
    /// Traite la requête de vérification et retourne 200 ou 404 selon la disponibilité.
    /// </summary>
    public override async Task HandleAsync(RequeteVerification req, CancellationToken ct)
    {
        _logger.LogDebug("Réception d'une requête de vérification — identifiant={Identifiant}", req.Identifiant);

        if (string.IsNullOrWhiteSpace(req.Identifiant))
        {
            await SendStringAsync("L'identifiant du fichier est obligatoire.", statusCode: 400, cancellation: ct);
            return;
        }

        var disponible = await _mediator.Send(new RequeteVerificationFichier(req.Identifiant), ct);

        if (disponible)
            await SendOkAsync(ct);
        else
            await SendNotFoundAsync(ct);
    }
}
