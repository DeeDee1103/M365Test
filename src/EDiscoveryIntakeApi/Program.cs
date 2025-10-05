using EDiscovery.Shared.Services;
using EDiscoveryIntakeApi.Data;
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

    // Add Entity Framework
    builder.Services.AddDbContext<EDiscoveryDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Data Source=ediscovery.db"));

    // Add AutoRouter service
    builder.Services.AddScoped<IAutoRouterService, AutoRouterService>();

    // Add Compliance Logger
    builder.Services.AddScoped<IComplianceLogger, ComplianceLogger>();

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo 
        { 
            Title = "eDiscovery Intake API", 
            Version = "v1",
            Description = "API for managing eDiscovery matters, jobs, and collection tracking"
        });
    });

    var app = builder.Build();

    // Ensure database is created
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<EDiscoveryDbContext>();
        context.Database.EnsureCreated();
        
        var complianceLogger = scope.ServiceProvider.GetRequiredService<IComplianceLogger>();
        complianceLogger.LogAudit("DatabaseInitialized", new { DatabaseProvider = "SQLite" });
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
