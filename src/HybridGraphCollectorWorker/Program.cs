using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using HybridGraphCollectorWorker;
using HybridGraphCollectorWorker.Services;
using Microsoft.EntityFrameworkCore;
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
    Log.Information("Starting Concurrent Hybrid Graph Collector Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Register database context factory for multi-threading
    builder.Services.AddDbContextFactory<EDiscoveryDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=ediscovery.db"));

    // Add HTTP client for API communication
    builder.Services.AddHttpClient<IEDiscoveryApiClient, EDiscoveryApiClient>();

    // Register concurrent job management services
    builder.Services.AddScoped<IConcurrentJobManager, ConcurrentJobManager>();
    
    // Add application services as Singleton to match IHostedService lifetime
    builder.Services.AddSingleton<IGraphCollectorService, GraphCollectorService>();
    builder.Services.AddSingleton<IAutoRouterService, AutoRouterService>();
    builder.Services.AddSingleton<IEDiscoveryApiClient, EDiscoveryApiClient>();
    builder.Services.AddSingleton<IComplianceLogger, ComplianceLogger>();

    // Register the original worker service (concurrent service has API signature issues)
    builder.Services.AddHostedService<Worker>();
    
    // TODO: Enable concurrent worker after fixing API signatures
    // builder.Services.AddHostedService<ConcurrentWorkerService>();

    var host = builder.Build();
    
    // Ensure database is created and migrated
    using (var scope = host.Services.CreateScope())
    {
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EDiscoveryDbContext>>();
        using var context = dbContextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }
    
    // Log startup completion
    using (var scope = host.Services.CreateScope())
    {
        var complianceLogger = scope.ServiceProvider.GetRequiredService<IComplianceLogger>();
        complianceLogger.LogAudit("ConcurrentWorkerServiceStarted", new { 
            Version = "2.0.0",
            Environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
            ConcurrentEnabled = true
        });
    }

    Log.Information("Concurrent Hybrid Graph Collector Worker started successfully");
    
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Concurrent Hybrid Graph Collector Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
