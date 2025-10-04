using EDiscovery.Shared.Services;
using HybridGraphCollectorWorker;
using HybridGraphCollectorWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddHostedService<Worker>();

// Add HTTP client for API communication
builder.Services.AddHttpClient<IEDiscoveryApiClient, EDiscoveryApiClient>();

// Add application services
builder.Services.AddScoped<IGraphCollectorService, GraphCollectorService>();
builder.Services.AddScoped<IAutoRouterService, AutoRouterService>();
builder.Services.AddScoped<IEDiscoveryApiClient, EDiscoveryApiClient>();

var host = builder.Build();
host.Run();
