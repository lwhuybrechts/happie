using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Happie.Api.Results;
using Happie.Api.Options;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Domain;
using Happie.Shared.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Happie.Api.Handlers;

/// <summary>Handles household login by verifying the password and issuing a signed JWT.</summary>
public class LoginHandler : ILoginHandler
{
    private readonly IHouseholdRepository _householdRepository;
    private readonly IHousemateRepository _housemateRepository;
    private readonly JwtOptions _jwtOptions;

    /// <summary>Initializes a new instance of <see cref="LoginHandler"/>.</summary>
    public LoginHandler(
        IHouseholdRepository householdRepository,
        IHousemateRepository housemateRepository,
        IOptions<JwtOptions> jwtOptions)
    {
        _householdRepository = householdRepository;
        _housemateRepository = housemateRepository;
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<LoginResult?> HandleAsync(string password, CancellationToken ct = default)
    {
        // Fetch all households and find the one whose password hash matches.
        var households = await _householdRepository.GetAllAsync(ct);

        var household = households.FirstOrDefault(x => BCrypt.Net.BCrypt.Verify(password, x.PasswordHash));
        if (household is null)
            return null;

        // Fetch all active (non-deleted) housemates for the matched household.
        var allHousemates = await _housemateRepository.GetAllAsync(household.Id, ct);
        var activeHousemates = allHousemates.Where(x => !x.IsDeleted).ToList();

        var token = IssueToken(household.Id);

        return new LoginResult(token, activeHousemates);
    }

    /// <summary>Issues a signed JWT scoped to the given household ID.</summary>
    private string IssueToken(Guid householdId)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_jwtOptions.SigningKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("householdId", householdId.ToString()),
        };

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
