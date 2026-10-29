using EventBus.Messages.Events;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Contracts;
using Project.Application.DTOs;
using Project.Application.Features.Estimates;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Persistence;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/estimates")]
[Authorize]
public sealed class EstimatesController(
    IMediator mediator,
    IEstimateRepository estimates,
    IRepositoryBase<LandscapeProject> projects,
    IPublishEndpoint publishEndpoint,
    ILogger<EstimatesController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<PagedResponse<EstimateDto>>> Get(
        [FromQuery] Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetEstimatesQuery(projectId, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<EstimateDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var estimate = await mediator.Send(new GetEstimateByIdQuery(id), cancellationToken);
        return estimate is null ? NotFound() : Ok(estimate);
    }

    [HttpPost]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<EstimateDto>> Create(CreateEstimateCommand command, CancellationToken cancellationToken)
    {
        var estimate = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = estimate.Id }, estimate);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<EstimateDto>> Update(Guid id, UpdateEstimateCommand command, CancellationToken cancellationToken)
    {
        var estimate = await mediator.Send(command with { Id = id }, cancellationToken);
        return estimate is null ? NotFound() : Ok(estimate);
    }

    [HttpPost("{id:guid}/line-items")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<EstimateDto>> AddLineItem(Guid id, AddEstimateLineItemCommand command, CancellationToken cancellationToken)
    {
        var estimate = await mediator.Send(command with { EstimateId = id }, cancellationToken);
        return estimate is null ? NotFound() : Ok(estimate);
    }

    [HttpDelete("{id:guid}/line-items/{lineItemId:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<EstimateDto>> RemoveLineItem(Guid id, Guid lineItemId, CancellationToken cancellationToken)
    {
        var estimate = await mediator.Send(new RemoveEstimateLineItemCommand(id, lineItemId), cancellationToken);
        return estimate is null ? NotFound() : Ok(estimate);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "projects.write")]
    public Task<ActionResult<EstimateDto>> Submit(Guid id, CancellationToken cancellationToken) =>
        Transition(new SubmitEstimateCommand(id), cancellationToken);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<EstimateDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var estimate = await mediator.Send(new ApproveEstimateCommand(id), cancellationToken);
        if (estimate is null) return NotFound();

        try
        {
            var aggregate = await estimates.GetByIdWithItemsAsync(id, tracking: false, cancellationToken);
            var project = await projects.GetByIdAsync(estimate.ProjectId);
            if (aggregate is not null && project is not null)
            {
                var costedItems = aggregate.LineItems.Where(item => item.LineCost.HasValue).ToArray();
                var workCost = costedItems.Where(item => item.ItemType == EstimateLineItemType.Work).Sum(item => item.LineCost!.Value);
                var materialsCost = costedItems.Where(item => item.ItemType != EstimateLineItemType.Work).Sum(item => item.LineCost!.Value);
                await publishEndpoint.Publish(new EstimateApprovedEvent
                {
                    EstimateId = estimate.Id,
                    ProjectId = estimate.ProjectId,
                    ProjectName = string.Empty,
                    ClientId = project.ClientId,
                    ClientName = string.Empty,
                    EstimatedTotal = estimate.EstimatedTotal,
                    MaterialsCost = materialsCost,
                    WorksCost = workCost,
                    // The estimate model does not persist currency. Do not assume the currency.
                    Currency = string.Empty,
                    ApprovedDate = estimate.ApprovedAtUtc ?? DateTime.UtcNow,
                    LineItemCount = estimate.LineItems.Count
                }, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // The estimate is committed before the integration event; log rather than returning a false failure.
            logger.LogError(exception, "Estimate {EstimateId} was approved but its analytics event could not be published.", id);
        }

        return Ok(estimate);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "projects.write")]
    public Task<ActionResult<EstimateDto>> Reject(Guid id, CancellationToken cancellationToken) =>
        Transition(new RejectEstimateCommand(id), cancellationToken);

    [HttpPost("{id:guid}/supersede")]
    [Authorize(Policy = "projects.write")]
    public Task<ActionResult<EstimateDto>> Supersede(Guid id, CancellationToken cancellationToken) =>
        Transition(new SupersedeEstimateCommand(id), cancellationToken);

    private async Task<ActionResult<EstimateDto>> Transition<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : MediatR.IRequest<EstimateDto?>
    {
        var estimate = await mediator.Send(command, cancellationToken);
        return estimate is null ? NotFound() : Ok(estimate);
    }
}
