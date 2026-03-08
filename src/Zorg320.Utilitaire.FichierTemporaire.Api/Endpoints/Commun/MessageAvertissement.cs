using System.Text.Json.Serialization;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.Commun;

/// <summary>
/// Représente un message d'avertissement dans la réponse API standardisée.
/// </summary>
public sealed record MessageAvertissement(
    /// <summary>Code d'avertissement (ex. : WRN001).</summary>
    [property: JsonPropertyName("code")] string Code,
    /// <summary>Description lisible de l'avertissement.</summary>
    [property: JsonPropertyName("message")] string Message
);
