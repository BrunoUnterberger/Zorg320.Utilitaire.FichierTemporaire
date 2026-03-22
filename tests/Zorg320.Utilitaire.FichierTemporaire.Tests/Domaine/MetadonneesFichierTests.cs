using FluentAssertions;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Domaine.Entites;

namespace Zorg320.Utilitaire.FichierTemporaire.Tests.Domaine;

public sealed class MetadonneesFichierTests
{
    private static MetadonneesFichier CreerMetadonnees(
        DateTimeOffset? dateExpiration = null,
        int? nombreAccesMax = null,
        int nombreAccesCourant = 0)
        => new()
        {
            Identifiant = "test-id",
            NomOriginal = "fichier.txt",
            TypeMime = "text/plain",
            DateCreation = DateTimeOffset.UtcNow,
            DateExpiration = dateExpiration,
            NombreAccesMax = nombreAccesMax,
            NombreAccesCourant = nombreAccesCourant,
            TailleOctets = 100,
            Sel = "sel-base64",
            VersionCle = "v1"
        };

    // ---- EstExpire ----

    [Fact]
    public void EstExpire_SansLimite_RetourneFaux()
    {
        var meta = CreerMetadonnees();

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void EstExpire_DateExpirationFuture_RetourneFaux()
    {
        var meta = CreerMetadonnees(dateExpiration: DateTimeOffset.UtcNow.AddMinutes(10));

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void EstExpire_DateExpirationAtteinte_RetourneVrai()
    {
        var meta = CreerMetadonnees(dateExpiration: DateTimeOffset.UtcNow.AddMinutes(-1));

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstExpire_DateExpirationExacte_RetourneVrai()
    {
        var maintenant = DateTimeOffset.UtcNow;
        var meta = CreerMetadonnees(dateExpiration: maintenant);

        meta.EstExpire(maintenant).Should().BeTrue();
    }

    [Fact]
    public void EstExpire_NombreAccesMaxNonAtteint_RetourneFaux()
    {
        var meta = CreerMetadonnees(nombreAccesMax: 5, nombreAccesCourant: 3);

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void EstExpire_NombreAccesMaxAtteint_RetourneVrai()
    {
        var meta = CreerMetadonnees(nombreAccesMax: 3, nombreAccesCourant: 3);

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstExpire_NombreAccesMaxDepasse_RetourneVrai()
    {
        var meta = CreerMetadonnees(nombreAccesMax: 3, nombreAccesCourant: 5);

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstExpire_DateExpireeEtAccesRestants_RetourneVrai()
    {
        var meta = CreerMetadonnees(
            dateExpiration: DateTimeOffset.UtcNow.AddMinutes(-5),
            nombreAccesMax: 10,
            nombreAccesCourant: 1);

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void EstExpire_DateValideEtAccesAtteint_RetourneVrai()
    {
        var meta = CreerMetadonnees(
            dateExpiration: DateTimeOffset.UtcNow.AddHours(1),
            nombreAccesMax: 2,
            nombreAccesCourant: 2);

        meta.EstExpire(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    // ---- AvecAccesIncremente ----

    [Fact]
    public void AvecAccesIncremente_IncrementeLeCompteur()
    {
        var meta = CreerMetadonnees(nombreAccesCourant: 3);

        var resultat = meta.AvecAccesIncremente();

        resultat.NombreAccesCourant.Should().Be(4);
    }

    [Fact]
    public void AvecAccesIncremente_NeCreesPasUneNouvelleInstanceAvecDestructionAncienne()
    {
        var meta = CreerMetadonnees(nombreAccesCourant: 0);

        var resultat = meta.AvecAccesIncremente();

        resultat.Should().NotBeSameAs(meta);
        meta.NombreAccesCourant.Should().Be(0); // l'original est inchangé
    }

    [Fact]
    public void AvecAccesIncremente_ConserveToutesLesAutresProprietes()
    {
        var dateExp = DateTimeOffset.UtcNow.AddHours(1);
        var meta = CreerMetadonnees(dateExpiration: dateExp, nombreAccesMax: 5, nombreAccesCourant: 2);

        var resultat = meta.AvecAccesIncremente();

        resultat.Identifiant.Should().Be(meta.Identifiant);
        resultat.NomOriginal.Should().Be(meta.NomOriginal);
        resultat.TypeMime.Should().Be(meta.TypeMime);
        resultat.DateExpiration.Should().Be(dateExp);
        resultat.NombreAccesMax.Should().Be(5);
        resultat.NombreAccesCourant.Should().Be(3);
    }
}
