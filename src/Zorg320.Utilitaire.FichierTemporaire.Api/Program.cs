using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Zorg320.Utilitaire.FichierTemporaire.Api;
using Zorg320.Utilitaire.FichierTemporaire.Api.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Chiffrement;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Cles;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Stockage;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

// ─── Configuration de Serilog depuis appsettings ───────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Démarrage de l'API Fichiers Temporaires");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext());

    // ─── Lecture anticipée de la configuration d'authentification ─────────────
    // Nécessaire pour l'enregistrement conditionnel des services et du Swagger.
    var authConfig = builder.Configuration
        .GetSection(ConfigurationAuthentification.Section)
        .Get<ConfigurationAuthentification>() ?? new ConfigurationAuthentification();

    // ─── Configuration typée ──────────────────────────────────────────────────
    builder.Services
        .Configure<ConfigurationStockage>(builder.Configuration.GetSection(ConfigurationStockage.Section))
        .Configure<ConfigurationChiffrement>(builder.Configuration.GetSection(ConfigurationChiffrement.Section))
        .Configure<ConfigurationCles>(builder.Configuration.GetSection(ConfigurationCles.Section))
        .Configure<ConfigurationAuthentification>(builder.Configuration.GetSection(ConfigurationAuthentification.Section));

    // ─── Services applicatifs ─────────────────────────────────────────────────
    builder.Services.AddSingleton<IGestionnaireCles, GestionnaireCles>();
    builder.Services.AddSingleton<IServiceChiffrement, ServiceChiffrement>();
    builder.Services.AddSingleton<IServiceStockage, ServiceStockage>();

    // ─── MediatR ──────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblyContaining<Program>());

    // ─── Authentification OIDC / JWT Bearer (conditionnelle) ──────────────────
    if (authConfig.Activer)
    {
        Log.Information("Authentification OIDC activée — autorité : {Autorite}", authConfig.Autorite);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Récupère automatiquement les clés publiques JWKS depuis {Autorite}/.well-known/openid-configuration
                options.Authority = authConfig.Autorite;
                options.Audience = authConfig.Audience;
                options.RequireHttpsMetadata = authConfig.ExigerHttpsMetadonnees;

                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    // Valide la signature du JWT avec les clés publiques JWKS du provider
                    ValidateIssuerSigningKey = true,
                    // Valide que l'émetteur correspond à l'autorité OIDC
                    ValidateIssuer = true,
                    // Valide que l'audience correspond à la valeur configurée
                    ValidateAudience = true,
                    // Valide les dates d'expiration et de début de validité
                    ValidateLifetime = true,
                    // Tolérance d'horloge réduite à 30 s (défaut : 5 min — trop large)
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // Refuse explicitement les tokens sans signature (alg:none)
                    RequireSignedTokens = true,
                    // Exige la présence du claim d'expiration
                    RequireExpirationTime = true,
                };
            });
    }
    else
    {
        Log.Information("Authentification désactivée — tous les endpoints sont accessibles anonymement");
    }

    // ─── Autorisation ─────────────────────────────────────────────────────────
    // AddAuthorization est toujours requis par FastEndpoints.
    builder.Services.AddAuthorization(options =>
    {
        if (authConfig.Activer && authConfig.ScopesRequis.Length > 0)
        {
            // Politique par défaut : utilisateur authentifié + scopes requis
            var politique = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("scope", authConfig.ScopesRequis)
                .Build();

            options.DefaultPolicy = politique;
            options.AddPolicy("PolitiqueAccesApi", politique);
        }
    });
    // ─── OpenTelemetry ─────────────────────────────────────────────────────────
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation());

    // ─── FastEndpoints + Swagger ───────────────────────────────────────────────

    // Section "Authentification" de la description Swagger, construite dynamiquement
    // à partir de la configuration réelle afin de refléter l'état réel du serveur.
    var descriptionAuthentification = authConfig.Activer
        ? $"""
           ## Authentification

           Authentification OIDC / JWT Bearer activée avec les paramètres suivants et un jeton doit être généré depuis le fournisseur d'identité configuré :
              - **Autorité OIDC** : {authConfig.Autorite}
           """
        : """
          ## Authentification

          L'authentification est **désactivée** sur cet environnement — tous les endpoints sont accessibles anonymement.
          """;

    builder.Services
        .AddFastEndpoints()
        .SwaggerDocument(o =>
        {
            o.AutoTagPathSegmentIndex = 0; // Désactive la génération automatique de tags depuis l'URL
            o.DocumentSettings = s =>
            {
                s.Title = "API Fichiers Temporaires";
                s.Version = "v1";
                s.Description = $$"""
                    ## Vue d'ensemble

                    API de gestion de **fichiers temporaires chiffrés** permettant de déposer un fichier et de le partager via un lien à usage limité.

                    Chaque fichier est chiffré côté serveur avec **AES-256-GCM** (par chunks de 1 Mo) et une clé dérivée par fichier via **HKDF-SHA-256**.
                    Une fois la limite atteinte, le fichier n'est plus accessible et sera supprimé lors du prochain nettoyage automatique.

                    ---

                    ## Cycle de vie d'un fichier

                    1. **Upload** — `POST /v1/fichiers` — déposer le fichier avec ses limites d'expiration.
                    2. **Réception** — l'API retourne un `identifiant` unique et un lien de téléchargement.
                    3. **Téléchargement** — `GET /v1/fichiers/{identifiant}` — récupérer le fichier déchiffré en streaming.
                    4. **Expiration** — le fichier devient inaccessible (HTTP 404) dès que l'une des limites est atteinte.

                    ---

                    ## Limites d'expiration

                    Deux types de limites, combinables :

                    | Paramètre | Type | Description |
                    |---|---|---|
                    | `DureeVieMinutes` | `int` (optionnel) | Durée maximale de disponibilité en minutes à partir de l'upload. |
                    | `NombreAccesMax` | `int` (optionnel) | Nombre maximal de téléchargements autorisés. |

                    **Comportement par défaut si aucune limite n'est fournie :**
                    - `NombreAccesMax` fourni sans `DureeVieMinutes` → durée de vie de **60 minutes** appliquée automatiquement.
                    - Aucune limite fournie → durée de vie de **30 minutes** appliquée automatiquement.

                    ---

                    ## Structure de réponse standard (upload)

                    Toutes les réponses de l'endpoint d'upload suivent cette enveloppe :

                    ```json
                    {
                      "donnees": { "identifiant": "abc123..." },
                      "erreurs": [],
                      "informations": [{ "code": "INF001", "message": "Le fichier a été déposé avec succès." }],
                      "avertissements": [],
                      "liens": [{ "telechargement": "https://…/v1/fichiers/abc123" }]
                    }
                    ```

                    > Le téléchargement retourne directement le fichier binaire (pas d'enveloppe JSON). En cas d'erreur : HTTP 404 simple.

                    ---

                    {{descriptionAuthentification}}

                    ---

                    ## Sécurité & confidentialité

                    - Le contenu des fichiers n'est **jamais stocké en clair** sur le serveur.
                    - Chaque fichier possède sa propre clé de chiffrement dérivée (sel aléatoire unique).
                    - Les fichiers expirés sont supprimés automatiquement par un service de nettoyage en arrière-plan.
                    """;

                if (authConfig.Activer)
                    s.EnableJWTBearerAuth();
            };
        });

    var app = builder.Build();

    // ─── Middleware ───────────────────────────────────────────────────────────
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} répondu {StatusCode} en {Elapsed:0.0000}ms";
    });

    app.UseHttpsRedirection();

    // UseAuthentication doit précéder UseAuthorization.
    if (authConfig.Activer)
        app.UseAuthentication();

    app.UseAuthorization();

    app.UseFastEndpoints(c =>
    {
        c.Endpoints.RoutePrefix = null;
    });

    app.UseSwaggerGen();

    Log.Information("API démarrée et prête à recevoir des requêtes");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "L'application a été arrêtée de manière inattendue");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
