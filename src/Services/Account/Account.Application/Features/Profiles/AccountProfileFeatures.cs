using Account.Application.Contracts.Persistence;
using Account.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Account.Application.Features.Profiles;

public sealed record GetMyProfileQuery(Guid UserId, string? IdentityDisplayName) : IRequest<AccountProfile>;
public sealed record UpdateMyProfileCommand(
    Guid UserId, string DisplayName, string? FirstName, string? LastName,
    string? Email, string? PhoneNumber, string? CompanyName) : IRequest<AccountProfile>;

public sealed record GetMyPreferencesQuery(Guid UserId, string? IdentityDisplayName) : IRequest<AccountPreference>;
public sealed record UpdateMyPreferencesCommand(
    Guid UserId, string Theme, string Language, string TimeZone,
    bool EmailNotificationsEnabled, bool SmsNotificationsEnabled) : IRequest<AccountPreference>;

public sealed record GetMyAddressesQuery(Guid UserId, string? IdentityDisplayName) : IRequest<IReadOnlyList<AccountAddress>>;
public sealed record CreateMyAddressCommand(
    Guid UserId, string? IdentityDisplayName, string Label, string Line1, string? Line2,
    string City, string? Region, string? PostalCode, string Country, bool IsDefault) : IRequest<AccountAddress>;
public sealed record UpdateMyAddressCommand(
    Guid UserId, Guid AddressId, string Label, string Line1, string? Line2,
    string City, string? Region, string? PostalCode, string Country, bool IsDefault) : IRequest<AccountAddress?>;
public sealed record DeleteMyAddressCommand(Guid UserId, Guid AddressId) : IRequest<bool>;

internal static class AccountProfileBootstrap
{
    public static async Task<(AccountProfile Profile, AccountPreference Preferences)> EnsureAsync(
        IAccountProfileRepository repository,
        Guid userId,
        string? identityDisplayName,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid Identity user id is required.", nameof(userId));

        var profile = await repository.GetProfileAsync(userId, cancellationToken);
        var preferences = await repository.GetPreferenceAsync(userId, cancellationToken);
        var changed = false;

        if (profile is null)
        {
            var displayName = string.IsNullOrWhiteSpace(identityDisplayName)
                ? $"User {userId.ToString("N")[..8]}"
                : identityDisplayName.Trim();
            profile = new AccountProfile(userId, displayName);
            repository.Add(profile);
            changed = true;
        }

        if (preferences is null)
        {
            preferences = new AccountPreference(userId);
            repository.Add(preferences);
            changed = true;
        }

        if (changed)
            await repository.SaveChangesAsync(cancellationToken);

        return (profile, preferences);
    }
}

public sealed class GetMyProfileQueryHandler(IAccountProfileRepository repository)
    : IRequestHandler<GetMyProfileQuery, AccountProfile>
{
    public async Task<AccountProfile> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var (profile, _) = await AccountProfileBootstrap.EnsureAsync(
            repository, request.UserId, request.IdentityDisplayName, cancellationToken);
        return profile;
    }
}

public sealed class UpdateMyProfileCommandHandler(IAccountProfileRepository repository)
    : IRequestHandler<UpdateMyProfileCommand, AccountProfile>
{
    public async Task<AccountProfile> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var (profile, _) = await AccountProfileBootstrap.EnsureAsync(
            repository, request.UserId, null, cancellationToken);
        profile.Update(request.DisplayName, request.FirstName, request.LastName,
            request.Email, request.PhoneNumber, request.CompanyName);
        await repository.SaveChangesAsync(cancellationToken);
        return profile;
    }
}

public sealed class GetMyPreferencesQueryHandler(IAccountProfileRepository repository)
    : IRequestHandler<GetMyPreferencesQuery, AccountPreference>
{
    public async Task<AccountPreference> Handle(GetMyPreferencesQuery request, CancellationToken cancellationToken)
    {
        var (_, preferences) = await AccountProfileBootstrap.EnsureAsync(
            repository, request.UserId, request.IdentityDisplayName, cancellationToken);
        return preferences;
    }
}

public sealed class UpdateMyPreferencesCommandHandler(IAccountProfileRepository repository)
    : IRequestHandler<UpdateMyPreferencesCommand, AccountPreference>
{
    public async Task<AccountPreference> Handle(UpdateMyPreferencesCommand request, CancellationToken cancellationToken)
    {
        var (_, preferences) = await AccountProfileBootstrap.EnsureAsync(
            repository, request.UserId, null, cancellationToken);
        preferences.Update(request.Theme, request.Language, request.TimeZone,
            request.EmailNotificationsEnabled, request.SmsNotificationsEnabled);
        await repository.SaveChangesAsync(cancellationToken);
        return preferences;
    }
}

public sealed class GetMyAddressesQueryHandler(IAccountProfileRepository repository)
    : IRequestHandler<GetMyAddressesQuery, IReadOnlyList<AccountAddress>>
{
    public async Task<IReadOnlyList<AccountAddress>> Handle(GetMyAddressesQuery request, CancellationToken cancellationToken)
    {
        await AccountProfileBootstrap.EnsureAsync(
            repository, request.UserId, request.IdentityDisplayName, cancellationToken);
        return await repository.GetAddressesAsync(request.UserId, cancellationToken);
    }
}

public sealed class CreateMyAddressCommandHandler(IAccountProfileRepository repository)
    : IRequestHandler<CreateMyAddressCommand, AccountAddress>
{
    public async Task<AccountAddress> Handle(CreateMyAddressCommand request, CancellationToken cancellationToken)
    {
        await AccountProfileBootstrap.EnsureAsync(
            repository, request.UserId, request.IdentityDisplayName, cancellationToken);
        if (request.IsDefault)
            await repository.ClearDefaultAddressesAsync(request.UserId, null, cancellationToken);

        var address = new AccountAddress(Guid.NewGuid(), request.UserId, request.Label,
            request.Line1, request.City, request.Country);
        address.Update(request.Label, request.Line1, request.Line2, request.City,
            request.Region, request.PostalCode, request.Country, request.IsDefault);
        repository.Add(address);
        await repository.SaveChangesAsync(cancellationToken);
        return address;
    }
}

public sealed class UpdateMyAddressCommandHandler(IAccountProfileRepository repository)
    : IRequestHandler<UpdateMyAddressCommand, AccountAddress?>
{
    public async Task<AccountAddress?> Handle(UpdateMyAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await repository.GetAddressAsync(request.AddressId, request.UserId, cancellationToken);
        if (address is null) return null;

        if (request.IsDefault)
            await repository.ClearDefaultAddressesAsync(request.UserId, request.AddressId, cancellationToken);

        address.Update(request.Label, request.Line1, request.Line2, request.City,
            request.Region, request.PostalCode, request.Country, request.IsDefault);
        await repository.SaveChangesAsync(cancellationToken);
        return address;
    }
}

public sealed class DeleteMyAddressCommandHandler(IAccountProfileRepository repository)
    : IRequestHandler<DeleteMyAddressCommand, bool>
{
    public async Task<bool> Handle(DeleteMyAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await repository.GetAddressAsync(request.AddressId, request.UserId, cancellationToken);
        if (address is null) return false;
        repository.Remove(address);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class GetMyProfileQueryValidator : AbstractValidator<GetMyProfileQuery>
{
    public GetMyProfileQueryValidator() => RuleFor(x => x.UserId).NotEmpty();
}
public sealed class GetMyPreferencesQueryValidator : AbstractValidator<GetMyPreferencesQuery>
{
    public GetMyPreferencesQueryValidator() => RuleFor(x => x.UserId).NotEmpty();
}
public sealed class GetMyAddressesQueryValidator : AbstractValidator<GetMyAddressesQuery>
{
    public GetMyAddressesQueryValidator() => RuleFor(x => x.UserId).NotEmpty();
}
public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.FirstName).MaximumLength(80);
        RuleFor(x => x.LastName).MaximumLength(80);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneNumber).MaximumLength(40);
        RuleFor(x => x.CompanyName).MaximumLength(160);
    }
}
public sealed class UpdateMyPreferencesCommandValidator : AbstractValidator<UpdateMyPreferencesCommand>
{
    public UpdateMyPreferencesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Theme).Must(value => new[] { "system", "light", "dark" }.Contains(value?.Trim().ToLowerInvariant()));
        RuleFor(x => x.Language).Must(value => new[] { "en", "ru", "be" }.Contains(value?.Trim().ToLowerInvariant()));
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(80);
    }
}
public sealed class CreateMyAddressCommandValidator : AbstractValidator<CreateMyAddressCommand>
{
    public CreateMyAddressCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Line2).MaximumLength(160);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Region).MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(24);
        RuleFor(x => x.Country).Matches("^[A-Za-z]{2}$");
    }
}
public sealed class UpdateMyAddressCommandValidator : AbstractValidator<UpdateMyAddressCommand>
{
    public UpdateMyAddressCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Line2).MaximumLength(160);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Region).MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(24);
        RuleFor(x => x.Country).Matches("^[A-Za-z]{2}$");
    }
}
public sealed class DeleteMyAddressCommandValidator : AbstractValidator<DeleteMyAddressCommand>
{
    public DeleteMyAddressCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AddressId).NotEmpty();
    }
}
