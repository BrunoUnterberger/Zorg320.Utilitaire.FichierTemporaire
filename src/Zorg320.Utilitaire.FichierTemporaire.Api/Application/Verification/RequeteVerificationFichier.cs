using MediatR;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Verification;

/// <summary>
/// Requête MediatR pour vérifier la disponibilité d'un fichier temporaire par son identifiant.
/// Ne modifie pas le compteur d'accès.
/// </summary>
public sealed record RequeteVerificationFichier(
    /// <summary>Identifiant unique du fichier à vérifier.</summary>
    string Identifiant
) : IRequest<bool>;
