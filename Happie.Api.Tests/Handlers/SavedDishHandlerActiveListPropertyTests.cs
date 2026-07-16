using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

// Feature: saved-dishes, Property 4: Active list excludes soft-deleted and is sorted alphabetically
/// <summary>
/// For any household with a mix of active and soft-deleted SavedDishes,
/// the list returned by <c>GetAllActiveAsync</c> should contain only dishes where
/// <c>IsDeleted</c> is false, and should be sorted alphabetically by description
/// (case-insensitive, ascending).
/// Validates: Requirements 3.2, 6.2, 6.3
/// </summary>
public class SavedDishHandlerActiveListPropertyTests
{
    private readonly Mock<ISavedDishRepository> _savedDishRepositoryMock = new();
    private readonly Mock<IDishRepository> _dishRepositoryMock = new();
    private readonly Mock<IDayPlanDishLinkRepository> _dayPlanDishLinkRepositoryMock = new();
    private readonly SavedDishHandler _sut;

    public SavedDishHandlerActiveListPropertyTests()
    {
        _sut = new SavedDishHandler(
            _savedDishRepositoryMock.Object,
            _dishRepositoryMock.Object,
            _dayPlanDishLinkRepositoryMock.Object,
            NullLogger<SavedDishHandler>.Instance);
    }

    [Property(MaxTest = 100)]
    public Property GetAllActiveAsync_ReturnsOnlyActiveDishes_SortedAlphabetically()
    {
        return Prop.ForAll(
            SavedDishListArb(),
            async generatedDishes =>
            {
                var householdId = Guid.NewGuid();

                _savedDishRepositoryMock
                    .Setup(x => x.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(generatedDishes);

                // Act.
                var result = await _sut.GetAllActiveAsync(householdId, CancellationToken.None);

                // Assert.
                var allExcludeDeleted = result.All(x => !x.IsDeleted)
                    .Label("All returned dishes should have IsDeleted == false");

                var expectedActiveCount = generatedDishes.Count(x => !x.IsDeleted);
                var noActiveMissing = (result.Count == expectedActiveCount)
                    .Label($"Expected {expectedActiveCount} active dishes but got {result.Count}");

                var isSortedAlphabetically = IsSortedCaseInsensitive(result)
                    .Label("Result should be sorted alphabetically by description (case-insensitive, ascending)");

                return allExcludeDeleted
                    .And(noActiveMissing)
                    .And(isSortedAlphabetically);
            });
    }

    private static bool IsSortedCaseInsensitive(IReadOnlyList<SavedDish> dishes)
    {
        for (var i = 1; i < dishes.Count; i++)
        {
            if (string.Compare(dishes[i - 1].Description, dishes[i].Description, StringComparison.OrdinalIgnoreCase) > 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Generates a list of SavedDishes with varied descriptions (including different casings)
    /// and a mix of active and soft-deleted flags.
    /// </summary>
    private static Arbitrary<List<SavedDish>> SavedDishListArb()
    {
        var descriptionGen = Gen.Choose(1, 15)
            .SelectMany(length =>
                Gen.Elements(
                    'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                    'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                    'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J')
                .ArrayOf(length)
                .Select(chars => new string(chars)));

        var isDeletedGen = Gen.Elements(true, false);

        var savedDishGen = descriptionGen
            .SelectMany(description => isDeletedGen
                .Select(isDeleted => new SavedDish(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    description,
                    isDeleted)));

        var listGen = Gen.Choose(0, 20)
            .SelectMany(count => savedDishGen.ArrayOf(count)
                .Select(x => x.ToList()));

        return Arb.From(listGen);
    }
}
