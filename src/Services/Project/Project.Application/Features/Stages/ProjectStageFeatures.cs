using FluentValidation;
using MediatR;
using Project.Application.DTOs;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Project.Application.Features.Stages;

public sealed record GetProjectStagesQuery(Guid ProjectId, int Page = 1, int PageSize = 50)
    : IRequest<PagedResponse<ProjectStageDto>>;
public sealed record CreateProjectStageCommand(Guid ProjectId, string Name, int Sequence, string Description,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate) : IRequest<ProjectStageDto>;
public sealed record UpdateProjectStageCommand(Guid Id, string Name, int Sequence, string Description,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate) : IRequest<ProjectStageDto?>;
public sealed record ChangeProjectStageStatusCommand(Guid Id, StageStatus Status) : IRequest<ProjectStageDto?>;

public sealed class GetProjectStagesQueryValidator : AbstractValidator<GetProjectStagesQuery>
{
    public GetProjectStagesQueryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
public sealed class CreateProjectStageCommandValidator : AbstractValidator<CreateProjectStageCommand>
{
    public CreateProjectStageCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.PlannedEndDate).Must((request, end) => end is null || request.PlannedStartDate is null || end >= request.PlannedStartDate)
            .WithMessage("Planned end date cannot be before planned start date.");
    }
}
public sealed class UpdateProjectStageCommandValidator : AbstractValidator<UpdateProjectStageCommand>
{
    public UpdateProjectStageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.PlannedEndDate).Must((request, end) => end is null || request.PlannedStartDate is null || end >= request.PlannedStartDate)
            .WithMessage("Planned end date cannot be before planned start date.");
    }
}
public sealed class ChangeProjectStageStatusCommandValidator : AbstractValidator<ChangeProjectStageStatusCommand>
{
    public ChangeProjectStageStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class GetProjectStagesQueryHandler(
    IRepositoryBase<LandscapeProject> projects, IRepositoryBase<ProjectStage> stages)
    : IRequestHandler<GetProjectStagesQuery, PagedResponse<ProjectStageDto>>
{
    public async Task<PagedResponse<ProjectStageDto>> Handle(GetProjectStagesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetByIdAsync(request.ProjectId) is null)
            throw new NotFoundException(nameof(LandscapeProject), request.ProjectId);
        var page = await stages.GetPagedAsync(request.Page, request.PageSize,
            predicate: stage => stage.ProjectId == request.ProjectId,
            orderBy: query => query.OrderBy(stage => stage.Sequence));
        return new PagedResponse<ProjectStageDto>(page.Items.Select(Map).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
    internal static ProjectStageDto Map(ProjectStage stage) => new(stage.Id, stage.ProjectId, stage.Name, stage.Sequence,
        stage.Description, stage.Status, stage.PlannedStartDate, stage.PlannedEndDate, stage.ActualStartDate,
        stage.ActualEndDate, stage.CreatedDate, stage.LastModifiedDate);
}
public sealed class CreateProjectStageCommandHandler(
    IRepositoryBase<LandscapeProject> projects, IRepositoryBase<ProjectStage> stages)
    : IRequestHandler<CreateProjectStageCommand, ProjectStageDto>
{
    public async Task<ProjectStageDto> Handle(CreateProjectStageCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await projects.GetByIdAsync(request.ProjectId) is null)
            throw new NotFoundException(nameof(LandscapeProject), request.ProjectId);
        if (await stages.GetAsync(stage => stage.ProjectId == request.ProjectId && stage.Sequence == request.Sequence) is not null)
            throw new InvalidOperationException("A stage with that sequence already exists in this project.");
        var stage = new ProjectStage(Guid.NewGuid(), request.ProjectId, request.Name, request.Sequence,
            request.Description, request.PlannedStartDate, request.PlannedEndDate);
        await stages.AddAsync(stage);
        return GetProjectStagesQueryHandler.Map(stage);
    }
}
public sealed class UpdateProjectStageCommandHandler(IRepositoryBase<ProjectStage> stages)
    : IRequestHandler<UpdateProjectStageCommand, ProjectStageDto?>
{
    public async Task<ProjectStageDto?> Handle(UpdateProjectStageCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stage = await stages.GetByIdAsync(request.Id);
        if (stage is null) return null;
        if (stage.Status == StageStatus.Completed)
            throw new InvalidOperationException("A completed stage cannot be edited.");
        if (await stages.GetAsync(other => other.ProjectId == stage.ProjectId &&
            other.Sequence == request.Sequence && other.Id != stage.Id) is not null)
            throw new InvalidOperationException("A stage with that sequence already exists in this project.");
        stage.Update(request.Name, request.Sequence, request.Description, request.PlannedStartDate, request.PlannedEndDate);
        await stages.UpdateAsync(stage);
        return GetProjectStagesQueryHandler.Map(stage);
    }
}
public sealed class ChangeProjectStageStatusCommandHandler(IRepositoryBase<ProjectStage> stages)
    : IRequestHandler<ChangeProjectStageStatusCommand, ProjectStageDto?>
{
    public async Task<ProjectStageDto?> Handle(ChangeProjectStageStatusCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stage = await stages.GetByIdAsync(request.Id);
        if (stage is null) return null;
        stage.ChangeStatus(request.Status, DateOnly.FromDateTime(DateTime.UtcNow));
        await stages.UpdateAsync(stage);
        return GetProjectStagesQueryHandler.Map(stage);
    }
}
