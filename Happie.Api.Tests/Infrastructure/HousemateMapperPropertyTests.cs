using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Domain;
using Happie.Api.Infrastructure.Mappers;

namespace Happie.Api.Tests.Infrastructure;

// Feature: version-tracking, Property 4: Mapper round-trip preserves AppVersion.
/// <summary>Property-based tests for <see cref="HousemateMapper"/> round-trip correctness of AppVersion.</summary>
public class HousemateMapperPropertyTests
{
    private readonly HousemateMapper _sut = new();

    /// <summary>
    /// For any valid Housemate domain record (with AppVersion set to null or any string of 1–20 characters),
    /// mapping to HousemateEntity and back via the mapper should produce an identical AppVersion value.
    /// Validates: Requirements 4.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToEntity_ThenToModel_PreservesAppVersion()
    {
        return Prop.ForAll(
            HousemateArb(),
            housemate =>
            {
                // Arrange.
                // Housemate is already constructed by the arbitrary.

                // Act.
                var entity = _sut.ToEntity(housemate);
                var roundTripped = _sut.ToModel(housemate.HouseholdId, entity);

                // Assert.
                return (roundTripped.AppVersion == housemate.AppVersion)
                    .Label($"AppVersion mismatch: expected '{housemate.AppVersion}' but got '{roundTripped.AppVersion}'");
            });
    }

    private static Arbitrary<Housemate> HousemateArb()
    {
        var guidGen = ArbMap.Default.GeneratorFor<Guid>();

        // Printable ASCII characters for name and color.
        var printableCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var nameGen = Gen.Choose(1, 20)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var colorGen = Gen.Choose(1, 7)
            .SelectMany(length => Gen.ListOf(printableCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var isDeletedGen = Gen.Elements(true, false);
        var sortOrderGen = Gen.Choose(0, 100);

        // AppVersion: null or a random string of 1–20 chars from [a-zA-Z0-9.].
        var versionCharGen = Gen.Elements(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.".ToCharArray());

        var versionStringGen = Gen.Choose(1, 20)
            .SelectMany(length => Gen.ListOf(versionCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var appVersionGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            versionStringGen.Select(x => (string?)x));

        var gen = guidGen.SelectMany(id =>
            guidGen.SelectMany(householdId =>
                nameGen.SelectMany(name =>
                    colorGen.SelectMany(color =>
                        isDeletedGen.SelectMany(isDeleted =>
                            sortOrderGen.SelectMany(sortOrder =>
                                appVersionGen.Select(appVersion =>
                                    new Housemate(id, householdId, name, color, isDeleted, sortOrder, appVersion))))))));

        return Arb.From(gen);
    }
}
