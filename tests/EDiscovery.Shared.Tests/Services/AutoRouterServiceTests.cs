using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EDiscovery.Shared.Tests.Services;

public class AutoRouterServiceTests
{
    private readonly Mock<ILogger<AutoRouterService>> _mockLogger;
    private readonly Mock<IComplianceLogger> _mockComplianceLogger;
    private readonly Mock<IOptions<AutoRouterOptions>> _mockOptions;
    private readonly AutoRouterService _autoRouterService;

    public AutoRouterServiceTests()
    {
        _mockLogger = new Mock<ILogger<AutoRouterService>>();
        _mockComplianceLogger = new Mock<IComplianceLogger>();
        _mockComplianceLogger.Setup(x => x.CreateCorrelationId()).Returns("test-correlation-id");
        _mockComplianceLogger.Setup(x => x.StartPerformanceTimer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());

        // Configure default test options
        var options = new AutoRouterOptions
        {
            GraphApiThresholds = new GraphApiThresholds
            {
                MaxSizeBytes = 107374182400L, // 100GB
                MaxItemCount = 500_000
            },
            RoutingConfidence = new RoutingConfidence
            {
                HighConfidence = 90.0,
                MediumConfidence = 80.0,
                LowConfidence = 70.0
            }
        };
        
        _mockOptions = new Mock<IOptions<AutoRouterOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(options);
        
        _autoRouterService = new AutoRouterService(_mockLogger.Object, _mockComplianceLogger.Object, _mockOptions.Object);
    }

    [Fact]
    public async Task DetermineOptimalRouteAsync_SmallCustodian_ReturnsGraphApi()
    {
        // Arrange
        var request = new CollectionRequest
        {
            CustodianEmail = "small.user@company.com",
            JobType = CollectionJobType.Email
        };

        // Act
        var result = await _autoRouterService.DetermineOptimalRouteAsync(request);

        // Assert
        Assert.Equal(CollectionRoute.GraphApi, result.RecommendedRoute);
        Assert.True(result.ConfidenceScore > 80);
        Assert.Contains("Graph API", result.Reason);
    }

    [Fact]
    public async Task DetermineOptimalRouteAsync_LargeCustodian_ReturnsGraphDataConnect()
    {
        // Arrange
        var request = new CollectionRequest
        {
            CustodianEmail = "large.user@company.com",
            JobType = CollectionJobType.Email
        };

        // Act
        var result = await _autoRouterService.DetermineOptimalRouteAsync(request);

        // Assert
        Assert.Equal(CollectionRoute.GraphDataConnect, result.RecommendedRoute);
        Assert.True(result.ConfidenceScore > 70);
        Assert.Contains("Graph Data Connect", result.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-email")]
    public async Task DetermineOptimalRouteAsync_InvalidCustodian_ThrowsArgumentException(string invalidCustodian)
    {
        // Arrange
        var request = new CollectionRequest
        {
            CustodianEmail = invalidCustodian,
            JobType = CollectionJobType.Email
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _autoRouterService.DetermineOptimalRouteAsync(request));
    }

    [Fact]
    public async Task DetermineOptimalRouteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _autoRouterService.DetermineOptimalRouteAsync(null!));
    }

    [Fact]
    public async Task DetermineOptimalRouteAsync_ValidRequest_ReturnsValidDecision()
    {
        // Arrange
        var request = new CollectionRequest
        {
            CustodianEmail = "test.user@company.com",
            JobType = CollectionJobType.Email
        };

        // Act
        var result = await _autoRouterService.DetermineOptimalRouteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(Enum.IsDefined(typeof(CollectionRoute), result.RecommendedRoute));
        Assert.True(result.ConfidenceScore >= 0 && result.ConfidenceScore <= 100);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task GetCurrentQuotaAsync_ReturnsValidQuota()
    {
        // Act
        var result = await _autoRouterService.GetCurrentQuotaAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.UsedBytes >= 0);
        Assert.True(result.LimitBytes > 0);
        Assert.True(result.UsedItems >= 0);
        Assert.True(result.LimitItems > 0);
    }
}