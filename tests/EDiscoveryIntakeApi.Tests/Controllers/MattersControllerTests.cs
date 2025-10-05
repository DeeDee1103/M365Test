using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Data;
using EDiscoveryIntakeApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EDiscoveryIntakeApi.Tests.Controllers;

public class MattersControllerTests : IDisposable
{
    private readonly EDiscoveryDbContext _context;
    private readonly Mock<ILogger<MattersController>> _mockLogger;
    private readonly Mock<IComplianceLogger> _mockComplianceLogger;
    private readonly MattersController _controller;

    public MattersControllerTests()
    {
        var options = new DbContextOptionsBuilder<EDiscoveryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new EDiscoveryDbContext(options);
        _mockLogger = new Mock<ILogger<MattersController>>();
        _mockComplianceLogger = new Mock<IComplianceLogger>();
        _mockComplianceLogger.Setup(x => x.CreateCorrelationId()).Returns("test-correlation-id");
        _mockComplianceLogger.Setup(x => x.StartPerformanceTimer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());
        _controller = new MattersController(_context, _mockLogger.Object, _mockComplianceLogger.Object);
    }

    [Fact]
    public async Task GetMatters_ReturnsEmptyList_WhenNoMatters()
    {
        // Act
        var result = await _controller.GetMatters();

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<Matter>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var matters = Assert.IsAssignableFrom<IEnumerable<Matter>>(okResult.Value);
        Assert.Empty(matters);
    }

    [Fact]
    public async Task GetMatters_ReturnsAllMatters_WhenMattersExist()
    {
        // Arrange
        var matters = new[]
        {
            new Matter { Name = "Matter 1", CaseNumber = "CASE-001", CreatedBy = "user1@company.com" },
            new Matter { Name = "Matter 2", CaseNumber = "CASE-002", CreatedBy = "user2@company.com" }
        };

        _context.Matters.AddRange(matters);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMatters();

        // Assert
        var actionResult = Assert.IsType<ActionResult<IEnumerable<Matter>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedMatters = Assert.IsAssignableFrom<IEnumerable<Matter>>(okResult.Value).ToList();
        Assert.Equal(2, returnedMatters.Count);
    }

    [Fact]
    public async Task GetMatter_ReturnsMatter_WhenMatterExists()
    {
        // Arrange
        var matter = new Matter 
        { 
            Name = "Test Matter", 
            CaseNumber = "CASE-001", 
            CreatedBy = "user@company.com" 
        };
        
        _context.Matters.Add(matter);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMatter(matter.Id);

        // Assert
        var actionResult = Assert.IsType<ActionResult<Matter>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedMatter = Assert.IsType<Matter>(okResult.Value);
        Assert.Equal(matter.Id, returnedMatter.Id);
        Assert.Equal(matter.Name, returnedMatter.Name);
    }

    [Fact]
    public async Task GetMatter_ReturnsNotFound_WhenMatterDoesNotExist()
    {
        // Act
        var result = await _controller.GetMatter(999);

        // Assert
        var actionResult = Assert.IsType<ActionResult<Matter>>(result);
        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    [Fact]
    public async Task PostMatter_CreatesMatter_WhenValidMatter()
    {
        // Arrange
        var matter = new Matter 
        { 
            Name = "New Matter", 
            CaseNumber = "CASE-003", 
            CreatedBy = "user@company.com",
            Description = "Test description"
        };

        // Act
        var result = await _controller.PostMatter(matter);

        // Assert
        var actionResult = Assert.IsType<ActionResult<Matter>>(result);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var createdMatter = Assert.IsType<Matter>(createdResult.Value);
        
        Assert.Equal(matter.Name, createdMatter.Name);
        Assert.Equal(matter.CaseNumber, createdMatter.CaseNumber);
        Assert.True(createdMatter.Id > 0);
        
        // Verify it was saved to database
        var savedMatter = await _context.Matters.FindAsync(createdMatter.Id);
        Assert.NotNull(savedMatter);
    }

    [Fact]
    public async Task DeleteMatter_RemovesMatter_WhenMatterExists()
    {
        // Arrange
        var matter = new Matter 
        { 
            Name = "Matter to Delete", 
            CaseNumber = "CASE-DELETE", 
            CreatedBy = "user@company.com" 
        };
        
        _context.Matters.Add(matter);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteMatter(matter.Id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        
        // Verify it was removed from database
        var deletedMatter = await _context.Matters.FindAsync(matter.Id);
        Assert.Null(deletedMatter);
    }

    [Fact]
    public async Task DeleteMatter_ReturnsNotFound_WhenMatterDoesNotExist()
    {
        // Act
        var result = await _controller.DeleteMatter(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}