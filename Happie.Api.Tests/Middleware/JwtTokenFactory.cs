using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Happie.Api.Tests.Middleware;

/// <summary>Factory for creating JWT tokens in middleware tests.</summary>
internal static class JwtTokenFactory
{
    /// <summary>Creates a signed JWT containing a householdId claim with the given expiry.</summary>
    internal static string Create(Guid householdId, string signingKey, DateTime expires)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("householdId", householdId.ToString()),
        };

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
