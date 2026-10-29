using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Identity.Infrastructure.Security;

public sealed class JwtTokenGenerator(IConfiguration configuration) : IJwtTokenGenerator
{
    public GeneratedJwtToken Generate(UserCredential user, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Role is null) throw new InvalidOperationException("Cannot issue a token without a resolved role.");
        var keyText = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(keyText) || Encoding.UTF8.GetByteCount(keyText) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        var issuer = configuration["Jwt:Issuer"] ?? "LandscapeStudio.Identity";
        var audience = configuration["Jwt:Audience"] ?? "LandscapeStudio.Clients";
        var minutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var configured)
            ? Math.Clamp(configured, 5, 1440) : 60;
        var issuedAt = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        var expiresAt = issuedAt.AddMinutes(minutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        foreach (var permission in user.Role.RolePermissions.Where(item => item.Permission is not null && item.Permission.IsActive)
                     .Select(item => item.Permission.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim("permission", permission));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(issuer, audience, claims, issuedAt, expiresAt, credentials);
        return new GeneratedJwtToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }
}
