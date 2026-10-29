using Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityContextSeed
{
    private static readonly (string Name, string Description)[] PermissionDefinitions =
    [
        ("users.read", "View identity users"), ("users.write", "Create and manage identity users"),
        ("catalog.read", "Read the landscaping catalog"), ("catalog.write", "Manage the landscaping catalog"),
        ("projects.read", "Read landscaping projects"), ("projects.write", "Manage landscaping projects"),
        ("organization.read", "Read organization records"), ("organization.write", "Manage organization records"),
        ("procurement.read", "Read purchasing and stock records"), ("procurement.write", "Manage purchasing and stock records"),
        ("analytics.read", "Read operational analytics"), ("analytics.export", "Export analytics")
    ];

    public static async Task SeedAsync(
        IdentityContext context, IPasswordHasher<UserCredential> passwordHasher,
        IConfiguration configuration, ILogger<IdentityContextSeed> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        if (!await context.Roles.AnyAsync(role => role.NormalizedName == "ADMINISTRATOR", cancellationToken))
            context.Roles.Add(new Role(Guid.Parse("c8fb4b6e-2a81-4d94-b331-2b00b2231001"), "Administrator", "Full administrative access.", true));
        if (!await context.Roles.AnyAsync(role => role.NormalizedName == "USER", cancellationToken))
            context.Roles.Add(new Role(Guid.Parse("c8fb4b6e-2a81-4d94-b331-2b00b2231002"), "User", "Standard application access.", true));
        await context.SaveChangesAsync(cancellationToken);

        foreach (var definition in PermissionDefinitions)
        {
            var normalized = definition.Name.ToUpperInvariant();
            if (!await context.Permissions.AnyAsync(item => item.NormalizedName == normalized, cancellationToken))
                context.Permissions.Add(new Permission(Guid.NewGuid(), definition.Name, definition.Description));
        }
        await context.SaveChangesAsync(cancellationToken);

        var admin = await context.Roles.SingleAsync(role => role.NormalizedName == "ADMINISTRATOR", cancellationToken);
        var userRole = await context.Roles.SingleAsync(role => role.NormalizedName == "USER", cancellationToken);
        var permissions = await context.Permissions.ToListAsync(cancellationToken);
        var userPermissions = new HashSet<string>(["CATALOG.READ", "PROJECTS.READ"], StringComparer.OrdinalIgnoreCase);
        foreach (var permission in permissions)
        {
            if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == admin.Id && rp.PermissionId == permission.Id, cancellationToken))
                context.RolePermissions.Add(new RolePermission(Guid.NewGuid(), admin.Id, permission.Id));
            if (userPermissions.Contains(permission.NormalizedName) &&
                !await context.RolePermissions.AnyAsync(rp => rp.RoleId == userRole.Id && rp.PermissionId == permission.Id, cancellationToken))
                context.RolePermissions.Add(new RolePermission(Guid.NewGuid(), userRole.Id, permission.Id));
        }
        await context.SaveChangesAsync(cancellationToken);

        var username = configuration["Identity:BootstrapAdmin:UserName"];
        var password = configuration["Identity:BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Identity role and permission seed complete; no bootstrap administrator credentials were configured.");
            return;
        }
        var normalizedUserName = username.Trim().ToUpperInvariant();
        if (await context.UserCredentials.AnyAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            logger.LogInformation("Identity seed found an existing bootstrap username; no credentials were changed.");
            return;
        }

        var credential = new UserCredential(Guid.NewGuid(), username, admin.Id, "bootstrap-password-hash-pending");
        credential.AssignRole(admin);
        credential.SetPasswordHash(passwordHasher.HashPassword(credential, password), DateTime.UtcNow);
        await context.UserCredentials.AddAsync(credential, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogWarning("A bootstrap administrator was created from deployment configuration. Rotate its password according to deployment policy.");
    }
}
