using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Entities;
using Organization.Domain.Enums;
using Organization.Infrastructure.Persistence;
using Xunit;

namespace Organization.Tests;

public sealed class OrganizationPersistenceTests
{
    [Fact]
    public async Task EmployeeBrigadeMembershipAndRemoteProjectAssignmentPersist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OrganizationContext>().UseSqlite(connection).Options;
        await using var context = new OrganizationContext(options);
        await context.Database.EnsureCreatedAsync();

        var position = new Position(Guid.NewGuid(), "LAND-DES", "Landscape designer", "Plans and designs gardens", 1);
        var employee = new Employee(Guid.NewGuid(), "EMP-001", "Morgan", "Green", position.Id,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100)), Guid.NewGuid(), "morgan@example.test", null);
        context.Positions.Add(position);
        await context.SaveChangesAsync();
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var brigade = new Brigade(Guid.NewGuid(), "BR-001", "Installation team", "Installation", "Hardscape and planting", employee.Id);
        brigade.AddMember(new BrigadeMember(Guid.NewGuid(), brigade.Id, employee.Id, BrigadeMemberRole.Foreperson,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20))));
        context.Brigades.Add(brigade);
        await context.SaveChangesAsync();

        var projectId = Guid.NewGuid(); // Owned by Project service; intentionally not an FK in this database.
        var assignment = new WorkAssignment(Guid.NewGuid(), null, brigade.Id, projectId, Guid.NewGuid(),
            "Install paving", DateOnly.FromDateTime(DateTime.UtcNow), null, "Task and project IDs are external references.");
        context.WorkAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var loaded = await context.Brigades.Include(x => x.Members).ThenInclude(x => x.Employee)
            .AsNoTracking().SingleAsync(x => x.Id == brigade.Id);
        var savedAssignment = await context.WorkAssignments.AsNoTracking().SingleAsync(x => x.Id == assignment.Id);
        Assert.Single(loaded.Members);
        Assert.Equal("Morgan Green", loaded.Members.Single().Employee.FullName);
        Assert.Equal(projectId, savedAssignment.ProjectId);
        Assert.NotEqual(default, savedAssignment.CreatedDate);
    }
}
