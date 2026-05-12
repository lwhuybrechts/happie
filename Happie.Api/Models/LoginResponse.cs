namespace Happie.Api.Models;

/// <summary>Response body for a successful login.</summary>
public record LoginResponse(
    string Token,
    IReadOnlyList<HousemateDto> Housemates);
