using FluentValidation;
using MediatR;
using Project.Application.Contracts;
using Project.Application.DTOs;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Project.Application.Features.Estimates;

public sealed record GetEstimatesQuery(Guid ProjectId, int Page = 1, int PageSize = 50)
    : IRequest<PagedResponse<EstimateDto>>;
public sealed record GetEstimateByIdQuery(Guid Id) : IRequest<EstimateDto?>;
public sealed record CreateEstimateCommand(Guid ProjectId, string Version, string Notes) : IRequest<EstimateDto>;
public sealed record UpdateEstimateCommand(Guid Id, string Version, string Notes) : IRequest<EstimateDto?>;
public sealed record AddEstimateLineItemCommand(Guid EstimateId, EstimateLineItemType ItemType, string Description,
    decimal Quantity, string UnitSymbol, decimal UnitPrice, decimal? UnitCost, Guid? CatalogItemId) : IRequest<EstimateDto?>;
public sealed record RemoveEstimateLineItemCommand(Guid EstimateId, Guid LineItemId) : IRequest<EstimateDto?>;
public sealed record SubmitEstimateCommand(Guid Id) : IRequest<EstimateDto?>;
public sealed record ApproveEstimateCommand(Guid Id) : IRequest<EstimateDto?>;
public sealed record RejectEstimateCommand(Guid Id) : IRequest<EstimateDto?>;
public sealed record SupersedeEstimateCommand(Guid Id) : IRequest<EstimateDto?>;

public sealed class GetEstimatesQueryValidator : AbstractValidator<GetEstimatesQuery>
{
    public GetEstimatesQueryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
public sealed class CreateEstimateCommandValidator : AbstractValidator<CreateEstimateCommand>
{
    public CreateEstimateCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Version).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
public sealed class UpdateEstimateCommandValidator : AbstractValidator<UpdateEstimateCommand>
{
    public UpdateEstimateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Version).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
public sealed class AddEstimateLineItemCommandValidator : AbstractValidator<AddEstimateLineItemCommand>
{
    public AddEstimateLineItemCommandValidator()
    {
        RuleFor(x => x.EstimateId).NotEmpty();
        RuleFor(x => x.ItemType).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(x => x.UnitSymbol).NotEmpty().MaximumLength(24);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue);
        RuleFor(x => x.CatalogItemId).Must(id => !id.HasValue || id.Value != Guid.Empty);
    }
}

public sealed class GetEstimatesQueryHandler(
    IRepositoryBase<LandscapeProject> projects, IEstimateRepository estimates)
    : IRequestHandler<GetEstimatesQuery, PagedResponse<EstimateDto>>
{
    public async Task<PagedResponse<EstimateDto>> Handle(GetEstimatesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetByIdAsync(request.ProjectId) is null)
            throw new NotFoundException(nameof(LandscapeProject), request.ProjectId);
        var page = await estimates.GetByProjectAsync(request.ProjectId, request.Page, request.PageSize, cancellationToken);
        return new PagedResponse<EstimateDto>(page.Items.Select(Map).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
    internal static EstimateDto Map(Estimate estimate) => new(
        estimate.Id, estimate.ProjectId, estimate.Version, estimate.Notes, estimate.Status,
        estimate.SubmittedAtUtc, estimate.ApprovedAtUtc, estimate.EstimatedTotal, estimate.EstimatedCost,
        estimate.LineItems.OrderBy(item => item.CreatedDate).Select(item => new EstimateLineItemDto(
            item.Id, item.ItemType, item.Description, item.Quantity, item.UnitSymbol, item.UnitPrice,
            item.UnitCost, item.CatalogItemId, item.LineTotal, item.LineCost)).ToArray(),
        estimate.CreatedDate, estimate.LastModifiedDate);
}
public sealed class GetEstimateByIdQueryHandler(IEstimateRepository estimates)
    : IRequestHandler<GetEstimateByIdQuery, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(GetEstimateByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.Id, tracking: false, cancellationToken);
        return estimate is null ? null : GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class CreateEstimateCommandHandler(
    IRepositoryBase<LandscapeProject> projects, IRepositoryBase<Estimate> estimateIndex, IEstimateRepository estimates)
    : IRequestHandler<CreateEstimateCommand, EstimateDto>
{
    public async Task<EstimateDto> Handle(CreateEstimateCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetByIdAsync(request.ProjectId) is null)
            throw new NotFoundException(nameof(LandscapeProject), request.ProjectId);
        var version = request.Version.Trim().ToUpperInvariant();
        if (await estimateIndex.GetAsync(estimate => estimate.ProjectId == request.ProjectId && estimate.Version == version) is not null)
            throw new InvalidOperationException("An estimate with that version already exists for the project.");
        var estimate = new Estimate(Guid.NewGuid(), request.ProjectId, version, request.Notes);
        await estimates.AddAsync(estimate, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class UpdateEstimateCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<UpdateEstimateCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(UpdateEstimateCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.Id, tracking: true, cancellationToken);
        if (estimate is null) return null;
        if (estimate.Status is EstimateStatus.Approved or EstimateStatus.Superseded)
            throw new InvalidOperationException("Approved or superseded estimates are immutable.");
        estimate.Update(request.Version, request.Notes);
        await estimates.SaveAsync(estimate, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class AddEstimateLineItemCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<AddEstimateLineItemCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(AddEstimateLineItemCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.EstimateId, tracking: true, cancellationToken);
        if (estimate is null) return null;
        var item = new EstimateLineItem(Guid.NewGuid(), estimate.Id, request.ItemType, request.Description,
            request.Quantity, request.UnitSymbol, request.UnitPrice, request.UnitCost, request.CatalogItemId);
        await estimates.AddLineItemAsync(estimate, item, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class RemoveEstimateLineItemCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<RemoveEstimateLineItemCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(RemoveEstimateLineItemCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.EstimateId, tracking: true, cancellationToken);
        if (estimate is null) return null;
        await estimates.RemoveLineItemAsync(estimate, request.LineItemId, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class SubmitEstimateCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<SubmitEstimateCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(SubmitEstimateCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.Id, tracking: true, cancellationToken);
        if (estimate is null) return null;
        estimate.Submit(DateTime.UtcNow);
        await estimates.SaveAsync(estimate, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class ApproveEstimateCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<ApproveEstimateCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(ApproveEstimateCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.Id, tracking: true, cancellationToken);
        if (estimate is null) return null;
        estimate.Approve(DateTime.UtcNow);
        await estimates.SaveAsync(estimate, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class RejectEstimateCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<RejectEstimateCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(RejectEstimateCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.Id, tracking: true, cancellationToken);
        if (estimate is null) return null;
        estimate.Reject();
        await estimates.SaveAsync(estimate, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
public sealed class SupersedeEstimateCommandHandler(IEstimateRepository estimates)
    : IRequestHandler<SupersedeEstimateCommand, EstimateDto?>
{
    public async Task<EstimateDto?> Handle(SupersedeEstimateCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var estimate = await estimates.GetByIdWithItemsAsync(request.Id, tracking: true, cancellationToken);
        if (estimate is null) return null;
        estimate.Supersede();
        await estimates.SaveAsync(estimate, cancellationToken);
        return GetEstimatesQueryHandler.Map(estimate);
    }
}
