using Microsoft.AspNetCore.Mvc;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Models;

namespace EDiscoveryIntakeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GraphDataConnectController : ControllerBase
{
    private readonly IGraphDataConnectService _gdcService;
    private readonly ILogger<GraphDataConnectController> _logger;

    public GraphDataConnectController(
        IGraphDataConnectService gdcService,
        ILogger<GraphDataConnectController> logger)
    {
        _gdcService = gdcService;
        _logger = logger;
    }

    /// <summary>
    /// Trigger a Graph Data Connect collection via Azure Data Factory
    /// </summary>
    [HttpPost("trigger")]
    [ProducesResponseType(typeof(CollectionResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public async Task<ActionResult<CollectionResult>> TriggerCollection(
        [FromBody] CollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GDC collection trigger requested for custodian: {CustodianEmail}, job type: {JobType}", 
                request.CustodianEmail, request.JobType);

            var result = await _gdcService.TriggerCollectionAsync(request, cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("GDC collection triggered successfully | PipelineRunId: {PipelineRunId}", 
                    result.CollectionMetadata?.GetValueOrDefault("PipelineRunId", "Unknown"));
                return Ok(result);
            }
            else
            {
                _logger.LogError("GDC collection trigger failed: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(new ProblemDetails
                {
                    Title = "GDC Collection Trigger Failed",
                    Detail = result.ErrorMessage,
                    Status = 400
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error triggering GDC collection for custodian: {CustodianEmail}", 
                request.CustodianEmail);
            
            return StatusCode(500, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while triggering the GDC collection",
                Status = 500
            });
        }
    }

    /// <summary>
    /// Get the status of a Graph Data Connect pipeline execution
    /// </summary>
    [HttpGet("status/{pipelineRunId}")]
    [ProducesResponseType(typeof(GdcPipelineStatus), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public async Task<ActionResult<GdcPipelineStatus>> GetPipelineStatus(
        string pipelineRunId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving GDC pipeline status for run: {PipelineRunId}", pipelineRunId);

            var status = await _gdcService.GetPipelineStatusAsync(pipelineRunId, cancellationToken);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GDC pipeline status for run: {PipelineRunId}", pipelineRunId);
            
            return StatusCode(500, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving pipeline status",
                Status = 500
            });
        }
    }

    /// <summary>
    /// Test endpoint to demonstrate GDC stub functionality
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(object), 200)]
    public ActionResult<object> TestGdcStub()
    {
        var testRequest = new CollectionRequest
        {
            CustodianEmail = "test.user@example.com",
            JobType = CollectionJobType.Email,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow
        };

        return Ok(new
        {
            Message = "GDC Stub Module Ready",
            TestRequest = testRequest,
            Features = new[]
            {
                "Azure Data Factory pipeline trigger via Service Bus",
                "Pipeline status monitoring and polling",
                "Configurable retry policies and error handling",
                "Integration with Chain of Custody for audit trails",
                "Support for multiple output formats (Parquet, JSON, CSV)",
                "WORM storage compliance for evidence preservation"
            },
            Configuration = new
            {
                ServiceBusEnabled = !string.IsNullOrEmpty(""), // Would check actual config
                StubMode = true,
                OutputFormat = "Parquet",
                RetentionDays = 2555
            }
        });
    }
}