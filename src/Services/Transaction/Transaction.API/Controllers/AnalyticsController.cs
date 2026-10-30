using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Transaction.API.Data.DTOs.Responses;
using Transaction.API.Repositories.Interfaces;

namespace Transaction.API.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(Policy = "analytics.read")]
public sealed class AnalyticsController(IAuditEventRepository repository) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetSummary(
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
    {
        var to = NormalizeUtc(toUtc ?? DateTime.UtcNow);
        var from = NormalizeUtc(fromUtc ?? to.AddDays(-30));
        if (from > to)
            return BadRequest(new ProblemDetails { Title = "Invalid date range", Detail = "fromUtc must be earlier than or equal to toUtc." });
        if (to - from > TimeSpan.FromDays(366))
            return BadRequest(new ProblemDetails { Title = "Date range too wide", Detail = "Analytics summaries are limited to 366 days." });

        var total = await repository.CountAsync(from, to, cancellationToken);
        var services = await repository.CountByServiceAsync(from, to, cancellationToken);
        var actions = await repository.CountByActionAsync(from, to, cancellationToken);
        return Ok(new AnalyticsSummaryDto(from, to, total,
            services.Select(x => new AnalyticsCountDto(x.Name, x.Count)).ToArray(),
            actions.Select(x => new AnalyticsCountDto(x.Name, x.Count)).ToArray()));
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
