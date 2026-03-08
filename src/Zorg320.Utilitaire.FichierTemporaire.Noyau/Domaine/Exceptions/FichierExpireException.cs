namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

/// <summary>
/// Exception levée lorsqu'un fichier temporaire demandé a dépassé sa durée de vie
/// (expiration par date ou par nombre d'accès maximum atteint).
/// </summary>
public sealed class FichierExpireException : Exception
{
    /// <summary>Identifiant du fichier expiré.</summary>
    public string Identifiant { get; }

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="FichierExpireException"/>.
    /// </summary>
    /// <param name="identifiant">Identifiant du fichier expiré.</param>
    public FichierExpireException(string identifiant)
        : base($"Le fichier avec l'identifiant '{identifiant}' est expiré et ne peut plus être téléchargé.")
    {
        Identifiant = identifiant;
    }
}
