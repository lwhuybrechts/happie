namespace Happie.Api.Results;

/// <summary>Result of a housemate timeline computation including entries and the earliest cooking date.</summary>
public record HousemateTimelineResult(
    IReadOnlyList<HousemateTimelineEntry> Entries,
    DateOnly? FirstCookedDate);
