using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Domain;
using Happie.Api.Results;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Property-based tests for version reporting in <see cref="HousemateHandler"/>.</summary>
public class HousemateHandlerVersionPropertyTests
{
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<ICommentRepository> _commentRepositoryMock = new();
    private readonly HousemateHandler _handler;

    /// <summary>Initializes a new instance of <see cref="HousemateHandlerVersionPropertyTests"/> with mocked dependencies.</summary>
    public HousemateHandlerVersionPropertyTests()
    {
        _handler = new HousemateHandler(
            _housemateRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _commentRepositoryMock.Object);
    }

    // Feature: version-tracking, Property 3: Invalid version strings are rejected.
    /// <summary>
    /// For any whitespace-only string, the handler returns <see cref="ReportVersionOutcome.ValidationError"/>
    /// and does not call GetAsync or UpsertAsync on the repository.
    /// Validates: Requirements 3.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReportVersionAsync_WhitespaceOnlyVersion_ReturnsValidationError()
    {
        return Prop.ForAll(
            WhitespaceOnlyStringArb(),
            async version =>
            {
                var householdId = Guid.NewGuid();
                var housemateId = Guid.NewGuid();

                // Act.
                var outcome = await _handler.ReportVersionAsync(householdId, housemateId, version);

                // Assert outcome is ValidationError.
                var correctOutcome = (outcome == ReportVersionOutcome.ValidationError)
                    .Label($"Expected ValidationError but got {outcome} for version '{version}'");

                // Assert repository was never called.
                _housemateRepositoryMock.Verify(
                    x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _housemateRepositoryMock.Verify(
                    x => x.UpsertAsync(It.IsAny<Housemate>(), It.IsAny<CancellationToken>()),
                    Times.Never);

                return correctOutcome;
            });
    }

    /// <summary>
    /// Generates whitespace-only strings of 1–10 characters using spaces, tabs, and newlines.
    /// These strings become empty after trimming, triggering the handler's validation rejection.
    /// </summary>
    private static Arbitrary<string> WhitespaceOnlyStringArb()
    {
        var generator = Gen.Choose(1, 10)
            .SelectMany(length => Gen.Elements(' ', '\t', '\n', '\r')
                .ArrayOf(length)
                .Select(characters => new string(characters)));

        return Arb.From(generator);
    }
}
