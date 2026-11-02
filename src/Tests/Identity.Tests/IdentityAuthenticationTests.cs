using Identity.Application.Contracts;
using Identity.Application.Features.Auth;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Tests;

public sealed class IdentityAuthenticationTests
{
    [Fact]
    public void PasswordHasherVerifiesCorrectPasswordAndRejectsIncorrectPassword()
    {
        var hasher = new PasswordHasher<UserCredential>();
        var user = CreateUser(hasher, "correct-horse-battery-staple");

        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, user.PasswordHash, "correct-horse-battery-staple"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, user.PasswordHash, "incorrect-password"));
        Assert.DoesNotContain("correct-horse-battery-staple", user.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedLoginsUseGenericErrorAndLockAccountAfterFiveAttempts()
    {
        var hasher = new PasswordHasher<UserCredential>();
        var user = CreateUser(hasher, "correct-password");
        var repository = new FakeIdentityRepository(user);
        var tokenGenerator = new FakeTokenGenerator();
        var handler = new LoginCommandHandler(repository, hasher, tokenGenerator);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new LoginCommand("test.user", "wrong-password"), CancellationToken.None));
            Assert.Equal("Invalid username or password.", exception.Message);
        }

        Assert.NotNull(user.LockoutEndUtc);
        Assert.Equal(0, user.FailedAccessCount);
        Assert.Equal(5, repository.UpdateCount);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new LoginCommand("test.user", "correct-password"), CancellationToken.None));
        Assert.Equal(0, tokenGenerator.CallCount);
    }

    [Fact]
    public async Task ValidLoginResetsFailuresAndRequestsJwt()
    {
        var hasher = new PasswordHasher<UserCredential>();
        var user = CreateUser(hasher, "correct-password");
        user.RegisterFailedLogin(DateTime.UtcNow);
        var repository = new FakeIdentityRepository(user);
        var tokenGenerator = new FakeTokenGenerator();
        var handler = new LoginCommandHandler(repository, hasher, tokenGenerator);

        var result = await handler.Handle(
            new LoginCommand(" TEST.USER ", "correct-password"), CancellationToken.None);

        Assert.Equal("test.user", result.User.UserName);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("test.jwt", result.AccessToken);
        Assert.Equal(1, tokenGenerator.CallCount);
        Assert.Equal(0, user.FailedAccessCount);
        Assert.Null(user.LockoutEndUtc);
        Assert.NotNull(user.LastLoginAtUtc);
    }

    [Fact]
    public async Task SeedIsIdempotentAndCreatesExpectedRolesAndPermissions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<IdentityContext>().UseSqlite(connection).Options;
        await using var context = new IdentityContext(options);
        await context.Database.EnsureCreatedAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var hasher = new PasswordHasher<UserCredential>();

        await IdentityContextSeed.SeedAsync(context, hasher, configuration,
            NullLogger<IdentityContextSeed>.Instance);
        await IdentityContextSeed.SeedAsync(context, hasher, configuration,
            NullLogger<IdentityContextSeed>.Instance);

        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(12, await context.Permissions.CountAsync());
        Assert.Equal(12, await context.RolePermissions.CountAsync(join =>
            join.Role.NormalizedName == "ADMINISTRATOR"));
        Assert.Equal(2, await context.RolePermissions.CountAsync(join =>
            join.Role.NormalizedName == "USER"));
        Assert.Empty(await context.UserCredentials.ToListAsync());
    }

    private static UserCredential CreateUser(IPasswordHasher<UserCredential> hasher, string password)
    {
        var role = new Role(Guid.NewGuid(), "User", "Standard application user.", isSystemRole: true);
        var user = new UserCredential(Guid.NewGuid(), "test.user", role.Id, "temporary-hash");
        user.AssignRole(role);
        user.SetPasswordHash(hasher.HashPassword(user, password), DateTime.UtcNow);
        return user;
    }

    private sealed class FakeIdentityRepository(UserCredential user) : IIdentityRepository
    {
        public int UpdateCount { get; private set; }

        public Task<UserCredential?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult<UserCredential?>(user.NormalizedUserName == normalizedUserName ? user : null);
        public Task<UserCredential?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<UserCredential?>(user.Id == id ? user : null);
        public Task<Role?> FindRoleByNormalizedNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
            Task.FromResult<Role?>(user.Role.NormalizedName == normalizedRoleName ? user.Role : null);
        public Task<bool> UserNameExistsAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName == normalizedUserName);
        public Task<Shared.Common.Persistence.PagedResult<UserCredential>> GetUsersPageAsync(
            int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new Shared.Common.Persistence.PagedResult<UserCredential>([user], page, pageSize, 1));
        public Task AddUserAsync(UserCredential value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateUserAsync(UserCredential value, CancellationToken cancellationToken)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenGenerator : IJwtTokenGenerator
    {
        public int CallCount { get; private set; }
        public GeneratedJwtToken Generate(UserCredential user, DateTime nowUtc)
        {
            CallCount++;
            return new GeneratedJwtToken("test.jwt", nowUtc.AddMinutes(60));
        }
    }
}
