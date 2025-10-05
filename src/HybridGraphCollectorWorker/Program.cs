using EDiscovery.Shared.Services;
using HybridGraphCollectorWorker;
using HybridGraphCollectorWorker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddHostedService<Worker>();

// Add HTTP client for API communication
builder.Services.AddHttpClient<IEDiscoveryApiClient, EDiscoveryApiClient>();

// Add application services as Singleton to match IHostedService lifetime
builder.Services.AddSingleton<IGraphCollectorService, GraphCollectorService>();
builder.Services.AddSingleton<IAutoRouterService, AutoRouterService>();
builder.Services.AddSingleton<IEDiscoveryApiClient, EDiscoveryApiClient>();

var host = builder.Build();
host.Run();
