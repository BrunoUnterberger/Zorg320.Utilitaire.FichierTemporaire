namespace Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;

/// <summary>
/// Contrat du service de chiffrement/déchiffrement des fichiers temporaires.
/// Le chiffrement est réalisé par chunks pour éviter de charger le fichier entier en mémoire.
/// Algorithme utilisé : AES-256-GCM avec dérivation de clé HKDF par fichier.
/// </summary>
public interface IServiceChiffrement
{
    /// <summary>
    /// Chiffre le flux source et écrit le résultat chiffré dans le flux de destination.
    /// Le flux de destination contiendra : [nombre de chunks][nonce][tag][longueur][données]...
    /// </summary>
    /// <param name="source">Flux de lecture contenant les données brutes à chiffrer.</param>
    /// <param name="destination">Flux d'écriture qui recevra les données chiffrées.</param>
    /// <param name="selBase64">Sel aléatoire encodé en Base64 utilisé pour dériver la clé HKDF.</param>
    /// <param name="cleMaitre">Clé maître de 32 octets utilisée pour la dérivation HKDF.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    Task ChiffrerAsync(Stream source, Stream destination, string selBase64, byte[] cleMaitre, CancellationToken ct = default);

    /// <summary>
    /// Déchiffre le flux source chiffré et écrit les données déchiffrées dans le flux de destination.
    /// </summary>
    /// <param name="source">Flux de lecture contenant les données chiffrées.</param>
    /// <param name="destination">Flux d'écriture qui recevra les données déchiffrées.</param>
    /// <param name="selBase64">Sel aléatoire encodé en Base64 utilisé pour dériver la clé HKDF.</param>
    /// <param name="cleMaitre">Clé maître de 32 octets correspondant à la version utilisée lors du chiffrement.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    Task DechiffrerAsync(Stream source, Stream destination, string selBase64, byte[] cleMaitre, CancellationToken ct = default);

    /// <summary>
    /// Génère un nouveau sel aléatoire encodé en Base64 pour une utilisation avec HKDF.
    /// </summary>
    /// <returns>Sel aléatoire encodé en Base64 (32 octets).</returns>
    string GenererSel();
}
