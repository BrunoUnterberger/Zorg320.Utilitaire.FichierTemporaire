using System.Text.Json.Serialization;

namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;

/// <summary>
/// Représente les métadonnées associées à un fichier temporaire chiffré.
/// Ces informations sont persistées dans un fichier JSON sidecar (.meta) à côté du fichier chiffré.
/// </summary>
public sealed record MetadonneesFichier
{
    /// <summary>Identifiant unique du fichier (GUID).</summary>
    [JsonPropertyName("identifiant")]
    public required string Identifiant { get; init; }

    /// <summary>Nom original du fichier tel que fourni lors de l'upload.</summary>
    [JsonPropertyName("nomOriginal")]
    public required string NomOriginal { get; init; }

    /// <summary>Type MIME du fichier (ex. : application/pdf, image/png).</summary>
    [JsonPropertyName("typeMime")]
    public required string TypeMime { get; init; }

    /// <summary>Date et heure de création du fichier (UTC).</summary>
    [JsonPropertyName("dateCreation")]
    public required DateTimeOffset DateCreation { get; init; }

    /// <summary>
    /// Date et heure d'expiration (UTC). Null si aucune limite de temps n'est définie.
    /// Le fichier est considéré expiré dès que cette date est atteinte.
    /// </summary>
    [JsonPropertyName("dateExpiration")]
    public DateTimeOffset? DateExpiration { get; init; }

    /// <summary>
    /// Nombre maximal d'accès autorisés en téléchargement. Null si aucune limite d'accès n'est définie.
    /// Le fichier est considéré expiré dès que ce seuil est atteint.
    /// </summary>
    [JsonPropertyName("nombreAccesMax")]
    public int? NombreAccesMax { get; init; }

    /// <summary>Nombre de téléchargements effectués depuis la création du fichier.</summary>
    [JsonPropertyName("nombreAccesCourant")]
    public int NombreAccesCourant { get; init; }

    /// <summary>Taille originale du fichier en octets (avant chiffrement).</summary>
    [JsonPropertyName("tailleOctets")]
    public long TailleOctets { get; init; }

    /// <summary>
    /// Sel aléatoire encodé en Base64 utilisé avec HKDF pour dériver la clé de chiffrement du fichier.
    /// Stocké ici pour permettre le déchiffrement ultérieur.
    /// </summary>
    [JsonPropertyName("sel")]
    public required string Sel { get; init; }

    /// <summary>
    /// Identifiant GUID (sans tirets) de la version de clé maître utilisée lors du chiffrement.
    /// Permet de retrouver la bonne clé dans le trousseau lors du déchiffrement.
    /// </summary>
    [JsonPropertyName("versionCle")]
    public required string VersionCle { get; init; }

    /// <summary>
    /// Détermine si le fichier est expiré en tenant compte de la date d'expiration
    /// et du nombre d'accès maximum. Le premier critère atteint rend le fichier expiré.
    /// </summary>
    /// <param name="maintenant">La date/heure courante à comparer.</param>
    /// <returns>True si le fichier est expiré ; false sinon.</returns>
    public bool EstExpire(DateTimeOffset maintenant)
    {
        if (DateExpiration.HasValue && maintenant >= DateExpiration.Value)
            return true;

        if (NombreAccesMax.HasValue && NombreAccesCourant >= NombreAccesMax.Value)
            return true;

        return false;
    }

    /// <summary>
    /// Retourne une nouvelle instance avec le compteur d'accès incrémenté de 1.
    /// </summary>
    public MetadonneesFichier AvecAccesIncremente()
        => this with { NombreAccesCourant = NombreAccesCourant + 1 };
}
