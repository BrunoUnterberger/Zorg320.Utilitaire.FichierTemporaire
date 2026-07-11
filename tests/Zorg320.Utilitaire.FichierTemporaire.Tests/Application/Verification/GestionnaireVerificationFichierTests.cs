using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Verification;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

namespace Zorg320.Utilitaire.FichierTemporaire.Tests.Application.Verification;

public sealed class GestionnaireVerificationFichierTests
{
    private readonly IServiceStockage _stockage = Substitute.For<IServiceStockage>();
    private readonly GestionnaireVerificationFichier _gestionnaire;

    public GestionnaireVerificationFichierTests()
    {
        _gestionnaire = new GestionnaireVerificationFichier(
            _stockage,
            NullLogger<GestionnaireVerificationFichier>.Instance);
    }

    private static MetadonneesFichier CreerMetadonneesValides(
        string identifiant = "abc123",
        DateTimeOffset? dateExpiration = null,
        int? nombreAccesMax = null,
        int nombreAccesCourant = 0)
        => new()
        {
            Identifiant = identifiant,
            NomOriginal = "rapport.pdf",
            TypeMime = "application/pdf",
            DateCreation = DateTimeOffset.UtcNow.AddHours(-1),
            DateExpiration = dateExpiration,
            NombreAccesMax = nombreAccesMax,
            NombreAccesCourant = nombreAccesCourant,
            TailleOctets = 2048,
            Sel = "sel-base64",
            VersionCle = "v1"
        };

    // ---- Cas nominal ----

    [Fact]
    public async Task Handle_FichierDisponible_RetourneVrai()
    {
        var meta = CreerMetadonneesValides(dateExpiration: DateTimeOffset.UtcNow.AddHours(1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        var resultat = await _gestionnaire.Handle(new RequeteVerificationFichier("abc123"), CancellationToken.None);

        resultat.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FichierDisponible_NModifiePasLeCompteurAcces()
    {
        var meta = CreerMetadonneesValides(nombreAccesMax: 3, nombreAccesCourant: 1,
            dateExpiration: DateTimeOffset.UtcNow.AddHours(1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        await _gestionnaire.Handle(new RequeteVerificationFichier("abc123"), CancellationToken.None);

        await _stockage.DidNotReceive().SauvegarderMetadonneesAsync(
            Arg.Any<MetadonneesFichier>(), Arg.Any<CancellationToken>());
    }

    // ---- Fichier introuvable ----

    [Fact]
    public async Task Handle_FichierInexistant_RetourneFaux()
    {
        _stockage
            .LireMetadonneesAsync("inconnu", Arg.Any<CancellationToken>())
            .ThrowsAsync(new FichierIntrouvableException("inconnu"));

        var resultat = await _gestionnaire.Handle(new RequeteVerificationFichier("inconnu"), CancellationToken.None);

        resultat.Should().BeFalse();
    }

    // ---- Fichier expiré par date ----

    [Fact]
    public async Task Handle_FichierExpireParDate_RetourneFaux()
    {
        var meta = CreerMetadonneesValides(dateExpiration: DateTimeOffset.UtcNow.AddMinutes(-1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        var resultat = await _gestionnaire.Handle(new RequeteVerificationFichier("abc123"), CancellationToken.None);

        resultat.Should().BeFalse();
    }

    // ---- Fichier expiré par nombre d'accès ----

    [Fact]
    public async Task Handle_NombreAccesMaxAtteint_RetourneFaux()
    {
        var meta = CreerMetadonneesValides(nombreAccesMax: 2, nombreAccesCourant: 2);
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        var resultat = await _gestionnaire.Handle(new RequeteVerificationFichier("abc123"), CancellationToken.None);

        resultat.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DernierAccesNonEncoreUtilise_RetourneVrai()
    {
        // accès 0/1 : non encore expiré
        var meta = CreerMetadonneesValides(nombreAccesMax: 1, nombreAccesCourant: 0);
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        var resultat = await _gestionnaire.Handle(new RequeteVerificationFichier("abc123"), CancellationToken.None);

        resultat.Should().BeTrue();
    }
}
