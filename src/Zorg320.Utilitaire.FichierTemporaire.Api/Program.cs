using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Chiffrement;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Cles;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Stockage;

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

    // ─── Configuration typée ──────────────────────────────────────────────────
    builder.Services
        .Configure<ConfigurationStockage>(builder.Configuration.GetSection(ConfigurationStockage.Section))
        .Configure<ConfigurationChiffrement>(builder.Configuration.GetSection(ConfigurationChiffrement.Section))
        .Configure<ConfigurationCles>(builder.Configuration.GetSection(ConfigurationCles.Section));

    // ─── Services applicatifs ─────────────────────────────────────────────────
    builder.Services.AddSingleton<IGestionnaireCles, GestionnaireCles>();
    builder.Services.AddSingleton<IServiceChiffrement, ServiceChiffrement>();
    builder.Services.AddSingleton<IServiceStockage, ServiceStockage>();

    // ─── MediatR ──────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblyContaining<Program>());

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
            };
        });

    var app = builder.Build();

    // ─── Middleware ───────────────────────────────────────────────────────────
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} répondu {StatusCode} en {Elapsed:0.0000}ms";
    });

    app.UseHttpsRedirection();

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
