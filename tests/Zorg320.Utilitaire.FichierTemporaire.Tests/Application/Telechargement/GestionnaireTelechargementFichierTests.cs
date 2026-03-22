using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Telechargement;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Exceptions;

namespace Zorg320.Utilitaire.FichierTemporaire.Tests.Application.Telechargement;

public sealed class GestionnaireTelechargementFichierTests
{
    private readonly IServiceChiffrement _chiffrement = Substitute.For<IServiceChiffrement>();
    private readonly IServiceStockage _stockage = Substitute.For<IServiceStockage>();
    private readonly IGestionnaireCles _gestionnaireCles = Substitute.For<IGestionnaireCles>();
    private readonly GestionnaireTelechargementFichier _gestionnaire;

    public GestionnaireTelechargementFichierTests()
    {
        _gestionnaire = new GestionnaireTelechargementFichier(
            _chiffrement,
            _stockage,
            _gestionnaireCles,
            NullLogger<GestionnaireTelechargementFichier>.Instance);
    }

    private static MetadonneesFichier CreerMetadonneesValides(
        string identifiant = "abc123",
        DateTimeOffset? dateExpiration = null,
        int? nombreAccesMax = null,
        int nombreAccesCourant = 0,
        string versionCle = "v1")
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
            VersionCle = versionCle
        };

    private void ConfigurerChiffrementDeuxStreamVides()
    {
        _stockage.OuvrirFichier(Arg.Any<string>()).Returns(new MemoryStream());
        _chiffrement
            .DechiffrerAsync(Arg.Any<Stream>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    // ---- Cas nominal ----

    [Fact]
    public async Task Handle_CasNominal_RetourneResultatAvecInfosFichier()
    {
        var meta = CreerMetadonneesValides(dateExpiration: DateTimeOffset.UtcNow.AddHours(1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);
        _gestionnaireCles.ObtenirCle("v1").Returns(new byte[32]);
        _stockage.SauvegarderMetadonneesAsync(Arg.Any<MetadonneesFichier>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        ConfigurerChiffrementDeuxStreamVides();

        var resultat = await _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None);

        resultat.NomFichier.Should().Be("rapport.pdf");
        resultat.TypeMime.Should().Be("application/pdf");
        resultat.TailleOctets.Should().Be(2048);
        resultat.Contenu.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CasNominal_IncrementeCompteurAcces()
    {
        var meta = CreerMetadonneesValides(nombreAccesCourant: 2, dateExpiration: DateTimeOffset.UtcNow.AddHours(1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);
        _gestionnaireCles.ObtenirCle("v1").Returns(new byte[32]);
        ConfigurerChiffrementDeuxStreamVides();

        MetadonneesFichier? metaEnregistree = null;
        _stockage
            .SauvegarderMetadonneesAsync(Arg.Do<MetadonneesFichier>(m => metaEnregistree = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None);

        metaEnregistree!.NombreAccesCourant.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CasNominal_UtiliseVersionCleDuFichier()
    {
        var meta = CreerMetadonneesValides(versionCle: "version-ancienne", dateExpiration: DateTimeOffset.UtcNow.AddHours(1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);
        _gestionnaireCles.ObtenirCle("version-ancienne").Returns(new byte[32]);
        _stockage.SauvegarderMetadonneesAsync(Arg.Any<MetadonneesFichier>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        ConfigurerChiffrementDeuxStreamVides();

        await _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None);

        _gestionnaireCles.Received(1).ObtenirCle("version-ancienne");
    }

    // ---- Fichier introuvable ----

    [Fact]
    public async Task Handle_FichierInexistant_LeveFichierIntrouvableException()
    {
        _stockage
            .LireMetadonneesAsync("inconnu", Arg.Any<CancellationToken>())
            .ThrowsAsync(new FichierIntrouvableException("inconnu"));

        var act = () => _gestionnaire.Handle(new RequeteTelechargementFichier("inconnu"), CancellationToken.None);

        await act.Should().ThrowAsync<FichierIntrouvableException>()
            .Where(e => e.Identifiant == "inconnu");
    }

    // ---- Fichier expiré par date ----

    [Fact]
    public async Task Handle_FichierExpireParDate_LeveFichierExpireException()
    {
        var meta = CreerMetadonneesValides(dateExpiration: DateTimeOffset.UtcNow.AddMinutes(-1));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        var act = () => _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None);

        await act.Should().ThrowAsync<FichierExpireException>()
            .Where(e => e.Identifiant == "abc123");
    }

    [Fact]
    public async Task Handle_FichierExpireParDate_NAppellePasLeChiffrement()
    {
        var meta = CreerMetadonneesValides(dateExpiration: DateTimeOffset.UtcNow.AddMinutes(-5));
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        try { await _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None); }
        catch (FichierExpireException) { }

        await _chiffrement.DidNotReceive().DechiffrerAsync(
            Arg.Any<Stream>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    // ---- Fichier expiré par nombre d'accès ----

    [Fact]
    public async Task Handle_NombreAccesMaxAtteint_LeveFichierExpireException()
    {
        var meta = CreerMetadonneesValides(nombreAccesMax: 2, nombreAccesCourant: 2);
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);

        var act = () => _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None);

        await act.Should().ThrowAsync<FichierExpireException>()
            .Where(e => e.Identifiant == "abc123");
    }

    [Fact]
    public async Task Handle_DernierAccesAutorise_Reussit()
    {
        // accès 1/1 : le compteur courant est 0, max est 1 → non expiré avant l'accès
        var meta = CreerMetadonneesValides(nombreAccesMax: 1, nombreAccesCourant: 0);
        _stockage.LireMetadonneesAsync("abc123", Arg.Any<CancellationToken>()).Returns(meta);
        _gestionnaireCles.ObtenirCle("v1").Returns(new byte[32]);
        _stockage.SauvegarderMetadonneesAsync(Arg.Any<MetadonneesFichier>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        ConfigurerChiffrementDeuxStreamVides();

        var act = () => _gestionnaire.Handle(new RequeteTelechargementFichier("abc123"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
