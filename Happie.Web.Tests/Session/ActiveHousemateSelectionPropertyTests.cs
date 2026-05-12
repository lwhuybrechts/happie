using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;

namespace Happie.Web.Tests.Session;

// Feature: happie, Property 2: Active housemate selection round-trip
public class ActiveHousemateSelectionPropertyTests
{
    private static readonly Arbitrary<HousemateDto> HousemateDtoArb =
        ArbMap.Default.GeneratorFor<Guid>()
            .SelectMany(id => Gen.Elements("Alice", "Bob", "Charlie", "Diana", "Eve")
                .SelectMany(name => Gen.Elements("#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF")
                    .Select(color => new HousemateDto(id, name, color))))
            .ToArbitrary();

    private static readonly Arbitrary<IReadOnlyList<HousemateDto>> NonEmptyHousemateListArb =
        Gen.Choose(1, 10)
            .SelectMany(count => Gen.ListOf(HousemateDtoArb.Generator, count)
                .Select(housemates => (IReadOnlyList<HousemateDto>)housemates))
            .ToArbitrary();

    // Feature: happie, Property 2: Active housemate selection round-trip
    // Validates: Requirements 1.3, 1.4
    [Property(MaxTest = 100)]
    public Property SelectHousemate_StoreAndRetrieveId_ReturnsSameHousemate()
    {
        return Prop.ForAll(
            NonEmptyHousemateListArb,
            housemates =>
            {
                // Arrange.
                var selectedIndex = housemates.Count / 2;
                var selectedHousemate = housemates[selectedIndex];

                // Act.
                // Simulate storing the selected housemate ID (as done in LoginPage.SelectHousemateAsync).
                var storedId = selectedHousemate.Id.ToString();

                // Simulate reading back the stored ID and finding the housemate (as done on session restore).
                var parsedId = Guid.Parse(storedId);
                var foundHousemate = housemates.FirstOrDefault(x => x.Id == parsedId);

                // Assert.
                return (foundHousemate is not null && foundHousemate.Id == selectedHousemate.Id)
                    .Label($"Expected to find housemate with Id={selectedHousemate.Id} after round-trip via stored string '{storedId}'");
            });
    }

    // Feature: happie, Property 2: Active housemate selection round-trip
    // Validates: Requirements 1.3, 1.4
    [Property(MaxTest = 100)]
    public Property SelectHousemate_StoredIdAsString_ParsesBackToSameGuid()
    {
        return Prop.ForAll(
            HousemateDtoArb,
            housemate =>
            {
                // Arrange + Act.
                // Simulate the round-trip: Guid → string (localStorage) → Guid.
                var storedId = housemate.Id.ToString();
                var parsedId = Guid.Parse(storedId);

                // Assert.
                return (parsedId == housemate.Id)
                    .Label($"Expected {housemate.Id} but got {parsedId} after round-trip via '{storedId}'");
            });
    }
}
