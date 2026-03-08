using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;

namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;

/// <summary>
/// Contrat du service de stockage des fichiers temporaires et de leurs métadonnées.
/// Chaque fichier est accompagné d'un fichier sidecar JSON contenant ses métadonnées.
/// </summary>
public interface IServiceStockage
{
    /// <summary>
    /// Sauvegarde un flux de données chiffrées sur le système de fichiers.
    /// </summary>
    /// <param name="identifiant">Identifiant unique du fichier.</param>
    /// <param name="contenuChiffre">Flux contenant les données chiffrées à écrire.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    Task SauvegarderFichierAsync(string identifiant, Stream contenuChiffre, CancellationToken ct = default);

    /// <summary>
    /// Sauvegarde les métadonnées d'un fichier dans le fichier sidecar JSON.
    /// </summary>
    /// <param name="metadonnees">Métadonnées à persister.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    Task SauvegarderMetadonneesAsync(MetadonneesFichier metadonnees, CancellationToken ct = default);

    /// <summary>
    /// Ouvre un flux de lecture sur le fichier chiffré identifié.
    /// </summary>
    /// <param name="identifiant">Identifiant du fichier.</param>
    /// <returns>Flux de lecture sur le fichier chiffré.</returns>
    /// <exception cref="Domaine.Exceptions.FichierIntrouvableException">Levée si le fichier n'existe pas.</exception>
    Stream OuvrirFichier(string identifiant);

    /// <summary>
    /// Lit les métadonnées associées à un fichier depuis le fichier sidecar JSON.
    /// </summary>
    /// <param name="identifiant">Identifiant du fichier.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns>Les métadonnées du fichier.</returns>
    /// <exception cref="Domaine.Exceptions.FichierIntrouvableException">Levée si le fichier sidecar n'existe pas.</exception>
    Task<MetadonneesFichier> LireMetadonneesAsync(string identifiant, CancellationToken ct = default);

    /// <summary>
    /// Supprime le fichier chiffré et son fichier sidecar de métadonnées.
    /// </summary>
    /// <param name="identifiant">Identifiant du fichier à supprimer.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    Task SupprimerAsync(string identifiant, CancellationToken ct = default);

    /// <summary>
    /// Retourne la liste des métadonnées de tous les fichiers présents dans le stockage.
    /// Utilisé par le service de nettoyage pour identifier les fichiers expirés.
    /// </summary>
    /// <param name="ct">Jeton d'annulation.</param>
    IAsyncEnumerable<MetadonneesFichier> EnumererTousAsync(CancellationToken ct = default);
}
