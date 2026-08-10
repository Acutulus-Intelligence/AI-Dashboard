using Application.DTos.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Middleware;

namespace Presentation.Controllers;

[ApiController]
[Route("api/collections")]
[Authorize]
[RequireActiveSubscription]
public class CollectionController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCollectionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _collectionService.CreateAsync(userId, request, ct);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetCollections(CancellationToken ct)
    {
        var userId = GetUserId();
        var collections = await _collectionService.GetCollectionsAsync(userId, ct);
        return Ok(collections);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCollection(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var collection = await _collectionService.GetCollectionAsync(id, userId, ct);
        return Ok(collection);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCollectionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _collectionService.UpdateAsync(id, userId, request, ct);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCollection(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        await _collectionService.DeleteCollectionAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/files")]
    public async Task<IActionResult> UploadFile(Guid id, [FromForm] IFormFile file, CancellationToken ct)
    {
        var userId = GetUserId();
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A CSV or XLSX file is required." });

        await using var stream = file.OpenReadStream();
        var response = await _collectionService.UploadFileAsync(id, userId, file.FileName, stream, ct);
        return Ok(response);
    }

    [HttpGet("{id:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> GetFile(Guid id, Guid fileId, CancellationToken ct)
    {
        var userId = GetUserId();
        var file = await _collectionService.GetFileAsync(id, fileId, userId, ct);
        return Ok(file);
    }

    [HttpDelete("{id:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> DeleteFile(Guid id, Guid fileId, CancellationToken ct)
    {
        var userId = GetUserId();
        await _collectionService.DeleteFileAsync(id, fileId, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/files/{fileId:guid}/generate")]
    public async Task<IActionResult> Generate(
        Guid id, Guid fileId, [FromBody] GenerateCollectionChartRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var response = await _collectionService.GenerateChartAsync(id, fileId, userId, request, ct);
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