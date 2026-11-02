using FluentValidation;
using MediatR;
using Procurement.Application.DTOs;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Shared.Common.Persistence;

namespace Procurement.Application.Features.Deliveries;

public sealed record GetSupplierDeliveriesQuery(int Page = 1, int PageSize = 50,
    Guid? PurchaseOrderId = null, DeliveryStatus? Status = null) : IRequest<PagedResult<SupplierDeliveryDto>>;
public sealed record GetSupplierDeliveryByIdQuery(Guid Id) : IRequest<SupplierDeliveryDto?>;
public sealed record CreateSupplierDeliveryCommand(Guid PurchaseOrderId, string DeliveryNumber,
    DateOnly ExpectedDate, string Carrier, string? TrackingReference = null) : IRequest<SupplierDeliveryDto>;
public sealed record ChangeSupplierDeliveryStatusCommand(Guid Id, DeliveryStatus Status,
    DateOnly? ActualDate = null) : IRequest<SupplierDeliveryDto?>;

public sealed class GetSupplierDeliveriesQueryValidator : AbstractValidator<GetSupplierDeliveriesQuery>
{
    public GetSupplierDeliveriesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.PurchaseOrderId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Status).Must(status => !status.HasValue || Enum.IsDefined(status.Value));
    }
}
public sealed class GetSupplierDeliveryByIdQueryValidator : AbstractValidator<GetSupplierDeliveryByIdQuery>
{
    public GetSupplierDeliveryByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class CreateSupplierDeliveryCommandValidator : AbstractValidator<CreateSupplierDeliveryCommand>
{
    public CreateSupplierDeliveryCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.DeliveryNumber).NotEmpty().MaximumLength(48);
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TrackingReference).MaximumLength(120);
    }
}
public sealed class ChangeSupplierDeliveryStatusCommandValidator : AbstractValidator<ChangeSupplierDeliveryStatusCommand>
{
    public ChangeSupplierDeliveryStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ActualDate).NotNull().When(x => x.Status == DeliveryStatus.Delivered);
        RuleFor(x => x.ActualDate).Null().When(x => x.Status != DeliveryStatus.Delivered);
    }
}

public static class SupplierDeliveryMapping
{
    public static SupplierDeliveryDto ToDto(SupplierDelivery x) => new(x.Id, x.PurchaseOrderId,
        x.DeliveryNumber, x.ExpectedDate, x.ActualDate, x.Carrier, x.TrackingReference,
        x.Status, x.CreatedDate, x.LastModifiedDate);
}

public sealed class GetSupplierDeliveriesQueryHandler(IRepositoryBase<SupplierDelivery> deliveries)
    : IRequestHandler<GetSupplierDeliveriesQuery, PagedResult<SupplierDeliveryDto>>
{
    public async Task<PagedResult<SupplierDeliveryDto>> Handle(GetSupplierDeliveriesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await deliveries.GetPagedAsync(request.Page, request.PageSize,
            predicate: item => (!request.PurchaseOrderId.HasValue || item.PurchaseOrderId == request.PurchaseOrderId) &&
                (!request.Status.HasValue || item.Status == request.Status),
            orderBy: query => query.OrderBy(item => item.ExpectedDate).ThenBy(item => item.DeliveryNumber));
        return new PagedResult<SupplierDeliveryDto>(page.Items.Select(SupplierDeliveryMapping.ToDto).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetSupplierDeliveryByIdQueryHandler(IRepositoryBase<SupplierDelivery> deliveries)
    : IRequestHandler<GetSupplierDeliveryByIdQuery, SupplierDeliveryDto?>
{
    public async Task<SupplierDeliveryDto?> Handle(GetSupplierDeliveryByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await deliveries.GetByIdAsync(request.Id);
        return entity is null ? null : SupplierDeliveryMapping.ToDto(entity);
    }
}

public sealed class CreateSupplierDeliveryCommandHandler(
    IRepositoryBase<SupplierDelivery> deliveries,
    IRepositoryBase<PurchaseOrder> purchaseOrders)
    : IRequestHandler<CreateSupplierDeliveryCommand, SupplierDeliveryDto>
{
    public async Task<SupplierDeliveryDto> Handle(CreateSupplierDeliveryCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = await purchaseOrders.GetByIdAsync(request.PurchaseOrderId);
        if (order is null) throw new KeyNotFoundException("Purchase order was not found.");
        if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            throw new InvalidOperationException("A delivery can only be scheduled for an approved or partially received purchase order.");
        var number = request.DeliveryNumber.Trim().ToUpperInvariant();
        if (await deliveries.GetAsync(item => item.DeliveryNumber == number) is not null)
            throw new InvalidOperationException("A delivery with that number already exists.");
        var entity = new SupplierDelivery(Guid.NewGuid(), request.PurchaseOrderId,
            request.DeliveryNumber, request.ExpectedDate, request.Carrier, request.TrackingReference ?? string.Empty);
        await deliveries.AddAsync(entity);
        return SupplierDeliveryMapping.ToDto(entity);
    }
}

public sealed class ChangeSupplierDeliveryStatusCommandHandler(IRepositoryBase<SupplierDelivery> deliveries)
    : IRequestHandler<ChangeSupplierDeliveryStatusCommand, SupplierDeliveryDto?>
{
    public async Task<SupplierDeliveryDto?> Handle(ChangeSupplierDeliveryStatusCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await deliveries.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.ChangeStatus(request.Status, request.ActualDate);
        await deliveries.UpdateAsync(entity);
        return SupplierDeliveryMapping.ToDto(entity);
    }
}