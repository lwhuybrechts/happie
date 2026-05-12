namespace Happie.Api.Models;

/// <summary>Housemate data returned as part of the login response.</summary>
public record HousemateDto(Guid Id, string Name, string Color);
