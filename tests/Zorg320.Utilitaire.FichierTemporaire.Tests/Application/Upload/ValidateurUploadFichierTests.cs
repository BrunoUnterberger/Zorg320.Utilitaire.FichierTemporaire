using FluentAssertions;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Upload;

namespace Zorg320.Utilitaire.FichierTemporaire.Tests.Application.Upload;

public sealed class ValidateurUploadFichierTests
{
    private readonly ValidateurUploadFichier _validateur = new();

    private static CommandeUploadFichier CommandeValide() => new()
    {
        Contenu = Stream.Null,
        NomFichier = "document.pdf",
        TypeMime = "application/pdf",
        TailleOctets = 1024,
        DureeVieMinutes = 30
    };

    // ---- NomFichier ----

    [Fact]
    public async Task Valider_NomFichierVide_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { NomFichier = "" };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("Le nom du fichier est obligatoire."));
    }

    [Fact]
    public async Task Valider_NomFichierTropLong_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { NomFichier = new string('a', 256) };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("255 caractères"));
    }

    [Fact]
    public async Task Valider_NomFichierDe255Caracteres_Reussit()
    {
        var commande = CommandeValide() with { NomFichier = new string('a', 255) };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeTrue();
    }

    // ---- TypeMime ----

    [Fact]
    public async Task Valider_TypeMimeVide_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { TypeMime = "" };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("Le type MIME du fichier est obligatoire."));
    }

    // ---- TailleOctets ----

    [Fact]
    public async Task Valider_TailleOctetsZero_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { TailleOctets = 0 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("La taille du fichier doit être supérieure à zéro."));
    }

    [Fact]
    public async Task Valider_TailleOctetsNegative_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { TailleOctets = -1 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
    }

    // ---- DureeVieMinutes ----

    [Fact]
    public async Task Valider_DureeVieMinutesZero_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { DureeVieMinutes = 0 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("La durée de vie en minutes doit être strictement positive."));
    }

    [Fact]
    public async Task Valider_DureeVieMinutesNegative_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { DureeVieMinutes = -5 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Valider_DureeVieMinutesNull_AvecNombreAccesMax_Reussit()
    {
        var commande = CommandeValide() with { DureeVieMinutes = null, NombreAccesMax = 3 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeTrue();
    }

    // ---- NombreAccesMax ----

    [Fact]
    public async Task Valider_NombreAccesMaxZero_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { NombreAccesMax = 0 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("Le nombre d'accès maximum doit être strictement positif."));
    }

    [Fact]
    public async Task Valider_NombreAccesMaxNull_AvecDureeVie_Reussit()
    {
        var commande = CommandeValide() with { NombreAccesMax = null, DureeVieMinutes = 60 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeTrue();
    }

    // ---- Règle combinée : au moins une limite obligatoire ----

    [Fact]
    public async Task Valider_AucuneLimite_EchoueAvecMessage()
    {
        var commande = CommandeValide() with { DureeVieMinutes = null, NombreAccesMax = null };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeFalse();
        resultat.Errors.Should().Contain(e => e.ErrorMessage.Contains("Au moins une limite de durée de vie est obligatoire"));
    }

    [Fact]
    public async Task Valider_DeuxLimitesFournies_Reussit()
    {
        var commande = CommandeValide() with { DureeVieMinutes = 60, NombreAccesMax = 5 };

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeTrue();
    }

    // ---- Cas nominal complet ----

    [Fact]
    public async Task Valider_CommandeValide_Reussit()
    {
        var commande = CommandeValide();

        var resultat = await _validateur.ValidateAsync(commande);

        resultat.IsValid.Should().BeTrue();
        resultat.Errors.Should().BeEmpty();
    }
}
