namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;

/// <summary>
/// Paramètres de chiffrement AES-256-GCM.
/// La clé maître est gérée par IGestionnaireCles avec rotation périodique automatique.
/// </summary>
public sealed class ConfigurationChiffrement
{
    /// <summary>Clé de section dans appsettings.json.</summary>
    public const string Section = "Chiffrement";

    /// <summary>Taille en octets de chaque chunk lors du chiffrement/déchiffrement (défaut : 1 Mo).</summary>
    public int TailleChunkOctets { get; init; } = 1_048_576;
}
