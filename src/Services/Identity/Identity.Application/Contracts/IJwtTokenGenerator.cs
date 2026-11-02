using Identity.Domain.Entities;

namespace Identity.Application.Contracts;

public sealed record GeneratedJwtToken(string AccessToken, DateTime ExpiresAtUtc);
public interface IJwtTokenGenerator { GeneratedJwtToken Generate(UserCredential user, DateTime nowUtc); }
