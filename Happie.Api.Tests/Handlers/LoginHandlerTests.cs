using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExpectedObjects;
using Happie.Api.Handlers;
using Happie.Api.Models;
using Happie.Api.Options;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Microsoft.IdentityModel.Tokens;
using Moq;
using MsOptions = Microsoft.Extensions.Options;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="LoginHandler"/>.</summary>
public class LoginHandlerTests
{
    private const string TestSigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";

    private readonly Mock<IHouseholdRepository> _householdRepositoryMock = new();
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly LoginHandler _sut;

    /// <summary>Initializes a new instance of <see cref="LoginHandlerTests"/> with mocked dependencies.</summary>
    public LoginHandlerTests()
    {
        var jwtOptions = MsOptions.Options.Create(new JwtOptions { SigningKey = TestSigningKey });

        _sut = new LoginHandler(
            _householdRepositoryMock.Object,
            _housemateRepositoryMock.Object,
            jwtOptions);
    }

    /// <summary>Correct password returns a result containing all active housemates.</summary>
    [Fact]
    public async Task HandleAsync_CorrectPassword_ReturnsActiveHousemates()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        var expectedHousemates = CreateHousemates(householdId, aliceId, bobId);

        SetupGetAllHouseholds(new List<Household> { new(householdId, "Test Household", passwordHash) });
        SetupGetAllHousemates(householdId, expectedHousemates);

        // Act.
        var result = await _sut.HandleAsync("correct-password");

        // Assert.
        expectedHousemates
            .ToExpectedObject()
            .ShouldEqual(result!.Housemates);
    }

    /// <summary>Correct password returns a JWT containing the householdId claim.</summary>
    [Fact]
    public async Task HandleAsync_CorrectPassword_ReturnsTokenWithHouseholdIdClaim()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        SetupGetAllHouseholds(new List<Household> { new(householdId, "Test Household", passwordHash) });
        SetupGetAllHousemates(householdId, new List<Housemate>());

        // Act.
        var result = await _sut.HandleAsync("correct-password");

        // Assert.
        var keyBytes = Encoding.UTF8.GetBytes(TestSigningKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(result!.Token, validationParameters, out _);
        var claim = principal.FindFirst("householdId");

        Assert.Equal(householdId.ToString(), claim?.Value);
    }

    /// <summary>Correct password excludes soft-deleted housemates from the result.</summary>
    [Fact]
    public async Task HandleAsync_CorrectPassword_ExcludesDeletedHousemates()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var aliceId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        var expectedHousemates = new List<Housemate>
        {
            new(aliceId, householdId, "Alice", HousemateColors.Palette[0], false),
        };

        SetupGetAllHouseholds(new List<Household> { new(householdId, "Test Household", passwordHash) });
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            new(aliceId, householdId, "Alice", HousemateColors.Palette[0], false),
            new(Guid.NewGuid(), householdId, "Deleted Bob", HousemateColors.Palette[1], true),
        });

        // Act.
        var result = await _sut.HandleAsync("correct-password");

        // Assert.
        expectedHousemates
            .ToExpectedObject()
            .ShouldEqual(result!.Housemates);
    }

    /// <summary>Incorrect password returns null.</summary>
    [Fact]
    public async Task HandleAsync_IncorrectPassword_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        SetupGetAllHouseholds(new List<Household> { new(householdId, "Test Household", passwordHash) });

        // Act.
        var result = await _sut.HandleAsync("wrong-password");

        // Assert.
        Assert.Null(result);
    }

    /// <summary>Incorrect password does not query housemates.</summary>
    [Fact]
    public async Task HandleAsync_IncorrectPassword_DoesNotQueryHousemates()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        SetupGetAllHouseholds(new List<Household> { new(householdId, "Test Household", passwordHash) });

        // Act.
        await _sut.HandleAsync("wrong-password");

        // Assert.
        _housemateRepositoryMock.Verify(
            x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>No households in the store returns null.</summary>
    [Fact]
    public async Task HandleAsync_NoHouseholds_ReturnsNull()
    {
        // Arrange.
        SetupGetAllHouseholds(new List<Household>());

        // Act.
        var result = await _sut.HandleAsync("any-password");

        // Assert.
        Assert.Null(result);
    }

    private void SetupGetAllHouseholds(List<Household> returns)
    {
        _householdRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllHousemates(Guid householdId, List<Housemate> returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private static List<Housemate> CreateHousemates(Guid householdId, Guid aliceId, Guid bobId) =>
        new()
        {
            new(aliceId, householdId, "Alice", HousemateColors.Palette[0], false),
            new(bobId, householdId, "Bob", HousemateColors.Palette[1], false),
        };
}
