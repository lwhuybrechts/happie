namespace Happie.Api.Results;

/// <summary>Result of a dish timeline computation including entries and the earliest cooking date.</summary>
public record DishTimelineResult(
    IReadOnlyList<DishTimelineEntry> Entries,
    DateOnly? FirstCookedDate);
