namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

/// <summary>
/// Paramètres de stockage des fichiers temporaires sur le système de fichiers local.
/// </summary>
public sealed class ConfigurationStockage
{
    /// <summary>Clé de section dans appsettings.json.</summary>
    public const string Section = "Stockage";

    /// <summary>Chemin absolu du répertoire racine où sont stockés les fichiers.</summary>
    public required string CheminRacine { get; init; }

    /// <summary>Extension utilisée pour les fichiers chiffrés (ex. : .enc).</summary>
    public string ExtensionFichier { get; init; } = ".enc";

    /// <summary>Extension utilisée pour les fichiers de métadonnées (ex. : .meta).</summary>
    public string ExtensionMetadonnees { get; init; } = ".meta";
}
