using System.Text.Json.Serialization;

namespace Zorg320.Utilitaire.FichierTemporaire.Api.Endpoints.Commun;

/// <summary>
/// Structure de réponse standardisée pour toutes les réponses de l'API (hors erreurs HTTP 500).
/// Regroupe les données métier, les erreurs, les informations, les avertissements et les liens utiles.
/// </summary>
/// <typeparam name="T">Type des données retournées dans le champ <c>donnees</c>.</typeparam>
public sealed class ReponseBase<T>
{
    /// <summary>Données métier retournées par l'opération. Null si aucune donnée n'est produite.</summary>
    [JsonPropertyName("donnees")]
    public T? Donnees { get; set; }

    /// <summary>Liste des messages d'erreur métier survenus pendant le traitement.</summary>
    [JsonPropertyName("erreurs")]
    public List<MessageErreur> Erreurs { get; set; } = [];

    /// <summary>Liste des messages d'information décrivant le résultat de l'opération.</summary>
    [JsonPropertyName("informations")]
    public List<MessageInformation> Informations { get; set; } = [];

    /// <summary>Liste des messages d'avertissement non bloquants.</summary>
    [JsonPropertyName("avertissements")]
    public List<MessageAvertissement> Avertissements { get; set; } = [];

    /// <summary>
    /// Liste de liens relatifs à la ressource produite ou concernée.
    /// Chaque entrée est un dictionnaire avec un seul couple clé/valeur (ex. : {"telechargement":"/v1/fichiers/abc"}).
    /// </summary>
    [JsonPropertyName("liens")]
    public List<Dictionary<string, string>> Liens { get; set; } = [];

    /// <summary>
    /// Crée une réponse de succès avec des données et un message d'information.
    /// </summary>
    public static ReponseBase<T> Succes(T donnees, string codeInfo, string messageInfo)
        => new()
        {
            Donnees = donnees,
            Informations = [new MessageInformation(codeInfo, messageInfo)]
        };

    /// <summary>
    /// Crée une réponse d'erreur sans données.
    /// </summary>
    public static ReponseBase<T> Erreur(string codeErreur, string messageErreur)
        => new()
        {
            Erreurs = [new MessageErreur(codeErreur, messageErreur)]
        };

    /// <summary>
    /// Crée une réponse de validation invalide à partir des erreurs FluentValidation.
    /// </summary>
    public static ReponseBase<T> ValidationInvalide(IEnumerable<(string Code, string Message)> erreurs)
    {
        var reponse = new ReponseBase<T>();
        foreach (var (code, message) in erreurs)
            reponse.Erreurs.Add(new MessageErreur(code, message));
        return reponse;
    }
}
