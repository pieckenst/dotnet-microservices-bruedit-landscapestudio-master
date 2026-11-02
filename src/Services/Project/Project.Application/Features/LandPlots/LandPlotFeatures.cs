using FluentValidation;
using MediatR;
using Project.Application.DTOs;
using Project.Domain.Entities;
using Shared.Common.Persistence;

namespace Project.Application.Features.LandPlots;

public sealed record GetLandPlotsQuery(int Page = 1, int PageSize = 50, bool IncludeArchived = false)
    : IRequest<PagedResponse<LandPlotDto>>;
public sealed record GetLandPlotByIdQuery(Guid Id) : IRequest<LandPlotDto?>;
public sealed record CreateLandPlotCommand(string Name, string Address, decimal AreaSquareMetres, string? SoilType, string? Notes)
    : IRequest<LandPlotDto>;
public sealed record UpdateLandPlotCommand(Guid Id, string Name, string Address, decimal AreaSquareMetres, string? SoilType, string? Notes)
    : IRequest<LandPlotDto?>;
public sealed record ArchiveLandPlotCommand(Guid Id, bool Archived = true) : IRequest<bool>;

public sealed class GetLandPlotsQueryValidator : AbstractValidator<GetLandPlotsQuery>
{
    public GetLandPlotsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
public sealed class CreateLandPlotCommandValidator : AbstractValidator<CreateLandPlotCommand>
{
    public CreateLandPlotCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AreaSquareMetres).InclusiveBetween(0.01m, 100_000_000m);
        RuleFor(x => x.SoilType).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}
public sealed class UpdateLandPlotCommandValidator : AbstractValidator<UpdateLandPlotCommand>
{
    public UpdateLandPlotCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AreaSquareMetres).InclusiveBetween(0.01m, 100_000_000m);
        RuleFor(x => x.SoilType).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}

public sealed class GetLandPlotsQueryHandler(IRepositoryBase<LandPlot> plots)
    : IRequestHandler<GetLandPlotsQuery, PagedResponse<LandPlotDto>>
{
    public async Task<PagedResponse<LandPlotDto>> Handle(GetLandPlotsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await plots.GetPagedAsync(request.Page, request.PageSize,
            predicate: plot => request.IncludeArchived || !plot.IsArchived,
            orderBy: query => query.OrderBy(plot => plot.Name));
        return new PagedResponse<LandPlotDto>(page.Items.Select(Map).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
    internal static LandPlotDto Map(LandPlot plot) => new(plot.Id, plot.Name, plot.Address, plot.AreaSquareMetres,
        plot.SoilType, plot.Notes, plot.IsArchived, plot.CreatedDate, plot.LastModifiedDate);
}

public sealed class GetLandPlotByIdQueryHandler(IRepositoryBase<LandPlot> plots)
    : IRequestHandler<GetLandPlotByIdQuery, LandPlotDto?>
{
    public async Task<LandPlotDto?> Handle(GetLandPlotByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plot = await plots.GetByIdAsync(request.Id);
        return plot is null ? null : GetLandPlotsQueryHandler.Map(plot);
    }
}
public sealed class CreateLandPlotCommandHandler(IRepositoryBase<LandPlot> plots)
    : IRequestHandler<CreateLandPlotCommand, LandPlotDto>
{
    public async Task<LandPlotDto> Handle(CreateLandPlotCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = new LandPlot(Guid.NewGuid(), request.Name, request.Address, request.AreaSquareMetres, request.SoilType, request.Notes);
        await plots.AddAsync(entity);
        return GetLandPlotsQueryHandler.Map(entity);
    }
}
public sealed class UpdateLandPlotCommandHandler(IRepositoryBase<LandPlot> plots)
    : IRequestHandler<UpdateLandPlotCommand, LandPlotDto?>
{
    public async Task<LandPlotDto?> Handle(UpdateLandPlotCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await plots.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.Update(request.Name, request.Address, request.AreaSquareMetres, request.SoilType, request.Notes);
        await plots.UpdateAsync(entity);
        return GetLandPlotsQueryHandler.Map(entity);
    }
}
public sealed class ArchiveLandPlotCommandHandler(IRepositoryBase<LandPlot> plots)
    : IRequestHandler<ArchiveLandPlotCommand, bool>
{
    public async Task<bool> Handle(ArchiveLandPlotCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await plots.GetByIdAsync(request.Id);
        if (entity is null) return false;
        if (request.Archived) entity.Archive(); else entity.Restore();
        await plots.UpdateAsync(entity);
        return true;
    }
}
