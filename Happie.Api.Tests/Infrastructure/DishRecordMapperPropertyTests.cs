using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

// Feature: dinner-time, Property 2: DishRecord mapper round-trip
/// <summary>Property-based tests for <see cref="DishRecordMapper"/> round-trip correctness.</summary>
public class DishRecordMapperPropertyTests
{
    private readonly DishRecordMapper _sut = new();

    /// <summary>
    /// For any valid DishRecord with an optional TimeOnly? dinner time field, mapping to a
    /// DishRecordEntity (via ToEntity) and back to a DishRecord (via ToModel) SHALL produce
    /// an equivalent DinnerTime value — null preserved as null, and any valid TimeOnly preserved
    /// with identical Hour and Minute.
    /// Validates: Requirements 5.3, 5.7, 5.8
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesDinnerTime()
    {
        return Prop.ForAll(
            DinnerTimeArb(),
            dinnerTime =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var date = new DateOnly(2025, 6, 15);
                var record = new DishRecord(
                    householdId,
                    date,
                    "Test dish",
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    dinnerTime,
                    DateTimeOffset.UtcNow);

                // Act.
                var entity = _sut.ToEntity(record);
                var roundTripped = _sut.ToModel(householdId, date, entity);

                // Assert.
                if (dinnerTime is null)
                    return (roundTripped.DinnerTime is null)
                        .Label("Expected null DinnerTime to be preserved as null");

                return (roundTripped.DinnerTime is not null
                        && roundTripped.DinnerTime.Value.Hour == dinnerTime.Value.Hour
                        && roundTripped.DinnerTime.Value.Minute == dinnerTime.Value.Minute)
                    .Label($"Expected TimeOnly({dinnerTime.Value.Hour}, {dinnerTime.Value.Minute}) " +
                           $"but got {roundTripped.DinnerTime}");
            });
    }

    private static Arbitrary<TimeOnly?> DinnerTimeArb()
    {
        var nullGen = Gen.Constant<TimeOnly?>(null);
        var validGen = Gen.Choose(0, 23)
            .SelectMany(hour => Gen.Choose(0, 59)
                .Select(minute => (TimeOnly?)new TimeOnly(hour, minute)));

        var gen = Gen.OneOf(nullGen, validGen);
        return Arb.From(gen);
    }
}
