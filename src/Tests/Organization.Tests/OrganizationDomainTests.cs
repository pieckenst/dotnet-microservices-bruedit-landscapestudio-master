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
}
