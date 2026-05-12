using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Services;
using Happie.Shared.Domain;
using Happie.Api.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Property-based tests for <see cref="PushHandler"/>.</summary>
public class PushHandlerPropertyTests
{
    private readonly Mock<IPushSubscriptionRepository> _pushSubscriptionRepositoryMock = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepositoryMock = new();
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IPushNotificationService> _pushNotificationServiceMock = new();
    private readonly PushHandler _sut;

    /// <summary>Initializes a new instance of <see cref="PushHandlerPropertyTests"/> with mocked dependencies.</summary>
    public PushHandlerPropertyTests()
    {
        _sut = new PushHandler(
            _pushSubscriptionRepositoryMock.Object,
            _attendanceRepositoryMock.Object,
            _housemateRepositoryMock.Object,
            _pushNotificationServiceMock.Object,
            NullLogger<PushHandler>.Instance);
    }

    // Feature: happie, Property 14: Nudge payload contains sender and date
    /// <summary>
    /// For any nudge request, the push notification payload sent to recipients must contain
    /// the sender's name and the date for which attendance is being requested.
    /// Validates: Requirements 7.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NudgeAsync_Payload_ContainsSenderNameAndDate()
    {
        return Prop.ForAll(
            SenderNameArb(),
            DateOnlyArb(),
            async (senderName, date) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var senderHousemateId = Guid.NewGuid();
                var recipientId = Guid.NewGuid();

                var capturedPayloads = new List<string>();

                SetupGetAttendanceByDate(householdId, date, new List<AttendanceRecord>());
                SetupGetHousemate(householdId, senderHousemateId, CreateHousemate(householdId, senderHousemateId, senderName));
                SetupGetSubscription(householdId, recipientId, CreateSubscription(householdId, recipientId));
                SetupPushSendCapture(capturedPayloads);

                // Act.
                var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, NudgeMessageKey.PleaseAddAttendance, null);

                // Assert.
                if (result is null)
                    return false.Label("NudgeAsync returned null unexpectedly.");

                if (capturedPayloads.Count == 0)
                    return true.Label("No subscription found — payload check skipped.");

                var payload = capturedPayloads[0];
                var dateStr = date.ToString("yyyy-MM-dd");

                return (payload.Contains(senderName) && payload.Contains(dateStr))
                    .Label($"Expected payload to contain sender '{senderName}' and date '{dateStr}'. Payload: {payload}");
            });
    }

    // Feature: happie, Property 15: Nudge default recipients are housemates with unknown status
    /// <summary>
    /// For any set of housemates with mixed attendance statuses, the nudge must be rejected
    /// when any specified recipient does not have Unknown status.
    /// Validates: Requirements 7.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NudgeAsync_RecipientWithNonUnknownStatus_ReturnsNull()
    {
        return Prop.ForAll(
            NonUnknownStatusArb(),
            async status =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var senderHousemateId = Guid.NewGuid();
                var recipientId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                SetupGetAttendanceByDate(householdId, date, new List<AttendanceRecord>
                {
                    new(householdId, recipientId, date, status),
                });

                // Act.
                var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, NudgeMessageKey.PleaseAddAttendance, null);

                // Assert.
                return (result == null)
                    .Label($"Expected null when recipient has status {status}.");
            });
    }

    // Feature: happie, Property 17: Auto-notification recipients exclude the sender
    /// <summary>
    /// For any day plan change, the set of housemates who receive an automatic push notification
    /// must be exactly all active housemates except the one who made the change.
    /// Validates: Requirements 10.1, 10.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SendAutoNotificationsAsync_ActorExcluded_FromRecipients()
    {
        return Prop.ForAll(
            HousemateCountArb(),
            async housemateCount =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actorId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                var otherIds = Enumerable.Range(0, housemateCount).Select(_ => Guid.NewGuid()).ToList();
                var allSubscriptions = otherIds
                    .Select(x => CreateSubscription(householdId, x))
                    .Append(CreateSubscription(householdId, actorId))
                    .ToList();

                var notifiedIds = new List<Guid>();

                SetupGetAllSubscriptions(householdId, allSubscriptions);
                SetupGetHousemate(householdId, actorId, CreateHousemate(householdId, actorId, "Alice"));
                SetupPushSendTrack(notifiedIds);

                // Act.
                await _sut.SendAutoNotificationsAsync(householdId, actorId, date, "Alice's attendance set to EatingIn.");

                // Assert.
                var actorNotified = notifiedIds.Contains(actorId);
                var allOthersNotified = otherIds.All(x => notifiedIds.Contains(x));

                return (!actorNotified && allOthersNotified)
                    .Label($"Actor notified: {actorNotified}, all others notified: {allOthersNotified}.");
            });
    }

    // Feature: happie, Property 18: Auto-notification payload contains actor, date, and change description
    /// <summary>
    /// For any day plan change event, the automatic notification payload must contain
    /// the actor's name, the affected date, and a description of what was changed.
    /// Validates: Requirements 10.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SendAutoNotificationsAsync_Payload_ContainsActorDateAndDescription()
    {
        return Prop.ForAll(
            SenderNameArb(),
            DateOnlyArb(),
            ChangeDescriptionArb(),
            async (actorName, date, changeDescription) =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actorId = Guid.NewGuid();
                var recipientId = Guid.NewGuid();

                var capturedPayloads = new List<string>();

                SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { CreateSubscription(householdId, recipientId) });
                SetupGetHousemate(householdId, actorId, CreateHousemate(householdId, actorId, actorName));
                SetupPushSendCapture(capturedPayloads);

                // Act.
                await _sut.SendAutoNotificationsAsync(householdId, actorId, date, changeDescription);

                // Assert.
                if (capturedPayloads.Count == 0)
                    return false.Label("No payload was sent.");

                var payload = capturedPayloads[0];
                var dateStr = date.ToString("yyyy-MM-dd");

                return (payload.Contains(actorName) && payload.Contains(dateStr) && payload.Contains(changeDescription))
                    .Label($"Expected payload to contain actor '{actorName}', date '{dateStr}', and description '{changeDescription}'. Payload: {payload}");
            });
    }

    // Feature: happie, Property 19: Push failure does not interrupt save
    /// <summary>
    /// For any day plan save operation where the push notification dispatch throws an exception,
    /// the save operation should still complete successfully.
    /// Validates: Requirements 10.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SendAutoNotificationsAsync_PushThrows_DoesNotPropagateException()
    {
        return Prop.ForAll(
            ChangeDescriptionArb(),
            async changeDescription =>
            {
                // Arrange.
                var householdId = Guid.NewGuid();
                var actorId = Guid.NewGuid();
                var recipientId = Guid.NewGuid();
                var date = new DateOnly(2025, 7, 15);

                SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { CreateSubscription(householdId, recipientId) });
                SetupGetHousemate(householdId, actorId, CreateHousemate(householdId, actorId, "Alice"));
                SetupPushSendThrows();

                // Act.
                Exception? caughtException = null;
                try
                {
                    await _sut.SendAutoNotificationsAsync(householdId, actorId, date, changeDescription);
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }

                // Assert.
                return (caughtException == null)
                    .Label($"Expected no exception but got: {caughtException?.Message}");
            });
    }

    private void SetupGetAttendanceByDate(Guid householdId, DateOnly date, List<AttendanceRecord> returns)
    {
        _attendanceRepositoryMock
            .Setup(x => x.GetByDateAsync(householdId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetHousemate(Guid householdId, Guid housemateId, Housemate returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetSubscription(Guid householdId, Guid housemateId, Domain.PushSubscription? returns)
    {
        _pushSubscriptionRepositoryMock
            .Setup(x => x.GetAsync(householdId, housemateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupGetAllSubscriptions(Guid householdId, List<Domain.PushSubscription> returns)
    {
        _pushSubscriptionRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupPushSendCapture(List<string> capturedPayloads)
    {
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.PushSubscription, string, CancellationToken>((_, payload, _) => capturedPayloads.Add(payload))
            .Returns(Task.CompletedTask);
    }

    private void SetupPushSendTrack(List<Guid> notifiedIds)
    {
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.PushSubscription, string, CancellationToken>((sub, _, _) => notifiedIds.Add(sub.HousemateId))
            .Returns(Task.CompletedTask);
    }

    private void SetupPushSendThrows()
    {
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Push service unavailable."));
    }

    private static Arbitrary<string> SenderNameArb()
    {
        var gen = Gen.Choose(1, 30)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    private static Arbitrary<DateOnly> DateOnlyArb()
    {
        var minDay = new DateOnly(2025, 1, 1).DayNumber;
        var maxDay = new DateOnly(2026, 12, 31).DayNumber;

        var gen = Gen.Choose(minDay, maxDay).Select(DateOnly.FromDayNumber);
        return Arb.From(gen);
    }

    private static Arbitrary<string> ChangeDescriptionArb()
    {
        var gen = Gen.Choose(1, 50)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));

        return Arb.From(gen);
    }

    private static Arbitrary<AttendanceStatus> NonUnknownStatusArb()
    {
        var gen = Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn);
        return Arb.From(gen);
    }

    private static Arbitrary<int> HousemateCountArb()
    {
        return Arb.From(Gen.Choose(0, 5));
    }

    private static Housemate CreateHousemate(Guid householdId, Guid housemateId, string name) =>
        new(housemateId, householdId, name, HousemateColors.Palette[0], false);

    private static Domain.PushSubscription CreateSubscription(Guid householdId, Guid housemateId) =>
        new(housemateId, householdId, "https://push.example.com/endpoint", "p256dhKey", "authKey", Locale.Nl);
}
