namespace Happie.Api.Models;

/// <summary>Result returned by an update housemate operation.</summary>
public record UpdateHousemateResult(
    UpdateHousemateOutcome Outcome,
    HousemateDto? Housemate = null,
    string? ErrorMessage = null
);
