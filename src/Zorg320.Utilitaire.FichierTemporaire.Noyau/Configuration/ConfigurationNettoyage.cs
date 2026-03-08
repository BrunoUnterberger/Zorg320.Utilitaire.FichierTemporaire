namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

/// <summary>
/// Paramètres du service de nettoyage automatique des fichiers expirés.
/// </summary>
public sealed class ConfigurationNettoyage
{
    /// <summary>Clé de section dans appsettings.json.</summary>
    public const string Section = "Nettoyage";

    /// <summary>Intervalle en minutes entre chaque passage du service de nettoyage (défaut : 60 minutes).</summary>
    public int IntervalleMinutes { get; init; } = 60;
}
