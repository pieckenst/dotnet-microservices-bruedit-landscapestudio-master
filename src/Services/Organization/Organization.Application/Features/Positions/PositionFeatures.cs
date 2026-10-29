using FluentValidation;
using MediatR;
using Organization.Application.DTOs;
using Organization.Domain.Entities;
using Shared.Common.Persistence;

namespace Organization.Application.Features.Positions;

public sealed record GetPositionsQuery(int Page = 1, int PageSize = 50, bool IncludeInactive = false) : IRequest<PagedResponse<PositionDto>>;
public sealed record CreatePositionCommand(string Code, string Title, string Description, int Rank) : IRequest<PositionDto>;
public sealed record UpdatePositionCommand(Guid Id, string Code, string Title, string Description, int Rank, bool IsActive) : IRequest<PositionDto?>;

public sealed class GetPositionsQueryValidator : AbstractValidator<GetPositionsQuery>
{
    public GetPositionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
public sealed class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9._-]+$");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Rank).InclusiveBetween(1, 1000);
    }
}
public sealed class UpdatePositionCommandValidator : AbstractValidator<UpdatePositionCommand>
{
    public UpdatePositionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9._-]+$");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Rank).InclusiveBetween(1, 1000);
    }
}
public static class PositionMapping
{
    public static PositionDto ToDto(Position x) => new(x.Id, x.Code, x.Title, x.Description, x.Rank, x.IsActive, x.CreatedDate);
}
public sealed class GetPositionsQueryHandler(IRepositoryBase<Position> positions)
    : IRequestHandler<GetPositionsQuery, PagedResponse<PositionDto>>
{
    public async Task<PagedResponse<PositionDto>> Handle(GetPositionsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await positions.GetPagedAsync(request.Page, request.PageSize,
            predicate: item => request.IncludeInactive || item.IsActive, orderBy: query => query.OrderBy(item => item.Rank).ThenBy(item => item.Title));
        return new PagedResponse<PositionDto>(page.Items.Select(PositionMapping.ToDto).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}
public sealed class CreatePositionCommandHandler(IRepositoryBase<Position> positions)
    : IRequestHandler<CreatePositionCommand, PositionDto>
{
    public async Task<PositionDto> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await positions.GetAsync(item => item.Code == code) is not null)
            throw new InvalidOperationException("A position with that code already exists.");
        var entity = new Position(Guid.NewGuid(), request.Code, request.Title, request.Description, request.Rank);
        await positions.AddAsync(entity);
        return PositionMapping.ToDto(entity);
    }
}
public sealed class UpdatePositionCommandHandler(IRepositoryBase<Position> positions)
    : IRequestHandler<UpdatePositionCommand, PositionDto?>
{
    public async Task<PositionDto?> Handle(UpdatePositionCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await positions.GetByIdAsync(request.Id);
        if (entity is null) return null;
        var code = request.Code.Trim().ToUpperInvariant();
        if (await positions.GetAsync(other => other.Id != entity.Id && other.Code == code) is not null)
            throw new InvalidOperationException("A position with that code already exists.");
        entity.Update(request.Code, request.Title, request.Description, request.Rank);
        entity.SetActive(request.IsActive);
        await positions.UpdateAsync(entity);
        return PositionMapping.ToDto(entity);
    }
}
