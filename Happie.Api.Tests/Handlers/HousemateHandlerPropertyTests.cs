using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Domain;
using Happie.Shared.Domain;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Property-based tests for <see cref="HousemateHandler"/>.</summary>
public class HousemateHandlerPropertyTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly HousemateHandler _sut;

    /// <summary>Initializes a new instance of <see cref="HousemateHandlerPropertyTests"/> with mocked dependencies.</summary>
    public HousemateHandlerPropertyTests()
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Housemate>());

        _sut = new HousemateHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    // Feature: happie, Property 30: Housemate name validation
    /// <summary>
    /// For any string that is empty, whitespace-only, or longer than 50 characters (after trimming),
    /// <c>AddHousemateAsync</c> must return null.
    /// For any string of 1–50 non-whitespace characters, it must succeed.
    /// Validates: Requirements 12.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddHousemateAsync_InvalidName_ReturnsNull()
    {
        return Prop.ForAll(
            InvalidHousemateNameArb(),
            async name =>
            {
                var householdId = Guid.NewGuid();

                // Act.
                var result = await _sut.AddHousemateAsync(householdId, name);

                // Assert.
                return (result == null)
                    .Label($"Expected null for invalid name '{name}' (length after trim: {name.Trim().Length})");
            });
    }

    // Feature: happie, Property 30: Housemate name validation
    /// <summary>
    /// For any string of 1–50 non-whitespace characters, <c>AddHousemateAsync</c> must succeed and return a non-null result.
    /// Validates: Requirements 12.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddHousemateAsync_ValidName_ReturnsNonNull()
    {
        return Prop.ForAll(
            ValidHousemateNameArb(),
            async name =>
            {
                var householdId = Guid.NewGuid();

                // Act.
                var result = await _sut.AddHousemateAsync(householdId, name);

                // Assert.
                return (result != null)
                    .Label($"Expected non-null result for valid name '{name}' (length after trim: {name.Trim().Length})");
            });
    }

    /// <summary>
    /// Generates invalid housemate names: empty strings, whitespace-only strings,
    /// or strings whose trimmed length exceeds 50 characters.
    /// </summary>
    private static Arbitrary<string> InvalidHousemateNameArb()
    {
        // Empty string.
        var emptyGen = Gen.Constant(string.Empty);

        // Whitespace-only strings (1–20 spaces/tabs).
        var whitespaceGen = Gen.Choose(1, 20)
            .SelectMany(len => Gen.Elements(' ', '\t', '\r', '\n')
                .ArrayOf(len)
                .Select(chars => new string(chars)));

        // Strings whose trimmed length is > 50 (51–100 non-whitespace chars).
        var tooLongGen = Gen.Choose(51, 100)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        var gen = Gen.OneOf(emptyGen, whitespaceGen, tooLongGen);
        return Arb.From(gen);
    }

    /// <summary>Generates valid housemate names: 1–50 non-whitespace characters.</summary>
    private static Arbitrary<string> ValidHousemateNameArb()
    {
        var gen = Gen.Choose(1, 50)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }
}
