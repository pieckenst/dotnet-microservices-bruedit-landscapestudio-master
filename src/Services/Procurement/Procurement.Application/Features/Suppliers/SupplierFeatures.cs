using FluentValidation;
using MediatR;
using Procurement.Application.DTOs;
using Procurement.Domain.Entities;
using Shared.Common.Persistence;

namespace Procurement.Application.Features.Suppliers;

public sealed record GetSuppliersQuery(int Page = 1, int PageSize = 50, bool IncludeInactive = false)
    : IRequest<PagedResult<SupplierDto>>;
public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierDto?>;
public sealed record CreateSupplierCommand(string Code, string Name, string ContactName, string? Email,
    string? Phone, string? TaxIdentifier, string Address, int PaymentTermDays, string CurrencyCode = "BYN")
    : IRequest<SupplierDto>;
public sealed record UpdateSupplierCommand(Guid Id, string Code, string Name, string ContactName, string? Email,
    string? Phone, string? TaxIdentifier, string Address, int PaymentTermDays, string CurrencyCode = "BYN",
    bool? IsActive = null) : IRequest<SupplierDto?>;
public sealed record SetSupplierActiveCommand(Guid Id, bool IsActive) : IRequest<bool>;

public sealed class GetSuppliersQueryValidator : AbstractValidator<GetSuppliersQuery>
{
    public GetSuppliersQueryValidator() { RuleFor(x => x.Page).GreaterThanOrEqualTo(1); RuleFor(x => x.PageSize).InclusiveBetween(1, 200); }
}
public sealed class GetSupplierByIdQueryValidator : AbstractValidator<GetSupplierByIdQuery>
{
    public GetSupplierByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.TaxIdentifier).MaximumLength(80);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PaymentTermDays).InclusiveBetween(0, 365);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
    }
}
public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.TaxIdentifier).MaximumLength(80);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PaymentTermDays).InclusiveBetween(0, 365);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
    }
}
public sealed class SetSupplierActiveCommandValidator : AbstractValidator<SetSupplierActiveCommand>
{
    public SetSupplierActiveCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public static class SupplierMapping
{
    public static SupplierDto ToDto(Supplier x) => new(x.Id, x.Code, x.Name, x.ContactName, x.Email,
        x.Phone, x.TaxIdentifier, x.Address, x.PaymentTermDays, x.CurrencyCode, x.IsActive, x.CreatedDate, x.LastModifiedDate);
}

public sealed class GetSuppliersQueryHandler(IRepositoryBase<Supplier> suppliers)
    : IRequestHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    public async Task<PagedResult<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await suppliers.GetPagedAsync(request.Page, request.PageSize,
            predicate: supplier => request.IncludeInactive || supplier.IsActive,
            orderBy: query => query.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code));
        return new PagedResult<SupplierDto>(page.Items.Select(SupplierMapping.ToDto).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetSupplierByIdQueryHandler(IRepositoryBase<Supplier> suppliers)
    : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await suppliers.GetByIdAsync(request.Id);
        return entity is null ? null : SupplierMapping.ToDto(entity);
    }
}

public sealed class CreateSupplierCommandHandler(IRepositoryBase<Supplier> suppliers)
    : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    public async Task<SupplierDto> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await suppliers.GetAsync(item => item.Code == code) is not null)
            throw new InvalidOperationException("A supplier with that code already exists.");
        var entity = new Supplier(Guid.NewGuid(), request.Code, request.Name, request.ContactName, request.Email,
            request.Phone, request.TaxIdentifier, request.Address, request.PaymentTermDays, request.CurrencyCode);
        await suppliers.AddAsync(entity);
        return SupplierMapping.ToDto(entity);
    }
}

public sealed class UpdateSupplierCommandHandler(IRepositoryBase<Supplier> suppliers)
    : IRequestHandler<UpdateSupplierCommand, SupplierDto?>
{
    public async Task<SupplierDto?> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await suppliers.GetByIdAsync(request.Id);
        if (entity is null) return null;
        var code = request.Code.Trim().ToUpperInvariant();
        if (await suppliers.GetAsync(other => other.Id != entity.Id && other.Code == code) is not null)
            throw new InvalidOperationException("A supplier with that code already exists.");
        entity.Update(request.Code, request.Name, request.ContactName, request.Email, request.Phone,
            request.TaxIdentifier, request.Address, request.PaymentTermDays, request.CurrencyCode);
        if (request.IsActive is bool isActive)
            entity.SetActive(isActive);
        await suppliers.UpdateAsync(entity);
        return SupplierMapping.ToDto(entity);
    }
}

public sealed class SetSupplierActiveCommandHandler(IRepositoryBase<Supplier> suppliers)
    : IRequestHandler<SetSupplierActiveCommand, bool>
{
    public async Task<bool> Handle(SetSupplierActiveCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await suppliers.GetByIdAsync(request.Id);
        if (entity is null) return false;
        entity.SetActive(request.IsActive);
        await suppliers.UpdateAsync(entity);
        return true;
    }
}