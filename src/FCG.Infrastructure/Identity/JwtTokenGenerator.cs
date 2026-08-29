using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Infrastructure.Identity;

public sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    IClock clock) : IJwtTokenGenerator
{
    public AccessToken Generate(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtOptions = options.Value;
        var issuedAtUtc = clock.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(jwtOptions.ExpirationMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            checked(jwtOptions.ExpirationMinutes * 60));
    }
}
