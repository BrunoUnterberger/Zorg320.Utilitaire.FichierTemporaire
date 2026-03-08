using System.Text.Json.Serialization;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.Commun;

/// <summary>
/// Représente un message d'information dans la réponse API standardisée.
/// </summary>
public sealed record MessageInformation(
    /// <summary>Code d'information (ex. : INF001).</summary>
    [property: JsonPropertyName("code")] string Code,
    /// <summary>Description lisible de l'information.</summary>
    [property: JsonPropertyName("message")] string Message
);
