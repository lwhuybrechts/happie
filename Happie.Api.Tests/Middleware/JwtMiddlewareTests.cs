using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExpectedObjects;
using Happie.Api.Middleware;
using Happie.Api.Options;
using Microsoft.IdentityModel.Tokens;
using MsOptions = Microsoft.Extensions.Options;

namespace Happie.Api.Tests.Middleware;

/// <summary>Unit tests for <see cref="JwtMiddleware"/>.</summary>
public class JwtMiddlewareTests
{
    private const string TestSigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";
    private const string DifferentSigningKey = "different-signing-key-that-is-long-enough-for-hmac";

    private readonly JwtMiddleware _sut;

    /// <summary>Initializes a new instance of <see cref="JwtMiddlewareTests"/>.</summary>
    public JwtMiddlewareTests()
    {
        _sut = new JwtMiddleware(MsOptions.Options.Create(new JwtOptions { SigningKey = TestSigningKey }));
    }

    /// <summary>Valid token with correct signing key is accepted and yields the householdId.</summary>
    [Fact]
    public void TryValidateToken_ValidToken_ReturnsTrueAndExtractsHouseholdId()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var token = JwtTokenFactory.Create(householdId, TestSigningKey, DateTime.UtcNow.AddDays(30));

        // Act.
        var accepted = _sut.TryValidateToken(token, out var extractedId);

        // Assert.
        new { Accepted = true, HouseholdId = householdId }
            .ToExpectedObject()
            .ShouldEqual(new { Accepted = accepted, HouseholdId = extractedId });
    }

    /// <summary>Token signed with a different key is rejected.</summary>
    [Fact]
    public void TryValidateToken_WrongSigningKey_ReturnsFalse()
    {
        // Arrange.
        var token = JwtTokenFactory.Create(Guid.NewGuid(), DifferentSigningKey, DateTime.UtcNow.AddDays(30));

        // Act.
        var accepted = _sut.TryValidateToken(token, out _);

        // Assert.
        Assert.False(accepted);
    }

    /// <summary>Expired token is rejected.</summary>
    [Fact]
    public void TryValidateToken_ExpiredToken_ReturnsFalse()
    {
        // Arrange.
        var token = JwtTokenFactory.Create(Guid.NewGuid(), TestSigningKey, DateTime.UtcNow.AddSeconds(-1));

        // Act.
        var accepted = _sut.TryValidateToken(token, out _);

        // Assert.
        Assert.False(accepted);
    }

    /// <summary>Token without a householdId claim is rejected even when the signature is valid.</summary>
    [Fact]
    public void TryValidateToken_MissingHouseholdIdClaim_ReturnsFalse()
    {
        // Arrange.
        var token = CreateTokenWithoutHouseholdClaim(TestSigningKey, DateTime.UtcNow.AddDays(30));

        // Act.
        var accepted = _sut.TryValidateToken(token, out _);

        // Assert.
        Assert.False(accepted);
    }

    /// <summary>Random non-JWT string is rejected.</summary>
    [Fact]
    public void TryValidateToken_RandomString_ReturnsFalse()
    {
        // Arrange.
        var token = "not-a-jwt-token";

        // Act.
        var accepted = _sut.TryValidateToken(token, out _);

        // Assert.
        Assert.False(accepted);
    }

    /// <summary>Valid Bearer header yields the token string.</summary>
    [Fact]
    public void TryExtractBearerToken_ValidHeader_ReturnsTrueAndToken()
    {
        // Arrange.
        var expectedToken = "some.jwt.token";

        // Act.
        var extracted = JwtMiddleware.TryExtractBearerToken($"Bearer {expectedToken}", out var token);

        // Assert.
        new { Extracted = true, Token = expectedToken }
            .ToExpectedObject()
            .ShouldEqual(new { Extracted = extracted, Token = token });
    }

    /// <summary>Missing Authorization header is rejected.</summary>
    [Fact]
    public void TryExtractBearerToken_NullHeader_ReturnsFalse()
    {
        // Arrange.
        string? header = null;

        // Act.
        var extracted = JwtMiddleware.TryExtractBearerToken(header, out _);

        // Assert.
        Assert.False(extracted);
    }

    /// <summary>Non-Bearer Authorization scheme is rejected.</summary>
    [Fact]
    public void TryExtractBearerToken_NonBearerScheme_ReturnsFalse()
    {
        // Arrange.
        var header = "Basic dXNlcjpwYXNz";

        // Act.
        var extracted = JwtMiddleware.TryExtractBearerToken(header, out _);

        // Assert.
        Assert.False(extracted);
    }

    /// <summary>Login route is identified as anonymous.</summary>
    [Fact]
    public void IsAnonymousRoute_LoginPath_ReturnsTrue()
    {
        // Arrange.
        var path = "/api/auth/login";

        // Act.
        var isAnonymous = JwtMiddleware.IsAnonymousRoute(path);

        // Assert.
        Assert.True(isAnonymous);
    }

    /// <summary>Non-login route is not anonymous.</summary>
    [Fact]
    public void IsAnonymousRoute_HousematesPath_ReturnsFalse()
    {
        // Arrange.
        var path = "/api/housemates";

        // Act.
        var isAnonymous = JwtMiddleware.IsAnonymousRoute(path);

        // Assert.
        Assert.False(isAnonymous);
    }

    /// <summary>Valid GUID header is parsed successfully.</summary>
    [Fact]
    public void TryParseHousemateId_ValidGuid_ReturnsTrueAndId()
    {
        // Arrange.
        var housemateId = Guid.NewGuid();

        // Act.
        var parsed = JwtMiddleware.TryParseHousemateId(housemateId.ToString(), out var result);

        // Assert.
        new { Parsed = true, Id = housemateId }
            .ToExpectedObject()
            .ShouldEqual(new { Parsed = parsed, Id = result });
    }

    /// <summary>Non-GUID header value is rejected.</summary>
    [Fact]
    public void TryParseHousemateId_InvalidGuid_ReturnsFalse()
    {
        // Arrange.
        var header = "not-a-guid";

        // Act.
        var parsed = JwtMiddleware.TryParseHousemateId(header, out _);

        // Assert.
        Assert.False(parsed);
    }

    /// <summary>Null header value is rejected.</summary>
    [Fact]
    public void TryParseHousemateId_NullHeader_ReturnsFalse()
    {
        // Arrange.
        string? header = null;

        // Act.
        var parsed = JwtMiddleware.TryParseHousemateId(header, out _);

        // Assert.
        Assert.False(parsed);
    }

    // Create methods.

    private static string CreateTokenWithoutHouseholdClaim(string signingKey, DateTime expires)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", "some-subject"),
        };

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}
