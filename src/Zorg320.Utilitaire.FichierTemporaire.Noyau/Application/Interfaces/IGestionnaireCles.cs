namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;

/// <summary>
/// Contrat du gestionnaire de clés maîtres avec rotation périodique.
/// Chaque version de clé est identifiée par un GUID opaque.
/// Les anciennes versions sont conservées tant que des fichiers actifs y font référence.
/// </summary>
public interface IGestionnaireCles
{
    /// <summary>
    /// Retourne la version courante et la clé maître active pour chiffrer un nouveau fichier.
    /// </summary>
    (string Version, byte[] CleMaitre) ObtenirCleCourante();

    /// <summary>
    /// Retourne la clé maître associée à une version donnée pour déchiffrer un fichier existant.
    /// </summary>
    /// <param name="version">Identifiant GUID de la version de clé.</param>
    /// <exception cref="InvalidOperationException">Levée si la version est inconnue.</exception>
    byte[] ObtenirCle(string version);

    /// <summary>
    /// Génère une nouvelle clé aléatoire, l'ajoute au trousseau et la définit comme version courante.
    /// Persiste le trousseau sur le disque.
    /// </summary>
    Task EffectuerRotationAsync(CancellationToken ct = default);

    /// <summary>
    /// Supprime du trousseau les versions qui ne sont plus utilisées par aucun fichier actif.
    /// La version courante n'est jamais supprimée.
    /// </summary>
    /// <param name="versionsUtilisees">Ensemble des versions référencées par les fichiers existants.</param>
    Task SupprimerVersionsInutilisees(IReadOnlySet<string> versionsUtilisees, CancellationToken ct = default);
}
