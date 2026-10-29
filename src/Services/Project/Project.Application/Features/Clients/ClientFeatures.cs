using FluentValidation;
using MediatR;
using Project.Application.DTOs;
using Project.Domain.Entities;
using Shared.Common.Persistence;

namespace Project.Application.Features.Clients;

public sealed record GetClientsQuery(int Page = 1, int PageSize = 50, bool IncludeArchived = false)
    : IRequest<PagedResponse<ClientDto>>;
public sealed record GetClientByIdQuery(Guid Id) : IRequest<ClientDto?>;
public sealed record CreateClientCommand(string DisplayName, string? Email, string? Phone, string? BillingAddress)
    : IRequest<ClientDto>;
public sealed record UpdateClientCommand(Guid Id, string DisplayName, string? Email, string? Phone, string? BillingAddress)
    : IRequest<ClientDto?>;
public sealed record ArchiveClientCommand(Guid Id) : IRequest<bool>;

public sealed class GetClientsQueryValidator : AbstractValidator<GetClientsQuery>
{
    public GetClientsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
public sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(254).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.BillingAddress).MaximumLength(500);
    }
}
public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(254).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.BillingAddress).MaximumLength(500);
    }
}

public sealed class GetClientsQueryHandler(IRepositoryBase<Client> clients)
    : IRequestHandler<GetClientsQuery, PagedResponse<ClientDto>>
{
    public async Task<PagedResponse<ClientDto>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await clients.GetPagedAsync(request.Page, request.PageSize,
            predicate: client => request.IncludeArchived || !client.IsArchived,
            orderBy: query => query.OrderBy(client => client.DisplayName));
        return new PagedResponse<ClientDto>(page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
    internal static ClientDto Map(Client client) => new(client.Id, client.DisplayName, client.Email, client.Phone,
        client.BillingAddress, client.IsArchived, client.CreatedDate, client.LastModifiedDate);
}
public sealed class GetClientByIdQueryHandler(IRepositoryBase<Client> clients)
    : IRequestHandler<GetClientByIdQuery, ClientDto?>
{
    public async Task<ClientDto?> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = await clients.GetByIdAsync(request.Id);
        return client is null ? null : GetClientsQueryHandler.Map(client);
    }
}
public sealed class CreateClientCommandHandler(IRepositoryBase<Client> clients)
    : IRequestHandler<CreateClientCommand, ClientDto>
{
    public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = new Client(Guid.NewGuid(), request.DisplayName, request.Email, request.Phone, request.BillingAddress);
        await clients.AddAsync(entity);
        return GetClientsQueryHandler.Map(entity);
    }
}
public sealed class UpdateClientCommandHandler(IRepositoryBase<Client> clients)
    : IRequestHandler<UpdateClientCommand, ClientDto?>
{
    public async Task<ClientDto?> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await clients.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.Update(request.DisplayName, request.Email, request.Phone, request.BillingAddress);
        await clients.UpdateAsync(entity);
        return GetClientsQueryHandler.Map(entity);
    }
}
public sealed class ArchiveClientCommandHandler(IRepositoryBase<Client> clients)
    : IRequestHandler<ArchiveClientCommand, bool>
{
    public async Task<bool> Handle(ArchiveClientCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await clients.GetByIdAsync(request.Id);
        if (entity is null) return false;
        entity.Archive();
        await clients.UpdateAsync(entity);
        return true;
    }
}
