using System.Text.Json;
using Happie.Api.Handlers;
using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Services;
using Happie.Shared.Contracts;
using Happie.Api.Domain;
using Happie.Shared.Domain;
using Happie.Shared.Resources;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Happie.Api.Tests.Handlers;

/// <summary>Unit tests for <see cref="PushHandler"/>.</summary>
public class PushHandlerTests
{
    private readonly Mock<IPushSubscriptionRepository> _pushSubscriptionRepositoryMock = new();
    private readonly Mock<IHousemateRepository> _housemateRepositoryMock = new();
    private readonly Mock<IPushNotificationService> _pushNotificationServiceMock = new();
    private readonly SharedStringResolver _sharedStringResolver = new();
    private readonly PushHandler _sut;

    /// <summary>Initializes a new instance of <see cref="PushHandlerTests"/> with mocked dependencies.</summary>
    public PushHandlerTests()
    {
        _sut = new PushHandler(
            _pushSubscriptionRepositoryMock.Object,
            _housemateRepositoryMock.Object,
            _pushNotificationServiceMock.Object,
            _sharedStringResolver,
            NullLogger<PushHandler>.Instance);
    }

    /// <summary>A custom nudge message of exactly 20 characters is accepted.</summary>
    [Fact]
    public async Task NudgeAsync_MessageExactly20Chars_ReturnsResult()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var message = new string('A', 20);

        SetupGetHousemate(householdId, senderHousemateId, CreateHousemate(householdId, senderHousemateId, "Alice"));
        SetupGetSubscription(householdId, recipientId, null);

        // Act.
        var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, null, message);

        // Assert.
        Assert.NotNull(result);
    }

    /// <summary>A custom nudge message of 21 characters is rejected (returns null).</summary>
    [Fact]
    public async Task NudgeAsync_Message21Chars_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var message = new string('A', 21);

        // Act.
        var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, null, message);

        // Assert.
        Assert.Null(result);
    }

    /// <summary>When both predefinedMessageKey and message are set, the nudge is rejected.</summary>
    [Fact]
    public async Task NudgeAsync_BothPredefinedAndMessage_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        // Act.
        var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, NudgeMessageKey.PleaseAddAttendance, "Hello");

        // Assert.
        Assert.Null(result);
    }

    /// <summary>When neither predefinedMessageKey nor message is set, the nudge is rejected.</summary>
    [Fact]
    public async Task NudgeAsync_NeitherPredefinedNorMessage_ReturnsNull()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        // Act.
        var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, null, null);

        // Assert.
        Assert.Null(result);
    }

    /// <summary>Auto-notification is not sent to the housemate who made the change.</summary>
    [Fact]
    public async Task SendAutoNotificationsAsync_ActorExcludedFromRecipients_ActorDoesNotReceiveNotification()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var otherHousemateId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        var actorSubscription = CreateSubscription(householdId, actorId);
        var otherSubscription = CreateSubscription(householdId, otherHousemateId);

        SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { actorSubscription, otherSubscription });
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            CreateHousemate(householdId, actorId, "Alice"),
            CreateHousemate(householdId, otherHousemateId, "Bob"),
        });
        SetupPushSend();

        // Act.
        await _sut.SendAutoNotificationsAsync(householdId, actorId, date, TranslationKeys.HistoryAttendanceSet, """{"name":"Alice","status":"EatingIn"}""");

        // Assert.
        _pushNotificationServiceMock.Verify(
            x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == actorId), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pushNotificationServiceMock.Verify(
            x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == otherHousemateId), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>When push service throws, SendAutoNotificationsAsync does not propagate the exception.</summary>
    [Fact]
    public async Task SendAutoNotificationsAsync_PushServiceThrows_DoesNotThrow()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        var recipientSubscription = CreateSubscription(householdId, recipientId);

        SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { recipientSubscription });
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            CreateHousemate(householdId, actorId, "Alice"),
            CreateHousemate(householdId, recipientId, "Bob"),
        });

        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Push service unavailable."));

        // Act.
        // Should not throw even though push service fails.
        var exception = await Record.ExceptionAsync(() =>
            _sut.SendAutoNotificationsAsync(householdId, actorId, date, TranslationKeys.HistoryAttendanceSet, """{"name":"Alice","status":"EatingIn"}"""));

        // Assert.
        Assert.Null(exception);
    }

    /// <summary>Auto-notifications resolve per-recipient locale using SharedStringResolver.</summary>
    [Fact]
    public async Task SendAutoNotificationsAsync_TwoRecipients_ResolvesPerRecipientLocale()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var dutchRecipientId = Guid.NewGuid();
        var englishRecipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var translationKey = TranslationKeys.HistoryAttendanceSet;
        var parameters = """{"name":"Alice","status":"EatingIn"}""";

        var dutchSubscription = CreateSubscription(householdId, dutchRecipientId, Locale.Nl);
        var englishSubscription = CreateSubscription(householdId, englishRecipientId, Locale.En);

        SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { dutchSubscription, englishSubscription });
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            CreateHousemate(householdId, actorId, "Alice"),
            CreateHousemate(householdId, dutchRecipientId, "Bob"),
            CreateHousemate(householdId, englishRecipientId, "Charlie"),
        });
        SetupPushSend();

        var capturedPayloads = new Dictionary<Guid, string>();
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.PushSubscription, string, CancellationToken>((subscription, payload, _) =>
                capturedPayloads[subscription.HousemateId] = payload)
            .Returns(Task.CompletedTask);

        // Act.
        await _sut.SendAutoNotificationsAsync(householdId, actorId, date, translationKey, parameters);

        // Assert.
        Assert.Contains("Mee-eten", capturedPayloads[dutchRecipientId]);
        Assert.Contains("Eating in", capturedPayloads[englishRecipientId]);
    }

    /// <summary>Auto-notifications resolve GUID-based "name" parameter to current housemate name in payload.</summary>
    [Fact]
    public async Task SendAutoNotificationsAsync_GuidNameParameter_ResolvesToCurrentName()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var subjectHousemateId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var translationKey = TranslationKeys.HistoryAttendanceSet;
        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = subjectHousemateId.ToString(),
            ["status"] = "EatingIn"
        });

        var recipientSubscription = CreateSubscription(householdId, recipientId, Locale.En);

        SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { recipientSubscription });
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            CreateHousemate(householdId, actorId, "Alice"),
            CreateHousemate(householdId, subjectHousemateId, "Bob"),
            CreateHousemate(householdId, recipientId, "Charlie"),
        });

        var capturedPayload = string.Empty;
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.PushSubscription, string, CancellationToken>((_, payload, _) =>
                capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act.
        await _sut.SendAutoNotificationsAsync(householdId, actorId, date, translationKey, parameters);

        // Assert.
        Assert.Contains("Bob", capturedPayload);
        Assert.DoesNotContain(subjectHousemateId.ToString(), capturedPayload);
    }

    /// <summary>Auto-notification excludes the actor from recipients.</summary>
    [Fact]
    public async Task SendAutoNotificationsAsync_ExcludesActor_DoesNotSendToActor()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var translationKey = TranslationKeys.HistoryDishSet;
        var parameters = """{"description":"Pizza"}""";

        var actorSubscription = CreateSubscription(householdId, actorId, Locale.Nl);
        var recipientSubscription = CreateSubscription(householdId, recipientId, Locale.En);

        SetupGetAllSubscriptions(householdId, new List<Domain.PushSubscription> { actorSubscription, recipientSubscription });
        SetupGetAllHousemates(householdId, new List<Housemate>
        {
            CreateHousemate(householdId, actorId, "Bob"),
            CreateHousemate(householdId, recipientId, "Charlie"),
        });
        SetupPushSend();

        // Act.
        await _sut.SendAutoNotificationsAsync(householdId, actorId, date, translationKey, parameters);

        // Assert.
        _pushNotificationServiceMock.Verify(
            x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == actorId), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pushNotificationServiceMock.Verify(
            x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == recipientId), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Predefined nudge message resolves using SharedStringResolver per recipient locale.</summary>
    [Fact]
    public async Task NudgeAsync_PredefinedKey_ResolvesUsingSharedResolver()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var dutchRecipientId = Guid.NewGuid();
        var englishRecipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 3, 15);

        SetupGetHousemate(householdId, senderHousemateId, CreateHousemate(householdId, senderHousemateId, "Alice"));
        SetupGetSubscription(householdId, dutchRecipientId, CreateSubscription(householdId, dutchRecipientId, Locale.Nl));
        SetupGetSubscription(householdId, englishRecipientId, CreateSubscription(householdId, englishRecipientId, Locale.En));

        var capturedPayloads = new Dictionary<Guid, string>();
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.PushSubscription, string, CancellationToken>((subscription, payload, _) =>
                capturedPayloads[subscription.HousemateId] = payload)
            .Returns(Task.CompletedTask);

        // Act.
        await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { dutchRecipientId, englishRecipientId }, NudgeMessageKey.PleaseAddAttendance, null);

        // Assert.
        Assert.Contains("Vul je aanwezigheid in voor 15 maart", capturedPayloads[dutchRecipientId]);
        Assert.Contains("Please add your attendance for March 15", capturedPayloads[englishRecipientId]);
    }

    /// <summary>Custom nudge message is sent as-is without resolution via SharedStringResolver.</summary>
    [Fact]
    public async Task NudgeAsync_CustomMessage_DoesNotResolveViaSharedResolver()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);
        var customMessage = "Kom je eten?";

        SetupGetHousemate(householdId, senderHousemateId, CreateHousemate(householdId, senderHousemateId, "Alice"));
        SetupGetSubscription(householdId, recipientId, CreateSubscription(householdId, recipientId, Locale.En));

        var capturedPayload = string.Empty;
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.PushSubscription, string, CancellationToken>((_, payload, _) =>
                capturedPayload = payload)
            .Returns(Task.CompletedTask);

        // Act.
        await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { recipientId }, null, customMessage);

        // Assert.
        Assert.Contains("Kom je eten?", capturedPayload);
    }

    /// <summary>When push service throws during nudge, the failure is recorded but delivery continues to other recipients.</summary>
    [Fact]
    public async Task NudgeAsync_PushServiceThrowsForOneRecipient_OtherRecipientsStillReceiveNotification()
    {
        // Arrange.
        var householdId = Guid.NewGuid();
        var senderHousemateId = Guid.NewGuid();
        var failingRecipientId = Guid.NewGuid();
        var successRecipientId = Guid.NewGuid();
        var date = new DateOnly(2025, 7, 15);

        SetupGetHousemate(householdId, senderHousemateId, CreateHousemate(householdId, senderHousemateId, "Alice"));
        SetupGetSubscription(householdId, failingRecipientId, CreateSubscription(householdId, failingRecipientId));
        SetupGetSubscription(householdId, successRecipientId, CreateSubscription(householdId, successRecipientId));

        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == failingRecipientId), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Push failed."));

        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == successRecipientId), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act.
        var result = await _sut.NudgeAsync(householdId, senderHousemateId, date, new[] { failingRecipientId, successRecipientId }, NudgeMessageKey.PleaseAddAttendance, null);

        // Assert.
        Assert.NotNull(result);
        Assert.Single(result!.Failures);
        Assert.Equal(failingRecipientId, result.Failures[0].RecipientHousemateId);

        _pushNotificationServiceMock.Verify(
            x => x.SendAsync(It.Is<Domain.PushSubscription>(s => s.HousemateId == successRecipientId), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupGetHousemate(Guid householdId, Guid housemateId, Housemate? returns)
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

    private void SetupGetAllHousemates(Guid householdId, List<Housemate> returns)
    {
        _housemateRepositoryMock
            .Setup(x => x.GetAllAsync(householdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
    }

    private void SetupPushSend()
    {
        _pushNotificationServiceMock
            .Setup(x => x.SendAsync(It.IsAny<Domain.PushSubscription>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Housemate CreateHousemate(Guid householdId, Guid housemateId, string name) =>
        new(housemateId, householdId, name, HousemateColors.Palette[0], false);

    private static Domain.PushSubscription CreateSubscription(Guid householdId, Guid housemateId) =>
        new(housemateId, householdId, "https://push.example.com/endpoint", "p256dhKey", "authKey", Locale.Nl);

    private static Domain.PushSubscription CreateSubscription(Guid householdId, Guid housemateId, Locale locale) =>
        new(housemateId, householdId, "https://push.example.com/endpoint", "p256dhKey", "authKey", locale);
}
