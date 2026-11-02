using FluentValidation;
using Identity.Application.Contracts;
using Identity.Application.DTOs;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Shared.Common.Exceptions;

namespace Identity.Application.Features.Users;

public sealed record GetUsersQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResponse<IdentityUserDto>>;
public sealed record GetUserByIdQuery(Guid Id) : IRequest<IdentityUserDto?>;
public sealed record CreateUserCommand(string UserName, string Password, string RoleName) : IRequest<IdentityUserDto>;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 200);
    }
}
public sealed class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator() => RuleFor(request => request.Id).NotEmpty();
}
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(request => request.UserName).NotEmpty().Length(3, 80)
            .Matches("^[a-zA-Z0-9._@+-]+$")
            .WithMessage("User name can contain letters, digits, dot, underscore, @, plus and hyphen.");
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(request => request.RoleName).NotEmpty().MaximumLength(64);
    }
}

public sealed class GetUsersQueryHandler(IIdentityRepository repository)
    : IRequestHandler<GetUsersQuery, PagedResponse<IdentityUserDto>>
{
    public async Task<PagedResponse<IdentityUserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await repository.GetUsersPageAsync(request.Page, request.PageSize, cancellationToken);
        return new PagedResponse<IdentityUserDto>(page.Items.Select(IdentityUserDto.From).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}

public sealed class GetUserByIdQueryHandler(IIdentityRepository repository)
    : IRequestHandler<GetUserByIdQuery, IdentityUserDto?>
{
    public async Task<IdentityUserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await repository.GetUserByIdAsync(request.Id, cancellationToken);
        return user is null ? null : IdentityUserDto.From(user);
    }
}

public sealed class CreateUserCommandHandler(
    IIdentityRepository repository,
    IPasswordHasher<UserCredential> passwordHasher) : IRequestHandler<CreateUserCommand, IdentityUserDto>
{
    public async Task<IdentityUserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedName = request.UserName.Trim().ToUpperInvariant();
        if (await repository.UserNameExistsAsync(normalizedName, cancellationToken))
            throw new InvalidOperationException("A user with that user name already exists.");

        var role = await repository.FindRoleByNormalizedNameAsync(request.RoleName.Trim().ToUpperInvariant(), cancellationToken);
        if (role is null) throw new NotFoundException(nameof(Role), request.RoleName);
        if (!role.IsActive) throw new InvalidOperationException("An inactive role cannot be assigned.");

        var user = new UserCredential(Guid.NewGuid(), request.UserName, role.Id, "password-hash-pending");
        user.AssignRole(role);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password), DateTime.UtcNow);
        await repository.AddUserAsync(user, cancellationToken);
        return IdentityUserDto.From(user);
    }
}
