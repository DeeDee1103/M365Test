using EDiscovery.Shared.Services;
using EDiscovery.Shared.Configuration;
using EDiscovery.Shared.Models;
using EDiscovery.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

// Configure Serilog from configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .CreateLogger();

try
{
    Log.Information("Starting eDiscovery Intake API");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();

    // Add Entity Framework with shared context factory for multi-threading
    builder.Services.AddDbContextFactory<EDiscoveryDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Data Source=ediscovery.db"));

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

    // Add concurrent job management services
    builder.Services.AddScoped<IConcurrentJobManager, ConcurrentJobManager>();

    // Add AutoRouter service
    builder.Services.AddScoped<IAutoRouterService, AutoRouterService>();

    // Add Graph Data Connect service
    builder.Services.AddScoped<IGraphDataConnectService, GraphDataConnectService>();

    // Add Delta Query service
    builder.Services.AddScoped<IDeltaQueryService, DeltaQueryService>();

    // Add Chain of Custody service
    builder.Services.AddScoped<IChainOfCustodyService, ChainOfCustodyService>();

    // Add Compliance Logger
    builder.Services.AddScoped<IComplianceLogger, ComplianceLogger>();

    // TODO: Add Observability service after resolving compilation issues
    // builder.Services.AddSingleton<IObservabilityService, ObservabilityService>();

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo 
        { 
            Title = "eDiscovery Intake API", 
            Version = "v1",
            Description = "API for managing eDiscovery matters, jobs, and collection tracking with multi-user concurrent processing"
        });
    });

    var app = builder.Build();

    // Ensure database is created
    using (var scope = app.Services.CreateScope())
    {
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EDiscoveryDbContext>>();
        using var context = dbContextFactory.CreateDbContext();
        context.Database.EnsureCreated();
        
        var complianceLogger = scope.ServiceProvider.GetRequiredService<IComplianceLogger>();
        complianceLogger.LogAudit("DatabaseInitialized", new { DatabaseProvider = "SQLite", ConcurrentProcessingEnabled = true });
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "eDiscovery Intake API v1");
        });
    }

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("eDiscovery Intake API started successfully");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "eDiscovery Intake API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for testing
public partial class Program { }
