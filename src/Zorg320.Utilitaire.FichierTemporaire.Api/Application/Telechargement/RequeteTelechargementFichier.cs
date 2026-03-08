using MediatR;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Telechargement;

/// <summary>
/// Résultat d'une requête de téléchargement de fichier.
/// Contient le flux déchiffré prêt à être streamé vers le client.
/// </summary>
public sealed record ResultatTelechargement(
    /// <summary>Flux de lecture des données déchiffrées.</summary>
    Stream Contenu,
    /// <summary>Nom original du fichier pour le header Content-Disposition.</summary>
    string NomFichier,
    /// <summary>Type MIME du fichier pour le header Content-Type.</summary>
    string TypeMime,
    /// <summary>Taille originale du fichier en octets.</summary>
    long TailleOctets
);

/// <summary>
/// Requête MediatR pour le téléchargement d'un fichier temporaire par son identifiant.
/// </summary>
public sealed record RequeteTelechargementFichier(
    /// <summary>Identifiant unique du fichier à télécharger.</summary>
    string Identifiant
) : IRequest<ResultatTelechargement>;
