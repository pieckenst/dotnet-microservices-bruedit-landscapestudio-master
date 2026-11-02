using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Organization.Domain.Entities;
using Organization.Domain.Enums;

namespace Organization.Infrastructure.Persistence;

public sealed class OrganizationContextSeed
{
    private static readonly Guid PositionLandscapeArchitectId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid PositionForepersonId        = Guid.Parse("b1000000-0000-0000-0000-000000000002");
    private static readonly Guid PositionGardenerId          = Guid.Parse("b1000000-0000-0000-0000-000000000003");
    private static readonly Guid PositionEquipmentOperatorId = Guid.Parse("b1000000-0000-0000-0000-000000000004");

    private static readonly Guid EmployeeAnnaId   = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    private static readonly Guid EmployeeDmitriId = Guid.Parse("b2000000-0000-0000-0000-000000000002");
    private static readonly Guid EmployeeElenaId  = Guid.Parse("b2000000-0000-0000-0000-000000000003");
    private static readonly Guid EmployeePavelId  = Guid.Parse("b2000000-0000-0000-0000-000000000004");

    private static readonly Guid BrigadeAlphaId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
    private static readonly Guid BrigadeBetaId  = Guid.Parse("b3000000-0000-0000-0000-000000000002");

    private static readonly Guid MemberDmitriAlphaId = Guid.Parse("b4000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberElenaAlphaId  = Guid.Parse("b4000000-0000-0000-0000-000000000002");
    private static readonly Guid MemberPavelBetaId   = Guid.Parse("b4000000-0000-0000-0000-000000000003");

    public static async Task SeedAsync(
        OrganizationContext context,
        ILogger<OrganizationContextSeed> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (await context.Positions.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Organization seed skipped: positions already present.");
            return;
        }

        var architect = new Position(PositionLandscapeArchitectId, "LA", "Landscape Architect", "Designs landscape plans, selects plant species, oversees project execution.", 90);
        var foreperson = new Position(PositionForepersonId, "FP", "Brigade Foreperson", "Leads a field brigade, supervises on-site work, reports progress.", 70);
        var gardener = new Position(PositionGardenerId, "GD", "Gardener", "Performs planting, pruning, and general horticultural maintenance.", 50);
        var operatorPos = new Position(PositionEquipmentOperatorId, "OP", "Equipment Operator", "Operates excavators, tillers, and other landscape machinery.", 60);

        await context.Positions.AddRangeAsync([architect, foreperson, gardener, operatorPos], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var anna = new Employee(EmployeeAnnaId, "EMP-001", "Anna", "Petrova", PositionLandscapeArchitectId,
            new DateOnly(2020, 3, 15), null, "anna.petrova@landscapestudio.example", "+375 29 111 22 33");
        var dmitri = new Employee(EmployeeDmitriId, "EMP-002", "Dmitri", "Volkov", PositionForepersonId,
            new DateOnly(2019, 6, 1), null, "dmitri.volkov@landscapestudio.example", "+375 29 222 33 44");
        var elena = new Employee(EmployeeElenaId, "EMP-003", "Elena", "Sidorova", PositionGardenerId,
            new DateOnly(2021, 9, 10), null, "elena.sidorova@landscapestudio.example", "+375 29 333 44 55");
        var pavel = new Employee(EmployeePavelId, "EMP-004", "Pavel", "Kozlov", PositionEquipmentOperatorId,
            new DateOnly(2018, 2, 20), null, "pavel.kozlov@landscapestudio.example", "+375 29 444 55 66");

        await context.Employees.AddRangeAsync([anna, dmitri, elena, pavel], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var alphaBrigade = new Brigade(BrigadeAlphaId, "BR-A", "Alpha Planting Brigade", "Planting & Horticulture",
            "Specializes in tree, shrub, and perennial installation and care.", EmployeeDmitriId);
        var betaBrigade = new Brigade(BrigadeBetaId, "BR-B", "Beta Hardscape Brigade", "Hardscape & Earthworks",
            "Handles pathways, retaining walls, excavation, and heavy equipment work.", EmployeePavelId);

        await context.Brigades.AddRangeAsync([alphaBrigade, betaBrigade], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var dmitriMembership = new BrigadeMember(MemberDmitriAlphaId, BrigadeAlphaId, EmployeeDmitriId,
            BrigadeMemberRole.Foreperson, new DateOnly(2019, 6, 1));
        var elenaMembership = new BrigadeMember(MemberElenaAlphaId, BrigadeAlphaId, EmployeeElenaId,
            BrigadeMemberRole.Member, new DateOnly(2021, 9, 10));
        var pavelMembership = new BrigadeMember(MemberPavelBetaId, BrigadeBetaId, EmployeePavelId,
            BrigadeMemberRole.Foreperson, new DateOnly(2018, 2, 20));

        alphaBrigade.AddMember(dmitriMembership);
        alphaBrigade.AddMember(elenaMembership);
        betaBrigade.AddMember(pavelMembership);

        await context.BrigadeMembers.AddRangeAsync([dmitriMembership, elenaMembership, pavelMembership], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Organization seed completed: 4 positions, 4 employees, 2 brigades, 3 memberships.");
    }
}
