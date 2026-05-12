using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Middleware;
using Happie.Api.Options;
using MsOptions = Microsoft.Extensions.Options;

namespace Happie.Api.Tests.Middleware;

// Feature: happie, Property 5: Logout invalidates session

/// <summary>
/// Property-based tests verifying that a cleared or invalidated session token is rejected
/// by the JWT middleware on all subsequent requests.
/// Validates: Requirements 1.7
/// </summary>
public class LogoutInvalidatesSessionTests
{
    private const string ConfiguredSigningKey = "configured-signing-key-that-is-long-enough-for-hmac-sha256";

    private readonly JwtMiddleware _sut;

    /// <summary>Initializes a new instance of <see cref="LogoutInvalidatesSessionTests"/>.</summary>
    public LogoutInvalidatesSessionTests()
    {
        _sut = new JwtMiddleware(MsOptions.Options.Create(new JwtOptions { SigningKey = ConfiguredSigningKey }));
    }

    /// <summary>
    /// For any token signed with a key that differs from the configured signing key, the middleware
    /// must reject the token. This models the logout scenario: after logout the client clears the
    /// token from localStorage; any attempt to reuse a token signed with a different (or unknown)
    /// key is rejected.
    /// Validates: Requirements 1.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TryValidateToken_TokenSignedWithDifferentKey_IsRejected()
    {
        return Prop.ForAll(
            DifferentSigningKeyArb(),
            differentKey =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var token = JwtTokenFactory.Create(householdId, differentKey, DateTime.UtcNow.AddDays(30));

                // Act.
                var accepted = _sut.TryValidateToken(token, out _);

                // Assert.
                return (!accepted)
                    .Label($"Token signed with key '{differentKey}' must be rejected by middleware configured with a different key.");
            });
    }

    /// <summary>
    /// For any valid household ID, a token that has already expired must be rejected by the
    /// middleware. This models the scenario where a session has ended (e.g. token TTL elapsed
    /// after logout) and the client attempts to reuse the old token.
    /// Validates: Requirements 1.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TryValidateToken_ExpiredToken_IsRejected()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<Guid>(),
            householdId =>
            {
                // Arrange.
                // Use a past expiry to simulate a token that was valid but has since expired.
                var expiredToken = JwtTokenFactory.Create(householdId, ConfiguredSigningKey, DateTime.UtcNow.AddSeconds(-1));

                // Act.
                var accepted = _sut.TryValidateToken(expiredToken, out _);

                // Assert.
                return (!accepted)
                    .Label($"Expired token for household {householdId} must be rejected.");
            });
    }

    // Create methods.

    /// <summary>
    /// Generates signing key strings that are guaranteed to differ from the configured signing key
    /// and are long enough to be used as HMAC-SHA256 keys (at least 32 characters).
    /// </summary>
    private static Arbitrary<string> DifferentSigningKeyArb()
    {
        // Generate strings of printable ASCII characters with a fixed length of 50 characters
        // to satisfy HMAC-SHA256 key size requirements, then filter out the configured key.
        var charGen = Gen.Choose(33, 126).Select(x => (char)x);
        var keyGen = Gen.ArrayOf(charGen, 50)
            .Select(x => new string(x))
            .Where(x => x != ConfiguredSigningKey);

        return Arb.From(keyGen);
    }
}
