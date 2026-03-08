namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

/// <summary>
/// Exception levée lorsqu'un fichier temporaire demandé est introuvable sur le système de fichiers.
/// </summary>
public sealed class FichierIntrouvableException : Exception
{
    /// <summary>Identifiant du fichier introuvable.</summary>
    public string Identifiant { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="FichierIntrouvableException"/>.
    /// </summary>
    /// <param name="identifiant">Identifiant du fichier introuvable.</param>
    public FichierIntrouvableException(string identifiant)
        : base($"Le fichier avec l'identifiant '{identifiant}' est introuvable.")
    {
        Identifiant = identifiant;
    }
}
