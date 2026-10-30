using Organization.Application.Features.Assignments;
using Organization.Domain.Entities;
using Organization.Domain.Enums;
using Xunit;

namespace Organization.Tests;

public sealed class OrganizationDomainTests
{
    [Fact]
    public void EmployeeTerminationIsTerminal()
    {
        var position = Guid.NewGuid();
        var employee = new Employee(Guid.NewGuid(), "e-001", "Jordan", "Gardener",
            position, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90)));
        employee.ChangeStatus(EmployeeStatus.Terminated, DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(EmployeeStatus.Terminated, employee.Status);
        Assert.NotNull(employee.TerminationDate);
        Assert.Throws<InvalidOperationException>(() => employee.ChangeStatus(EmployeeStatus.Active, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public void BrigadeMembershipCannotDuplicateAndLeavingIsRecorded()
    {
        var brigade = new Brigade(Guid.NewGuid(), "BR-01", "Planting team", "Planting", "");
        var employeeId = Guid.NewGuid();
        var member = new BrigadeMember(Guid.NewGuid(), brigade.Id, employeeId, BrigadeMemberRole.Foreperson,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)));
        brigade.AddMember(member);
        Assert.Throws<InvalidOperationException>(() => brigade.AddMember(
            new BrigadeMember(Guid.NewGuid(), brigade.Id, employeeId, BrigadeMemberRole.Member,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))));
        brigade.RemoveMember(employeeId, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.NotNull(member.LeftDate);
        Assert.Throws<KeyNotFoundException>(() => brigade.RemoveMember(employeeId, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Fact]
    public void WorkAssignmentMustTargetExactlyOneWorkerOrBrigade()
    {
        Assert.Throws<ArgumentException>(() => new WorkAssignment(Guid.NewGuid(), null, null, Guid.NewGuid(),
            null, "Landscape build", DateOnly.FromDateTime(DateTime.UtcNow), null));
        Assert.Throws<ArgumentException>(() => new WorkAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), null, "Landscape build", DateOnly.FromDateTime(DateTime.UtcNow), null));
    }

    [Fact]
    public void WorkAssignmentStatusCanAdvanceButTerminalStatesCannotBeReopened()
    {
        var assignment = new WorkAssignment(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            null, "Install irrigation", DateOnly.FromDateTime(DateTime.UtcNow), null);

        assignment.ChangeStatus(AssignmentStatus.Active);
        assignment.ChangeStatus(AssignmentStatus.Completed);

        Assert.Equal(AssignmentStatus.Completed, assignment.Status);
        Assert.Throws<InvalidOperationException>(() => assignment.ChangeStatus(AssignmentStatus.Active));

        var cancelled = new WorkAssignment(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            null, "Planting crew", DateOnly.FromDateTime(DateTime.UtcNow), null);
        cancelled.ChangeStatus(AssignmentStatus.Cancelled);
        Assert.Throws<InvalidOperationException>(() => cancelled.ChangeStatus(AssignmentStatus.Planned));
    }

    [Fact]
    public void WorkAssignmentCommandRequiresExactlyOneTargetAndValidDateRange()
    {
        var validator = new CreateWorkAssignmentCommandValidator();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var validEmployee = new CreateWorkAssignmentCommand(
            Guid.NewGuid(), null, Guid.NewGuid(), null, "Install paving", today, today.AddDays(2), "");

        Assert.True(validator.Validate(validEmployee).IsValid);
        Assert.False(validator.Validate(validEmployee with { EmployeeId = null }).IsValid);
        Assert.False(validator.Validate(validEmployee with { BrigadeId = Guid.NewGuid() }).IsValid);
        Assert.False(validator.Validate(validEmployee with { EndDate = today.AddDays(-1) }).IsValid);
    }
}
