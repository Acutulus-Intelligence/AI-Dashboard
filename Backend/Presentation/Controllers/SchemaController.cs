using Application.DTos.Response;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[ApiController]
[Route("api/connections/{connectionId:guid}/tables")]
[Authorize]
public class SchemaController : ControllerBase
{
    private readonly ISchemaInspector _schemaInspector;

    public SchemaController(ISchemaInspector schemaInspector)
    {
        _schemaInspector = schemaInspector;
    }

    [HttpGet]
    public async Task<IActionResult> GetTables(Guid connectionId, CancellationToken ct)
    {
        var userId = GetUserId();
        var schema = await _schemaInspector.GetSchemaAsync(connectionId, userId, ct);

        var tables = schema.Select(t => new TableInfoResponse(
            t.TableName,
            t.Columns.Select(c => new ColumnInfoResponse(c.ColumnName, c.DataType, c.IsNullable)).ToList()
        )).ToList();

        return Ok(tables);
    }

    [HttpGet("{tableName}")]
    public async Task<IActionResult> GetTableSchema(Guid connectionId, string tableName, CancellationToken ct)
    {
        var userId = GetUserId();
        var schema = await _schemaInspector.GetTableSchemaAsync(connectionId, userId, tableName, ct);

        var response = new TableInfoResponse(
            schema.TableName,
            schema.Columns.Select(c => new ColumnInfoResponse(c.ColumnName, c.DataType, c.IsNullable)).ToList()
        );

        return Ok(response);
    }

    [HttpGet("{tableName}/preview")]
    public async Task<IActionResult> PreviewTable(Guid connectionId, string tableName, [FromQuery] int rows = 5, CancellationToken ct = default)
    {
        var userId = GetUserId();

        var schema = await _schemaInspector.GetTableSchemaAsync(connectionId, userId, tableName, ct);
        var data = await _schemaInspector.PreviewTableAsync(connectionId, userId, tableName, rows, ct);

        var response = new TablePreviewResponse(
            tableName,
            schema.Columns.Select(c => new ColumnInfoResponse(c.ColumnName, c.DataType, c.IsNullable)).ToList(),
            data
        );

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
