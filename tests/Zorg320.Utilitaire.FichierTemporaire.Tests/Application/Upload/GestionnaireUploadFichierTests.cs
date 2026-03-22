using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Zorg320.Utilitaire.FichierTemporaire.Api.Application.Upload;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;

namespace Zorg320.Utilitaire.FichierTemporaire.Tests.Application.Upload;

public sealed class GestionnaireUploadFichierTests
{
    private readonly IServiceChiffrement _chiffrement = Substitute.For<IServiceChiffrement>();
    private readonly IServiceStockage _stockage = Substitute.For<IServiceStockage>();
    private readonly IGestionnaireCles _gestionnaireCles = Substitute.For<IGestionnaireCles>();
    private readonly GestionnaireUploadFichier _gestionnaire;

    public GestionnaireUploadFichierTests()
    {
        var configStockage = Options.Create(new ConfigurationStockage { CheminRacine = "/tmp/test" });
        _gestionnaire = new GestionnaireUploadFichier(
            _chiffrement,
            _stockage,
            _gestionnaireCles,
            configStockage,
            NullLogger<GestionnaireUploadFichier>.Instance);

        // Comportements par défaut
        _chiffrement.GenererSel().Returns("sel-test-base64");
        _gestionnaireCles.ObtenirCleCourante().Returns(("v1", new byte[32]));
        _chiffrement
            .ChiffrerAsync(Arg.Any<Stream>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _stockage
            .SauvegarderFichierAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _stockage
            .SauvegarderMetadonneesAsync(Arg.Any<MetadonneesFichier>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_CasNominal_RetourneIdentifiantEtUrl()
    {
        var commande = new CommandeUploadFichier
        {
            Contenu = new MemoryStream(new byte[] { 1, 2, 3 }),
            NomFichier = "rapport.pdf",
            TypeMime = "application/pdf",
            TailleOctets = 3,
            DureeVieMinutes = 30
        };

        var resultat = await _gestionnaire.Handle(commande, CancellationToken.None);

        resultat.Identifiant.Should().NotBeNullOrEmpty();
        resultat.UrlTelechargement.Should().Be($"/v1/fichiers/{resultat.Identifiant}");
    }

    [Fact]
    public async Task Handle_CasNominal_AppelleChiffrementEtStockage()
    {
        var commande = new CommandeUploadFichier
        {
            Contenu = new MemoryStream(new byte[] { 1, 2, 3 }),
            NomFichier = "fichier.txt",
            TypeMime = "text/plain",
            TailleOctets = 3,
            DureeVieMinutes = 60
        };

        await _gestionnaire.Handle(commande, CancellationToken.None);

        await _chiffrement.Received(1).ChiffrerAsync(
            Arg.Any<Stream>(), Arg.Any<Stream>(),
            "sel-test-base64", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

        await _stockage.Received(1).SauvegarderFichierAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());

        await _stockage.Received(1).SauvegarderMetadonneesAsync(
            Arg.Any<MetadonneesFichier>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AvecDureeVie_MetadonneesContiennentDateExpiration()
    {
        MetadonneesFichier? metaCapturee = null;
        _stockage
            .SauvegarderMetadonneesAsync(Arg.Do<MetadonneesFichier>(m => metaCapturee = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var avant = DateTimeOffset.UtcNow;
        var commande = new CommandeUploadFichier
        {
            Contenu = Stream.Null,
            NomFichier = "image.png",
            TypeMime = "image/png",
            TailleOctets = 512,
            DureeVieMinutes = 45
        };

        await _gestionnaire.Handle(commande, CancellationToken.None);
        var apres = DateTimeOffset.UtcNow;

        metaCapturee.Should().NotBeNull();
        metaCapturee!.DateExpiration.Should().NotBeNull();
        metaCapturee.DateExpiration!.Value.Should().BeCloseTo(avant.AddMinutes(45), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_SansDureeVie_MetadonneesDateExpirationNull()
    {
        MetadonneesFichier? metaCapturee = null;
        _stockage
            .SauvegarderMetadonneesAsync(Arg.Do<MetadonneesFichier>(m => metaCapturee = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var commande = new CommandeUploadFichier
        {
            Contenu = Stream.Null,
            NomFichier = "archive.zip",
            TypeMime = "application/zip",
            TailleOctets = 2048,
            NombreAccesMax = 1
        };

        await _gestionnaire.Handle(commande, CancellationToken.None);

        metaCapturee!.DateExpiration.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MetadonneesConserventInfosFichier()
    {
        MetadonneesFichier? metaCapturee = null;
        _stockage
            .SauvegarderMetadonneesAsync(Arg.Do<MetadonneesFichier>(m => metaCapturee = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var commande = new CommandeUploadFichier
        {
            Contenu = Stream.Null,
            NomFichier = "video.mp4",
            TypeMime = "video/mp4",
            TailleOctets = 100_000,
            DureeVieMinutes = 10,
            NombreAccesMax = 3
        };

        await _gestionnaire.Handle(commande, CancellationToken.None);

        metaCapturee!.NomOriginal.Should().Be("video.mp4");
        metaCapturee.TypeMime.Should().Be("video/mp4");
        metaCapturee.TailleOctets.Should().Be(100_000);
        metaCapturee.NombreAccesMax.Should().Be(3);
        metaCapturee.NombreAccesCourant.Should().Be(0);
        metaCapturee.Sel.Should().Be("sel-test-base64");
        metaCapturee.VersionCle.Should().Be("v1");
    }

    [Fact]
    public async Task Handle_UtiliseVersionCleCourante()
    {
        _gestionnaireCles.ObtenirCleCourante().Returns(("version-speciale", new byte[32]));

        MetadonneesFichier? metaCapturee = null;
        _stockage
            .SauvegarderMetadonneesAsync(Arg.Do<MetadonneesFichier>(m => metaCapturee = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var commande = new CommandeUploadFichier
        {
            Contenu = Stream.Null,
            NomFichier = "test.txt",
            TypeMime = "text/plain",
            TailleOctets = 10,
            DureeVieMinutes = 5
        };

        await _gestionnaire.Handle(commande, CancellationToken.None);

        metaCapturee!.VersionCle.Should().Be("version-speciale");
    }
}
