using EDiscovery.Shared.Services;
using HybridGraphCollectorWorker;
using HybridGraphCollectorWorker.Services;
using Serilog;

// Configure Serilog from configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .CreateLogger();

try
{
    Log.Information("Starting Hybrid Graph Collector Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Add services
    builder.Services.AddHostedService<Worker>();

    // Add HTTP client for API communication
    builder.Services.AddHttpClient<IEDiscoveryApiClient, EDiscoveryApiClient>();

    // Add application services as Singleton to match IHostedService lifetime
    builder.Services.AddSingleton<IGraphCollectorService, GraphCollectorService>();
    builder.Services.AddSingleton<IAutoRouterService, AutoRouterService>();
    builder.Services.AddSingleton<IEDiscoveryApiClient, EDiscoveryApiClient>();
    builder.Services.AddSingleton<IComplianceLogger, ComplianceLogger>();

    var host = builder.Build();
    
    // Log startup completion
    var complianceLogger = host.Services.GetRequiredService<IComplianceLogger>();
    complianceLogger.LogAudit("WorkerServiceStarted", new { 
        Version = "1.0.0",
        Environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"
    });

    Log.Information("Hybrid Graph Collector Worker started successfully");
    
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Hybrid Graph Collector Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
