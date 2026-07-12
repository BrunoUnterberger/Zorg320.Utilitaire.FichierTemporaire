using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Application.Interfaces;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Configuration;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Cles;
using Zorg320.Utilitaire.FichierTemporaire.Noyau.Infrastructure.Stockage;
using Zorg320.Utilitaire.FichierTemporaire.Worker;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Démarrage du Worker de nettoyage et de rotation des clés");

    var builder = Host.CreateApplicationBuilder(args);

    // ─── Serilog ──────────────────────────────────────────────────────────────
    builder.Services.AddSerilog((services, config) =>
        config.ReadFrom.Configuration(builder.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext());

    // ─── Configuration typée ──────────────────────────────────────────────────
    builder.Services
        .Configure<ConfigurationStockage>(builder.Configuration.GetSection(ConfigurationStockage.Section))
        .Configure<ConfigurationNettoyage>(builder.Configuration.GetSection(ConfigurationNettoyage.Section))
        .Configure<ConfigurationCles>(builder.Configuration.GetSection(ConfigurationCles.Section));

    // ─── Data Protection (chiffrement du trousseau de clés au repos) ─────────
    // Stocké sous {CheminRacine}/dataprotection-keys — même volume que les fichiers,
    // une seule variable Stockage__CheminRacine suffit à tout configurer en prod.
    var configStockage = builder.Configuration
        .GetSection(ConfigurationStockage.Section)
        .Get<ConfigurationStockage>();
    var dirDataProtection = Path.Combine(
        configStockage?.CheminRacine ?? "data",
        "dataprotection-keys");
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dirDataProtection))
        .SetApplicationName("Zorg320.FichierTemporaire");

    // ─── Services ─────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<IGestionnaireCles, GestionnaireCles>();
    builder.Services.AddSingleton<IServiceStockage, ServiceStockage>();

    // ─── OpenTelemetry ─────────────────────────────────────────────────────────
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddRuntimeInstrumentation());

    // ─── Workers ──────────────────────────────────────────────────────────────
    builder.Services.AddHostedService<WorkerNettoyage>();
    builder.Services.AddHostedService<WorkerRotationCles>();

    var host = builder.Build();

    Log.Information("Worker prêt — démarrage des boucles de nettoyage et de rotation des clés");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Le worker a été arrêté de manière inattendue");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
