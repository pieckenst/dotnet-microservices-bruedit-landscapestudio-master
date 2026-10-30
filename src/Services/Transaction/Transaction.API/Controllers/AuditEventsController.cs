using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Transaction.API.Data.DTOs.Requests;
using Transaction.API.Data.DTOs.Responses;
using Transaction.API.Repositories.Interfaces;
using Transaction.API.Services;

namespace Transaction.API.Controllers;

[ApiController]
[Route("api/v1/audit-events")]
[Authorize(Policy = "analytics.read")]
public sealed class AuditEventsController(IAuditEventRepository repository) : ControllerBase
{
    private const int ExportLimit = 10_000;

    [HttpGet]
    [ProducesResponseType(typeof(PagedAuditEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedAuditEventsResponse>> Search(
        [FromQuery] AuditEventQuery query, CancellationToken cancellationToken)
    {
        var validation = query.Validate();
        if (validation is not null)
            return BadRequest(new ProblemDetails { Title = "Invalid audit query", Detail = validation });

        var result = await repository.QueryAsync(query, cancellationToken);
        var totalPages = result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)query.PageSize);
        return Ok(new PagedAuditEventsResponse(
            result.Items.Select(AuditEventMapping.ToDto).ToArray(), query.Page, query.PageSize, result.TotalCount, totalPages));
    }

    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery] AuditEventQuery query,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var validation = query.Validate();
        if (validation is not null)
            return BadRequest(new ProblemDetails { Title = "Invalid audit query", Detail = validation });

        var records = await repository.GetForExportAsync(query, ExportLimit + 1, cancellationToken);
        if (records.Count > ExportLimit)
            return BadRequest(new ProblemDetails
            {
                Title = "Export limit exceeded",
                Detail = $"Narrow the filters so the export contains at most {ExportLimit:N0} records."
            });

        var normalizedFormat = format.Trim().ToLowerInvariant();
        var (content, contentType, extension) = normalizedFormat switch
        {
            "csv" => (AuditCsvExporter.Export(records), "text/csv; charset=utf-8", "csv"),
            "pipe" => (AuditCsvExporter.ExportPipe(records), "text/plain; charset=utf-8", "txt"),
            _ => (null, null, null)
        };

        if (content is null)
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported export format",
                Detail = "Supported formats are 'csv' and 'pipe'."
            });

        return File(System.Text.Encoding.UTF8.GetBytes(content), contentType!,
            $"landscape-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}");
    }
}
