using FluentValidation;
using MediatR;
using Project.Application.DTOs;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Project.Application.Features.Tasks;

public sealed record GetProjectTasksQuery(Guid ProjectId, int Page = 1, int PageSize = 50,
    Guid? StageId = null, ProjectTaskStatus? Status = null) : IRequest<PagedResponse<ProjectTaskDto>>;
public sealed record GetProjectTaskByIdQuery(Guid Id) : IRequest<ProjectTaskDto?>;

public sealed record CreateProjectTaskCommand(Guid ProjectId, Guid StageId, string Title, string Description,
    DateOnly? DueDate, Guid? AssignedEmployeeId, Guid? AssignedBrigadeId) : IRequest<ProjectTaskDto>;
public sealed record UpdateProjectTaskCommand(Guid Id, string Title, string Description, DateOnly? DueDate,
    Guid? AssignedEmployeeId, Guid? AssignedBrigadeId) : IRequest<ProjectTaskDto?>;
public sealed record ChangeProjectTaskStatusCommand(Guid Id, ProjectTaskStatus Status) : IRequest<ProjectTaskDto?>;

public sealed class GetProjectTasksQueryValidator : AbstractValidator<GetProjectTasksQuery>
{
    public GetProjectTasksQueryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).Must(status => !status.HasValue || Enum.IsDefined(status.Value));
    }
}
public sealed class CreateProjectTaskCommandValidator : AbstractValidator<CreateProjectTaskCommand>
{
    public CreateProjectTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.AssignedEmployeeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.AssignedBrigadeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x).Must(command => command.AssignedEmployeeId is null || command.AssignedBrigadeId is null)
            .WithMessage("Assign either an employee or a brigade, not both.");
    }
}
public sealed class UpdateProjectTaskCommandValidator : AbstractValidator<UpdateProjectTaskCommand>
{
    public UpdateProjectTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.AssignedEmployeeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.AssignedBrigadeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x).Must(command => command.AssignedEmployeeId is null || command.AssignedBrigadeId is null)
            .WithMessage("Assign either an employee or a brigade, not both.");
    }
}
public sealed class ChangeProjectTaskStatusCommandValidator : AbstractValidator<ChangeProjectTaskStatusCommand>
{
    public ChangeProjectTaskStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class GetProjectTaskByIdQueryHandler(IRepositoryBase<ProjectTask> tasks)
    : IRequestHandler<GetProjectTaskByIdQuery, ProjectTaskDto?>
{
    public async Task<ProjectTaskDto?> Handle(GetProjectTaskByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Id == Guid.Empty) return null;
        var task = await tasks.GetByIdAsync(request.Id);
        return task is null ? null : GetProjectTasksQueryHandler.Map(task);
    }
}

public sealed class GetProjectTasksQueryHandler(
    IRepositoryBase<LandscapeProject> projects, IRepositoryBase<ProjectTask> tasks)
    : IRequestHandler<GetProjectTasksQuery, PagedResponse<ProjectTaskDto>>
{
    public async Task<PagedResponse<ProjectTaskDto>> Handle(GetProjectTasksQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetByIdAsync(request.ProjectId) is null)
            throw new NotFoundException(nameof(LandscapeProject), request.ProjectId);
        var page = await tasks.GetPagedAsync(request.Page, request.PageSize,
            predicate: task => task.ProjectId == request.ProjectId &&
                (request.StageId == null || task.StageId == request.StageId) &&
                (request.Status == null || task.Status == request.Status),
            orderBy: query => query.OrderBy(task => task.DueDate).ThenBy(task => task.Title));
        return new PagedResponse<ProjectTaskDto>(page.Items.Select(Map).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
    internal static ProjectTaskDto Map(ProjectTask task) => new(task.Id, task.ProjectId, task.StageId, task.Title,
        task.Description, task.DueDate, task.AssignedEmployeeId, task.AssignedBrigadeId, task.Status,
        task.CompletedDate, task.CreatedDate, task.LastModifiedDate);
}
public sealed class CreateProjectTaskCommandHandler(
    IRepositoryBase<LandscapeProject> projects, IRepositoryBase<ProjectStage> stages, IRepositoryBase<ProjectTask> tasks)
    : IRequestHandler<CreateProjectTaskCommand, ProjectTaskDto>
{
    public async Task<ProjectTaskDto> Handle(CreateProjectTaskCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetByIdAsync(request.ProjectId) is null)
            throw new NotFoundException(nameof(LandscapeProject), request.ProjectId);
        var stage = await stages.GetByIdAsync(request.StageId);
        if (stage is null || stage.ProjectId != request.ProjectId)
            throw new NotFoundException(nameof(ProjectStage), request.StageId);
        var entity = new ProjectTask(Guid.NewGuid(), request.ProjectId, request.StageId, request.Title, request.Description,
            request.DueDate, request.AssignedEmployeeId, request.AssignedBrigadeId);
        await tasks.AddAsync(entity);
        return GetProjectTasksQueryHandler.Map(entity);
    }
}
public sealed class UpdateProjectTaskCommandHandler(IRepositoryBase<ProjectTask> tasks)
    : IRequestHandler<UpdateProjectTaskCommand, ProjectTaskDto?>
{
    public async Task<ProjectTaskDto?> Handle(UpdateProjectTaskCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await tasks.GetByIdAsync(request.Id);
        if (entity is null) return null;
        if (entity.Status is ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled task cannot be edited.");
        entity.Update(request.Title, request.Description, request.DueDate, request.AssignedEmployeeId, request.AssignedBrigadeId);
        await tasks.UpdateAsync(entity);
        return GetProjectTasksQueryHandler.Map(entity);
    }
}
public sealed class ChangeProjectTaskStatusCommandHandler(IRepositoryBase<ProjectTask> tasks)
    : IRequestHandler<ChangeProjectTaskStatusCommand, ProjectTaskDto?>
{
    public async Task<ProjectTaskDto?> Handle(ChangeProjectTaskStatusCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await tasks.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.ChangeStatus(request.Status, DateOnly.FromDateTime(DateTime.UtcNow));
        await tasks.UpdateAsync(entity);
        return GetProjectTasksQueryHandler.Map(entity);
    }
}
