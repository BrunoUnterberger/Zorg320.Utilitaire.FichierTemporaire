using MediatR;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Upload;

/// <summary>
/// Résultat retourné après un upload de fichier réussi.
/// </summary>
public sealed record ResultatUpload(
    /// <summary>Identifiant unique attribué au fichier uploadé.</summary>
    string Identifiant,
    /// <summary>URL relative de téléchargement du fichier.</summary>
    string UrlTelechargement
);

/// <summary>
/// Commande MediatR pour l'upload d'un fichier temporaire.
/// Le flux est transmis directement pour éviter de charger le fichier en mémoire.
/// </summary>
public sealed record CommandeUploadFichier : IRequest<ResultatUpload>
{
    /// <summary>Flux de lecture du fichier à uploader.</summary>
    public required Stream Contenu { get; init; }

    /// <summary>Nom original du fichier tel que fourni par le client.</summary>
    public required string NomFichier { get; init; }

    /// <summary>Type MIME du fichier (ex. : application/pdf).</summary>
    public required string TypeMime { get; init; }

    /// <summary>Taille du fichier en octets.</summary>
    public long TailleOctets { get; init; }

    /// <summary>
    /// Durée de vie en minutes à partir du moment de l'upload.
    /// Null si aucune limite de temps n'est souhaitée.
    /// </summary>
    public int? DureeVieMinutes { get; init; }

    /// <summary>
    /// Nombre maximal d'accès en téléchargement autorisés.
    /// Null si aucune limite d'accès n'est souhaitée.
    /// </summary>
    public int? NombreAccesMax { get; init; }
}
