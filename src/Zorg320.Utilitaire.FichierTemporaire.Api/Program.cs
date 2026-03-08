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
    builder.Services
        .AddFastEndpoints()
        .SwaggerDocument(o =>
        {
            o.DocumentSettings = s =>
            {
                s.Title = "API Fichiers Temporaires";
                s.Version = "v1";
                s.Description = "API de gestion de fichiers temporaires chiffrés avec expiration configurable.";

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
