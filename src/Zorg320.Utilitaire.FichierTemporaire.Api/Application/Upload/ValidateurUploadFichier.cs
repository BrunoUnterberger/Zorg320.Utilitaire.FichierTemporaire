using FluentValidation;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Application.Upload;

/// <summary>
/// Validateur FluentValidation pour la commande d'upload d'un fichier temporaire.
/// Au moins une limite de durée de vie (temps ou nombre d'accès) est obligatoire.
/// </summary>
public sealed class ValidateurUploadFichier : AbstractValidator<CommandeUploadFichier>
{
    /// <summary>
    /// Initialise les règles de validation pour l'upload d'un fichier temporaire.
    /// </summary>
    public ValidateurUploadFichier()
    {
        RuleFor(c => c.NomFichier)
            .NotEmpty()
            .WithMessage("Le nom du fichier est obligatoire.")
            .MaximumLength(255)
            .WithMessage("Le nom du fichier ne doit pas dépasser 255 caractères.");

        RuleFor(c => c.TypeMime)
            .NotEmpty()
            .WithMessage("Le type MIME du fichier est obligatoire.");

        RuleFor(c => c.TailleOctets)
            .GreaterThan(0)
            .WithMessage("La taille du fichier doit être supérieure à zéro.");

        RuleFor(c => c.DureeVieMinutes)
            .GreaterThan(0)
            .WithMessage("La durée de vie en minutes doit être strictement positive.")
            .When(c => c.DureeVieMinutes.HasValue);

        RuleFor(c => c.NombreAccesMax)
            .GreaterThan(0)
            .WithMessage("Le nombre d'accès maximum doit être strictement positif.")
            .When(c => c.NombreAccesMax.HasValue);

        RuleFor(c => c)
            .Must(c => c.DureeVieMinutes.HasValue || c.NombreAccesMax.HasValue)
            .WithMessage("Au moins une limite de durée de vie est obligatoire : durée en minutes ou nombre d'accès maximum.")
            .WithName("DureeDeVie");
    }
}
