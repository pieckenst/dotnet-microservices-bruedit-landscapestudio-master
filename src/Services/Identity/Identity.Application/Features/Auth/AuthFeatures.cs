using FluentValidation;
using Identity.Application.Contracts;
using Identity.Application.DTOs;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Identity.Application.Features.Auth;

public sealed record LoginCommand(string UserName, string Password) : IRequest<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(request => request.UserName).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class LoginCommandHandler(
    IIdentityRepository repository,
    IPasswordHasher<UserCredential> passwordHasher,
    IJwtTokenGenerator tokenGenerator) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await repository.FindByNormalizedUserNameAsync(request.UserName.Trim().ToUpperInvariant(), cancellationToken);
        var now = DateTime.UtcNow;
        if (user is null || !user.IsActive || user.Role is null || !user.Role.IsActive || user.IsLockedOut(now))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.RegisterFailedLogin(now);
            await repository.UpdateUserAsync(user, cancellationToken);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password), now);
        user.RegisterSuccessfulLogin(now);
        await repository.UpdateUserAsync(user, cancellationToken);
        var token = tokenGenerator.Generate(user, now);
        return new AuthResponse(token.AccessToken, "Bearer", token.ExpiresAtUtc, IdentityUserDto.From(user));
    }
}
