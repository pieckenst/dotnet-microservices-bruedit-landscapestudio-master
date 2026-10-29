using FluentValidation;
using MediatR;
using Project.Application.DTOs;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Project.Application.Features.Projects;

public sealed record GetProjectsQuery(int Page = 1, int PageSize = 50, ProjectStatus? Status = null)
    : IRequest<PagedResponse<LandscapeProjectDto>>;
public sealed record GetProjectByIdQuery(Guid Id) : IRequest<LandscapeProjectDto?>;
public sealed record CreateProjectCommand(string ProjectNumber, string Name, Guid ClientId, Guid LandPlotId,
    string Description, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate) : IRequest<LandscapeProjectDto>;
public sealed record UpdateProjectCommand(Guid Id, string ProjectNumber, string Name, Guid ClientId, Guid LandPlotId,
    string Description, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate) : IRequest<LandscapeProjectDto?>;
public sealed record ChangeProjectStatusCommand(Guid Id, ProjectStatus Status) : IRequest<LandscapeProjectDto?>;

public sealed class GetProjectsQueryValidator : AbstractValidator<GetProjectsQuery>
{
    public GetProjectsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}
public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.ProjectNumber).NotEmpty().MaximumLength(40).Matches("^[a-zA-Z0-9._/-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.LandPlotId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.PlannedEndDate).Must((request, end) => end is null || request.PlannedStartDate is null || end >= request.PlannedStartDate)
            .WithMessage("Planned end date cannot be before planned start date.");
    }
}
public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProjectNumber).NotEmpty().MaximumLength(40).Matches("^[a-zA-Z0-9._/-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.LandPlotId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.PlannedEndDate).Must((request, end) => end is null || request.PlannedStartDate is null || end >= request.PlannedStartDate)
            .WithMessage("Planned end date cannot be before planned start date.");
    }
}
public sealed class ChangeProjectStatusCommandValidator : AbstractValidator<ChangeProjectStatusCommand>
{
    public ChangeProjectStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class GetProjectsQueryHandler(IRepositoryBase<LandscapeProject> projects)
    : IRequestHandler<GetProjectsQuery, PagedResponse<LandscapeProjectDto>>
{
    public async Task<PagedResponse<LandscapeProjectDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await projects.GetPagedAsync(request.Page, request.PageSize,
            predicate: project => request.Status == null || project.Status == request.Status,
            orderBy: query => query.OrderBy(project => project.ProjectNumber));
        return new PagedResponse<LandscapeProjectDto>(page.Items.Select(Map).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
    internal static LandscapeProjectDto Map(LandscapeProject project) => new(
        project.Id, project.ProjectNumber, project.Name, project.ClientId, project.LandPlotId, project.Description,
        project.Status, project.PlannedStartDate, project.PlannedEndDate, project.ActualStartDate,
        project.ActualEndDate, project.CreatedDate, project.LastModifiedDate);
}
public sealed class GetProjectByIdQueryHandler(IRepositoryBase<LandscapeProject> projects)
    : IRequestHandler<GetProjectByIdQuery, LandscapeProjectDto?>
{
    public async Task<LandscapeProjectDto?> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project = await projects.GetByIdAsync(request.Id);
        return project is null ? null : GetProjectsQueryHandler.Map(project);
    }
}
public sealed class CreateProjectCommandHandler(
    IRepositoryBase<LandscapeProject> projects,
    IRepositoryBase<Client> clients,
    IRepositoryBase<LandPlot> plots) : IRequestHandler<CreateProjectCommand, LandscapeProjectDto>
{
    public async Task<LandscapeProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetAsync(project => project.ProjectNumber == request.ProjectNumber.Trim().ToUpperInvariant()) is not null)
            throw new InvalidOperationException("A project with that number already exists.");
        var client = await clients.GetByIdAsync(request.ClientId);
        if (client is null || client.IsArchived) throw new NotFoundException(nameof(Client), request.ClientId);
        var plot = await plots.GetByIdAsync(request.LandPlotId);
        if (plot is null || plot.IsArchived) throw new NotFoundException(nameof(LandPlot), request.LandPlotId);
        var entity = new LandscapeProject(Guid.NewGuid(), request.ProjectNumber, request.Name, request.ClientId, request.LandPlotId,
            request.Description, request.PlannedStartDate, request.PlannedEndDate);
        await projects.AddAsync(entity);
        return GetProjectsQueryHandler.Map(entity);
    }
}
public sealed class UpdateProjectCommandHandler(
    IRepositoryBase<LandscapeProject> projects,
    IRepositoryBase<Client> clients,
    IRepositoryBase<LandPlot> plots) : IRequestHandler<UpdateProjectCommand, LandscapeProjectDto?>
{
    public async Task<LandscapeProjectDto?> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await projects.GetByIdAsync(request.Id);
        if (entity is null) return null;
        if (entity.Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled project cannot be edited.");
        if (await projects.GetAsync(project => project.Id != request.Id &&
            project.ProjectNumber == request.ProjectNumber.Trim().ToUpperInvariant()) is not null)
            throw new InvalidOperationException("A project with that number already exists.");
        var client = await clients.GetByIdAsync(request.ClientId);
        if (client is null || client.IsArchived) throw new NotFoundException(nameof(Client), request.ClientId);
        var plot = await plots.GetByIdAsync(request.LandPlotId);
        if (plot is null || plot.IsArchived) throw new NotFoundException(nameof(LandPlot), request.LandPlotId);
        entity.UpdateDetails(request.ProjectNumber, request.Name, request.ClientId, request.LandPlotId,
            request.Description, request.PlannedStartDate, request.PlannedEndDate);
        await projects.UpdateAsync(entity);
        return GetProjectsQueryHandler.Map(entity);
    }
}
public sealed class ChangeProjectStatusCommandHandler(IRepositoryBase<LandscapeProject> projects)
    : IRequestHandler<ChangeProjectStatusCommand, LandscapeProjectDto?>
{
    public async Task<LandscapeProjectDto?> Handle(ChangeProjectStatusCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await projects.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.ChangeStatus(request.Status, DateOnly.FromDateTime(DateTime.UtcNow));
        await projects.UpdateAsync(entity);
        return GetProjectsQueryHandler.Map(entity);
    }
}
