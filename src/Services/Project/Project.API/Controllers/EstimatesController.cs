using EventBus.Messages.Events;
using Shared.ServiceKit.Outbox;
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
    IRepositoryBase<Client> clients,
    IIntegrationOutbox outbox) : ControllerBase
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

        var aggregate = await estimates.GetByIdWithItemsAsync(id, tracking: false, cancellationToken)
            ?? throw new InvalidOperationException("The approved estimate could not be reloaded with its line items.");
        var project = await projects.GetByIdAsync(estimate.ProjectId)
            ?? throw new InvalidOperationException("The project for the approved estimate no longer exists.");
        var client = await clients.GetByIdAsync(project.ClientId)
            ?? throw new InvalidOperationException("The CRM client for the approved estimate no longer exists.");

        var costedItems = aggregate.LineItems.Where(item => item.LineCost.HasValue).ToArray();
        var workCost = costedItems
            .Where(item => item.ItemType == EstimateLineItemType.Work)
            .Sum(item => item.LineCost!.Value);
        var materialsCost = costedItems
            .Where(item => item.ItemType != EstimateLineItemType.Work)
            .Sum(item => item.LineCost!.Value);

        outbox.Enqueue(new EstimateApprovedEvent
        {
            EstimateId = estimate.Id,
            ProjectId = estimate.ProjectId,
            ProjectName = project.Name,
            ClientId = project.ClientId,
            ClientName = client.DisplayName,
            EstimatedTotal = estimate.EstimatedTotal,
            MaterialsCost = materialsCost,
            WorksCost = workCost,
            Currency = string.Empty,
            ApprovedDate = estimate.ApprovedAtUtc ?? DateTime.UtcNow,
            LineItemCount = estimate.LineItems.Count
        });

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
