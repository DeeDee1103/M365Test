using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Configuration;
using EDiscovery.Shared.Data;
using HybridGraphCollectorWorker;
using HybridGraphCollectorWorker.Services;
using HybridGraphCollectorWorker.Workers;
using HybridGraphCollectorWorker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Azure.Identity;

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

    // Check for CLI mode arguments
    if (args.Length > 0 && args[0] == "--reconcile")
    {
        await RunReconcileCLI(args);
        return;
    }

    var builder = Host.CreateApplicationBuilder(args);

    // Add Azure Key Vault configuration (if enabled)
    var keyVaultUrl = builder.Configuration["AzureKeyVault:VaultUrl"];
    var useKeyVault = builder.Configuration.GetValue<bool>("AzureKeyVault:UseKeyVault");
    
    if (useKeyVault && !string.IsNullOrEmpty(keyVaultUrl))
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true,
            ExcludeVisualStudioCodeCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeEnvironmentCredential = false
        });
        
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), credential);
        Log.Information("Azure Key Vault configuration added: {VaultUrl}", keyVaultUrl);
    }

    // Add Serilog
    builder.Services.AddSerilog();

    // Register database context factory for multi-threading
    builder.Services.AddDbContextFactory<EDiscoveryDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=ediscovery.db"));

    // Configure AutoRouter options
    builder.Services.Configure<AutoRouterOptions>(
        builder.Configuration.GetSection(AutoRouterOptions.SectionName));

    // Configure DeltaQuery options
    builder.Services.Configure<EDiscovery.Shared.Configuration.DeltaQueryOptions>(
        builder.Configuration.GetSection(EDiscovery.Shared.Configuration.DeltaQueryOptions.SectionName));

    // Configure ChainOfCustody options
    builder.Services.Configure<ChainOfCustodyOptions>(
        builder.Configuration.GetSection(ChainOfCustodyOptions.SectionName));

    // Configure GraphDataConnect options
    builder.Services.Configure<GdcOptions>(
        builder.Configuration.GetSection(GdcOptions.SectionName));

    // Configure GDC Binary Fetch options
    builder.Services.Configure<HybridGraphCollectorWorker.Models.GdcBinaryFetchOptions>(
        builder.Configuration.GetSection(HybridGraphCollectorWorker.Models.GdcBinaryFetchOptions.SectionName));

    // Configure Reconcile options
    builder.Services.Configure<ReconcileOptions>(
        builder.Configuration.GetSection("Reconcile"));

    // Add HTTP client for API communication
    builder.Services.AddHttpClient<IEDiscoveryApiClient, EDiscoveryApiClient>();

    // Register concurrent job management services
    builder.Services.AddScoped<IConcurrentJobManager, ConcurrentJobManager>();
    
    // Add security services
    builder.Services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
    builder.Services.AddScoped<ISecureConfigurationService, SecureConfigurationService>();
    
    // Add application services as Singleton to match IHostedService lifetime
    builder.Services.AddSingleton<IGraphCollectorService, GraphCollectorService>();
    builder.Services.AddSingleton<IAutoRouterService, AutoRouterService>();
    builder.Services.AddSingleton<IGraphDataConnectService, GraphDataConnectService>();
    builder.Services.AddScoped<IDeltaQueryService, DeltaQueryService>(); // Scoped due to DbContext dependency
    builder.Services.AddScoped<IChainOfCustodyService, ChainOfCustodyService>(); // Scoped due to DbContext dependency
    builder.Services.AddSingleton<IEDiscoveryApiClient, EDiscoveryApiClient>();
    builder.Services.AddSingleton<IComplianceLogger, ComplianceLogger>();

    // Add Delta Services
    builder.Services.AddSingleton<IFileDeltaCursorStorage, FileDeltaCursorStorage>();
    builder.Services.AddScoped<IOneDriveDeltaEnumerator, OneDriveDeltaEnumerator>();
    builder.Services.AddScoped<IMailDeltaEnumerator, MailDeltaEnumerator>();

    // Add GDC Binary Fetch services
    builder.Services.AddScoped<HybridGraphCollectorWorker.Services.GdcBinaryFetcher>();

    // Add Reconciliation services
    builder.Services.AddScoped<Reconciler>();

    // Add temporary ObservabilityHelper for structured logging
    builder.Services.AddSingleton<ObservabilityHelper>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<ObservabilityHelper>>();
        var complianceLogger = provider.GetRequiredService<IComplianceLogger>();
        return new ObservabilityHelper(logger, complianceLogger);
    });

    // TODO: Add Observability service after resolving compilation issues
    // builder.Services.AddSingleton<IObservabilityService, ObservabilityService>();

    // Register the original worker service (concurrent service has API signature issues)
    builder.Services.AddHostedService<Worker>();
    
    // Register GDC Fetch Worker for post-GDC binary collection
    builder.Services.AddHostedService<HybridGraphCollectorWorker.Workers.GdcFetchWorker>();
    
    // Register Reconciliation Worker for validation operations
    builder.Services.AddHostedService<ReconcileWorker>();
    
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

static async Task RunReconcileCLI(string[] args)
{
    try
    {
        // Parse CLI arguments
        if (args.Length < 5)
        {
            Console.WriteLine("Usage: --reconcile <custodian> <jobId> <sourcePath> <collectedPath> [--dry-run]");
            Console.WriteLine("Example: --reconcile john.doe@company.com 123 ./source-manifest.csv ./collected-manifest.csv --dry-run");
            return;
        }

        var custodian = args[1];
        var jobId = args[2];
        var sourcePath = args[3];
        var collectedPath = args[4];
        var dryRun = args.Contains("--dry-run");

        Console.WriteLine($"Starting reconciliation:");
        Console.WriteLine($"  Custodian: {custodian}");
        Console.WriteLine($"  Job ID: {jobId}");
        Console.WriteLine($"  Source: {sourcePath}");
        Console.WriteLine($"  Collected: {collectedPath}");
        Console.WriteLine($"  Dry Run: {dryRun}");

        // Build minimal DI container for CLI execution
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Configure options
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
            
        services.Configure<ReconcileOptions>(configuration.GetSection("Reconcile"));
        services.AddScoped<Reconciler>();
        services.AddScoped<IComplianceLogger, ComplianceLogger>();

        var serviceProvider = services.BuildServiceProvider();
        var reconciler = serviceProvider.GetRequiredService<Reconciler>();

        // Execute reconciliation
        var result = await reconciler.ReconcileAsync(
            custodian,
            jobId,
            sourcePath, 
            collectedPath);

        Console.WriteLine($"Reconciliation completed: {(result.OverallPassed ? "PASS" : "FAIL")}");
        Console.WriteLine($"  Source items: {result.SourceCount}");
        Console.WriteLine($"  Collected items: {result.CollectedCount}");
        Console.WriteLine($"  Missing items: {result.MissedCount}");
        Console.WriteLine($"  Extra items: {result.ExtraCount}");
        Console.WriteLine($"  Hash mismatches: {result.HashMismatchCount}");
        Console.WriteLine($"  Size delta: {result.SizeDeltaBytes:N0} bytes");
        
        if (!string.IsNullOrEmpty(result.ReportPath))
        {
            Console.WriteLine($"  Report saved: {result.ReportPath}");
        }

        Environment.Exit(result.OverallPassed ? 0 : 1);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"CLI Error: {ex.Message}");
        Log.Error(ex, "CLI reconciliation failed");
        Environment.Exit(1);
    }
}
