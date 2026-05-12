using Happie.Api.Handlers;
using Happie.Api.Results;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Api.IntegrationTests.Infrastructure;

/// <summary>A no-op push handler used in integration tests to avoid real push dispatch.</summary>
internal class NoOpPushHandler : IPushHandler
{
    /// <inheritdoc/>
    public Task SubscribeAsync(Guid householdId, Guid housemateId, string endpoint, string p256dhKey, string authKey, Locale locale, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<NudgeResult?> NudgeAsync(Guid householdId, Guid senderHousemateId, DateOnly date, IReadOnlyList<Guid> recipientIds, NudgeMessageKey? predefinedMessageKey, string? message, CancellationToken ct = default)
        => Task.FromResult<NudgeResult?>(new NudgeResult(new List<NudgeFailureDto>()));

    /// <inheritdoc/>
    public Task SendAutoNotificationsAsync(Guid householdId, Guid actorHousemateId, DateOnly date, string changeDescription, CancellationToken ct = default)
        => Task.CompletedTask;
}
