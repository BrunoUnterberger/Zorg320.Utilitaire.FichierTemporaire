namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

/// <summary>
/// Paramètres de gestion du trousseau de clés rotatif.
/// </summary>
public sealed class ConfigurationCles
{
    /// <summary>Clé de section dans appsettings.json.</summary>
    public const string Section = "Cles";

    /// <summary>
    /// Chemin du fichier JSON contenant le trousseau de clés.
    /// Doit pointer vers un volume persistant partagé entre l'Api et le Worker.
    /// </summary>
    public string CheminFichierTrousseau { get; init; } = "cles/trousseau.json";
}
