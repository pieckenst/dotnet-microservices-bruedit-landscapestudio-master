using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Organization.Application.Contracts;
using Organization.Application.Features.Assignments;
using Organization.Domain.Entities;
using Organization.Domain.Enums;
using Organization.Infrastructure.Persistence;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;
using Xunit;

namespace Organization.Tests;

public sealed class WorkAssignmentHandlerTests
{
    [Fact]
    public async Task CreateAssignmentValidatesRemoteProjectAndTaskBeforePersisting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OrganizationContext>().UseSqlite(connection).Options;
        await using var context = new OrganizationContext(options);
        await context.Database.EnsureCreatedAsync();

        var position = new Position(Guid.NewGuid(), "LAND-DES", "Landscape designer", "Designs sites", 1);
        var employee = new Employee(Guid.NewGuid(), "EMP-001", "Alex", "Green", position.Id,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        context.Positions.Add(position);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var lookup = new StubProjectAssignmentLookup("InProgress", "NotStarted");
        var handler = new CreateWorkAssignmentCommandHandler(
            new RepositoryBase<WorkAssignment>(context),
            new RepositoryBase<Employee>(context),
            new RepositoryBase<Brigade>(context),
            lookup);
        var command = new CreateWorkAssignmentCommand(
            employee.Id, null, projectId, taskId, "Install irrigation",
            DateOnly.FromDateTime(DateTime.UtcNow), null, "");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await context.WorkAssignments.AsNoTracking().SingleAsync(x => x.Id == result.Id);
        Assert.Equal(projectId, saved.ProjectId);
        Assert.Equal(taskId, saved.ProjectTaskId);
        Assert.Equal(employee.Id, saved.EmployeeId);
        Assert.Equal(1, lookup.ProjectCalls);
        Assert.Equal(1, lookup.TaskCalls);
    }

    [Fact]
    public async Task CreateAssignmentRejectsCompletedProjectsAndTasksFromAnotherProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OrganizationContext>().UseSqlite(connection).Options;
        await using var context = new OrganizationContext(options);
        await context.Database.EnsureCreatedAsync();

        var position = new Position(Guid.NewGuid(), "LAND-DES", "Landscape designer", "Designs sites", 1);
        var employee = new Employee(Guid.NewGuid(), "EMP-002", "Sam", "Green", position.Id,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        context.Positions.Add(position);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var command = new CreateWorkAssignmentCommand(
            employee.Id, null, Guid.NewGuid(), Guid.NewGuid(), "Install irrigation",
            DateOnly.FromDateTime(DateTime.UtcNow), null, "");

        var completedProjectHandler = new CreateWorkAssignmentCommandHandler(
            new RepositoryBase<WorkAssignment>(context),
            new RepositoryBase<Employee>(context),
            new RepositoryBase<Brigade>(context),
            new StubProjectAssignmentLookup("Completed", "NotStarted"));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            completedProjectHandler.Handle(command, CancellationToken.None));

        var mismatchedTaskHandler = new CreateWorkAssignmentCommandHandler(
            new RepositoryBase<WorkAssignment>(context),
            new RepositoryBase<Employee>(context),
            new RepositoryBase<Brigade>(context),
            new StubProjectAssignmentLookup("Planned", null));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            mismatchedTaskHandler.Handle(command, CancellationToken.None));

        Assert.Empty(await context.WorkAssignments.AsNoTracking().ToListAsync());
    }

    private sealed class StubProjectAssignmentLookup(string? projectStatus, string? taskStatus)
        : IProjectAssignmentLookup
    {
        public int ProjectCalls { get; private set; }
        public int TaskCalls { get; private set; }

        public Task<string?> GetProjectStatusAsync(Guid projectId, CancellationToken cancellationToken)
        {
            ProjectCalls++;
            return Task.FromResult(projectStatus);
        }

        public Task<string?> GetProjectTaskStatusAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
        {
            TaskCalls++;
            return Task.FromResult(taskStatus);
        }
    }
}
