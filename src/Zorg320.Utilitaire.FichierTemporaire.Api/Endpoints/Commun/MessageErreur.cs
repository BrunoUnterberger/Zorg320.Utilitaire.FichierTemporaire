using System.Text.Json.Serialization;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.Commun;

/// <summary>
/// Représente un message d'erreur dans la réponse API standardisée.
/// </summary>
public sealed record MessageErreur(
    /// <summary>Code d'erreur métier (ex. : ERR001).</summary>
    [property: JsonPropertyName("code")] string Code,
    /// <summary>Description lisible de l'erreur.</summary>
    [property: JsonPropertyName("message")] string Message
);
