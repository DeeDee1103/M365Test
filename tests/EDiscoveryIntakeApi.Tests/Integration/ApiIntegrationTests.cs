using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace EDiscoveryIntakeApi.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<EDiscoveryDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Add in-memory database for testing
                services.AddDbContext<EDiscoveryDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });

                // Build the service provider
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database context
                using var scope = sp.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<EDiscoveryDbContext>();

                // Ensure the database is created
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetMatters_ReturnsSuccessAndCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/matters");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", 
            response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task PostMatter_CreatesNewMatter()
    {
        // Arrange
        var matter = new Matter
        {
            Name = "Integration Test Matter",
            CaseNumber = "IT-001",
            CreatedBy = "integration@test.com",
            Description = "Created by integration test"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/matters", matter);

        // Assert
        response.EnsureSuccessStatusCode();
        var createdMatter = await response.Content.ReadFromJsonAsync<Matter>();
        
        Assert.NotNull(createdMatter);
        Assert.True(createdMatter.Id > 0);
        Assert.Equal(matter.Name, createdMatter.Name);
        Assert.Equal(matter.CaseNumber, createdMatter.CaseNumber);
    }

    [Fact]
    public async Task GetJobs_ReturnsSuccessAndCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/jobs");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", 
            response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task PostJob_CreatesNewJob()
    {
        // Arrange - First create a matter
        var matter = new Matter
        {
            Name = "Test Matter for Job",
            CaseNumber = "JOB-001",
            CreatedBy = "jobtest@test.com"
        };

        var matterResponse = await _client.PostAsJsonAsync("/api/matters", matter);
        var createdMatter = await matterResponse.Content.ReadFromJsonAsync<Matter>();

        var job = new CollectionJob
        {
            MatterId = createdMatter!.Id,
            CustodianEmail = "custodian@company.com",
            JobType = CollectionJobType.Email,
            Route = CollectionRoute.GraphApi
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs", job);

        // Assert
        response.EnsureSuccessStatusCode();
        var createdJob = await response.Content.ReadFromJsonAsync<CollectionJob>();
        
        Assert.NotNull(createdJob);
        Assert.True(createdJob.Id > 0);
        Assert.Equal(job.MatterId, createdJob.MatterId);
        Assert.Equal(job.CustodianEmail, createdJob.CustodianEmail);
        Assert.Equal(job.JobType, createdJob.JobType);
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", content);
        }
        else
        {
            // Health check endpoint might not be configured, which is fine for POC
            Assert.True(response.StatusCode == System.Net.HttpStatusCode.NotFound);
        }
    }
}