using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/datasets")]
[Authorize]
[RequireActiveSubscription]
public class DatasetController : ControllerBase
{
    private readonly IDatasetService _datasetService;

    public DatasetController(IDatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        var userId = GetUserId();
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A CSV file is required." });

        await using var stream = file.OpenReadStream();
        var response = await _datasetService.UploadAsync(userId, file.FileName, stream, ct);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetDatasets(CancellationToken ct)
    {
        var userId = GetUserId();
        var datasets = await _datasetService.GetDatasetsAsync(userId, ct);
        return Ok(datasets);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDataset(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var dataset = await _datasetService.GetDatasetAsync(id, userId, ct);
        return Ok(dataset);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDataset(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _datasetService.DeleteDatasetAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(Guid id, [FromBody] GenerateDatasetChartRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _datasetService.GenerateChartAsync(id, userId, request, ct);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return parsed;
    }
}