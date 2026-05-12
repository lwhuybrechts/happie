using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Handlers;
using Happie.Api.Options;
using Happie.Api.Infrastructure.Repositories;
using Happie.Shared.Domain;
using Moq;
using MsOptions = Microsoft.Extensions.Options;

namespace Happie.Api.Tests.Handlers;

/// <summary>Property-based tests for <see cref="LoginHandler"/>.</summary>
public class LoginHandlerPropertyTests
{
    private const string TestSigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";

    private readonly Mock<IHouseholdRepository> _householdRepositoryMock = new();
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly LoginHandler _sut;

    /// <summary>Initializes a new instance of <see cref="LoginHandlerPropertyTests"/> with mocked dependencies.</summary>
    public LoginHandlerPropertyTests()
    {
        var jwtOptions = MsOptions.Options.Create(new JwtOptions { SigningKey = TestSigningKey });

        _sut = new LoginHandler(
            _householdRepositoryMock.Object,
            _housemateRepositoryMock.Object,
            jwtOptions);
    }

    // Feature: happie, Property 4: Wrong password is denied
    /// <summary>
    /// For any string that does not match any known household password, the login handler must return null
    /// and must not return any household data.
    /// Validates: Requirements 1.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HandleAsync_WrongPassword_ReturnsNull()
    {
        return Prop.ForAll(
            WrongPasswordArb(),
            async args =>
            {
                var (correctPassword, wrongPassword) = args;

                // Arrange.
                var householdId = Guid.NewGuid();
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword);
                var household = new Household(householdId, "Test Household", passwordHash);

                _householdRepositoryMock
                    .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Household> { household });

                // Act.
                var result = await _sut.HandleAsync(wrongPassword);

                // Assert.
                return (result == null)
                    .Label($"Expected null for wrong password '{wrongPassword}' (correct password was '{correctPassword}')");
            });
    }

    /// <summary>
    /// Generates a pair of (correctPassword, wrongPassword) where the two strings are guaranteed to differ.
    /// Both are non-null, non-empty strings to represent realistic password inputs.
    /// </summary>
    private static Arbitrary<(string CorrectPassword, string WrongPassword)> WrongPasswordArb()
    {
        // Generate non-null, non-empty strings for both passwords, ensuring they differ.
        var nonEmptyStringGen = ArbMap.Default.GeneratorFor<string>()
            .Where(x => !string.IsNullOrEmpty(x));

        var gen = nonEmptyStringGen.SelectMany(correct =>
            nonEmptyStringGen
                .Where(wrong => wrong != correct)
                .Select(wrong => (CorrectPassword: correct, WrongPassword: wrong)));

        return Arb.From(gen);
    }
}
