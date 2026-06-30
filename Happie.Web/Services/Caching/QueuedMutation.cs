namespace Happie.Web.Services.Caching;

/// <summary>Represents a queued offline mutation stored in IndexedDB for later replay.</summary>
public record QueuedMutation(
    int Id,
    string HouseholdId,
    string Method,
    string Url,
    Dictionary<string, string> Headers,
    string? Body,
    DateTimeOffset CreatedAt,
    DateOnly Date,
    string MutationType);
