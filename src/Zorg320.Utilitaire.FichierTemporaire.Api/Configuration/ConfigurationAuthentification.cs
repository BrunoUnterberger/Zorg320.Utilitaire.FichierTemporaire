namespace Zorg320.Utilitaire.FichierTemporaire.Api.Configuration;

/// <summary>
/// Paramètres de l'authentification OIDC / JWT Bearer.
/// Section appsettings : "Authentification".
/// </summary>
public sealed class ConfigurationAuthentification
{
    /// <summary>Nom de la section dans appsettings.json.</summary>
    public const string Section = "Authentification";

    /// <summary>
    /// Active ou désactive l'authentification.
    /// Quand <c>false</c>, tous les endpoints sont accessibles anonymement.
    /// Par défaut : <c>false</c>.
    /// </summary>
    public bool Activer { get; set; } = false;

    /// <summary>
    /// URL de l'autorité OIDC (fournisseur d'identité).
    /// Exemples : https://login.microsoftonline.com/{tenant}/v2.0
    ///            https://auth.example.com/realms/mon-realm
    /// </summary>
    public string Autorite { get; set; } = string.Empty;

    /// <summary>
    /// Audience attendue dans le JWT (claim "aud").
    /// Doit correspondre à l'identifiant de l'API déclaré chez le fournisseur.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Exige HTTPS pour le téléchargement des métadonnées OIDC.
    /// Désactiver uniquement en environnement de développement local.
    /// Par défaut : <c>true</c>.
    /// </summary>
    public bool ExigerHttpsMetadonnees { get; set; } = true;

    /// <summary>
    /// Liste de scopes OAuth2 requis (claim "scope").
    /// Laissez vide pour n'exiger qu'une authentification sans scope particulier.
    /// Exemple : [ "fichiers-temporaires.upload" ]
    /// </summary>
    public string[] ScopesRequis { get; set; } = [];
}
