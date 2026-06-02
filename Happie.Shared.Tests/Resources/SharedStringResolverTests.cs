using Happie.Shared.Domain;
using Happie.Shared.Resources;

namespace Happie.Shared.Tests.Resources;

/// <summary>Unit tests for <see cref="SharedStringResolver"/>.</summary>
public class SharedStringResolverTests
{
    private readonly SharedStringResolver _sut = new();

    // --- History key × locale tests (string parameters JSON overload) ---

    [Fact]
    public void Resolve_HistoryAttendanceSetEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Alice","status":"EatingIn"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's attendance set to Eating in.", result);
    }

    [Fact]
    public void Resolve_HistoryAttendanceSetDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Alice","status":"EatingIn"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Aanwezigheid van Alice ingesteld op Mee-eten.", result);
    }

    [Fact]
    public void Resolve_HistoryDishSetEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"description":"Pasta"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryDishSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Dish set to \"Pasta\".", result);
    }

    [Fact]
    public void Resolve_HistoryDishSetDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"description":"Pasta"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryDishSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Gerecht ingesteld op \"Pasta\".", result);
    }

    [Fact]
    public void Resolve_HistoryCommentSetEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Bob","text":"Sounds good"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryCommentSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Bob's comment set to \"Sounds good\".", result);
    }

    [Fact]
    public void Resolve_HistoryCommentSetDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Bob","text":"Sounds good"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryCommentSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Opmerking van Bob ingesteld op \"Sounds good\".", result);
    }

    [Fact]
    public void Resolve_HistoryCommentDeletedEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Alice"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryCommentDeleted, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's comment was deleted.", result);
    }

    [Fact]
    public void Resolve_HistoryCommentDeletedDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Alice"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryCommentDeleted, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Opmerking van Alice is verwijderd.", result);
    }

    [Fact]
    public void Resolve_HistoryChefStatusEnabledEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Alice","enabled":"true"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryChefStatusChanged, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's chef status enabled.", result);
    }

    [Fact]
    public void Resolve_HistoryChefStatusEnabledDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Alice","enabled":"true"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryChefStatusChanged, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Kookstatus van Alice is ingeschakeld.", result);
    }

    [Fact]
    public void Resolve_HistoryChefStatusDisabledEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Bob","enabled":"false"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryChefStatusChanged, parameters, Locale.En);

        // Assert.
        Assert.Equal("Bob's chef status disabled.", result);
    }

    [Fact]
    public void Resolve_HistoryChefStatusDisabledDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"name":"Bob","enabled":"false"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryChefStatusChanged, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Kookstatus van Bob is uitgeschakeld.", result);
    }

    // --- History key × locale tests (pre-parsed Dictionary overload) ---

    [Fact]
    public void Resolve_HistoryAttendanceSetWithDictionaryEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "EatingIn" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's attendance set to Eating in.", result);
    }

    [Fact]
    public void Resolve_HistoryAttendanceSetWithDictionaryDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "EatingIn" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Aanwezigheid van Alice ingesteld op Mee-eten.", result);
    }

    // --- Nudge key × locale tests ---

    [Fact]
    public void Resolve_NudgePleaseAddAttendanceDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"date":"5 juni"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.NudgePleaseAddAttendance, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Vul je aanwezigheid in voor 5 juni.", result);
    }

    [Fact]
    public void Resolve_NudgePleaseAddAttendanceEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        var parameters = """{"date":"June 5"}""";

        // Act.
        var result = _sut.Resolve(TranslationKeys.NudgePleaseAddAttendance, parameters, Locale.En);

        // Assert.
        Assert.Equal("Please add your attendance for June 5.", result);
    }

    [Fact]
    public void Resolve_NudgeWhatWouldYouLikeToEatEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.NudgeWhatWouldYouLikeToEat, (string?)null, Locale.En);

        // Assert.
        Assert.Equal("What would you like to eat tonight?", result);
    }

    [Fact]
    public void Resolve_NudgeWhatWouldYouLikeToEatDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.NudgeWhatWouldYouLikeToEat, (string?)null, Locale.Nl);

        // Assert.
        Assert.Equal("Wat wil je vanavond eten?", result);
    }

    [Fact]
    public void Resolve_NudgeDinnerSoonEnglish_ReturnsExpectedOutput()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.NudgeDinnerSoonWhatsYourPlan, (string?)null, Locale.En);

        // Assert.
        Assert.Equal("Dinner is coming up \u2014 are you joining?", result);
    }

    [Fact]
    public void Resolve_NudgeDinnerSoonDutch_ReturnsExpectedOutput()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.NudgeDinnerSoonWhatsYourPlan, (string?)null, Locale.Nl);

        // Assert.
        Assert.Equal("Het eten komt eraan \u2014 doe je mee?", result);
    }

    // --- AttendanceStatus display name resolution tests ---

    [Fact]
    public void Resolve_StatusEatingInEnglish_ReturnsLocalizedDisplayName()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "EatingIn" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's attendance set to Eating in.", result);
    }

    [Fact]
    public void Resolve_StatusEatingInDutch_ReturnsLocalizedDisplayName()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "EatingIn" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Aanwezigheid van Alice ingesteld op Mee-eten.", result);
    }

    [Fact]
    public void Resolve_StatusNotEatingInEnglish_ReturnsLocalizedDisplayName()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "NotEatingIn" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's attendance set to Not eating in.", result);
    }

    [Fact]
    public void Resolve_StatusNotEatingInDutch_ReturnsLocalizedDisplayName()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "NotEatingIn" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Aanwezigheid van Alice ingesteld op Niet mee-eten.", result);
    }

    [Fact]
    public void Resolve_StatusUnknownEnglish_ReturnsLocalizedDisplayName()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "Unknown" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("Alice's attendance set to Unknown.", result);
    }

    [Fact]
    public void Resolve_StatusUnknownDutch_ReturnsLocalizedDisplayName()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "Unknown" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.Nl);

        // Assert.
        Assert.Equal("Aanwezigheid van Alice ingesteld op Onbekend.", result);
    }

    // --- Edge case: null parameters returns template without substitution ---

    [Fact]
    public void Resolve_NullStringParameters_ReturnsTemplateWithoutSubstitution()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, (string?)null, Locale.En);

        // Assert.
        Assert.Equal("{name}'s attendance set to {status}.", result);
    }

    [Fact]
    public void Resolve_NullDictionaryParameters_ReturnsTemplateWithoutSubstitution()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, (Dictionary<string, string>?)null, Locale.En);

        // Assert.
        Assert.Equal("{name}'s attendance set to {status}.", result);
    }

    // --- Edge case: empty parameters returns template without substitution ---

    [Fact]
    public void Resolve_EmptyStringParameters_ReturnsTemplateWithoutSubstitution()
    {
        // Arrange.
        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, "", Locale.En);

        // Assert.
        Assert.Equal("{name}'s attendance set to {status}.", result);
    }

    [Fact]
    public void Resolve_EmptyDictionaryParameters_ReturnsTemplateWithoutSubstitution()
    {
        // Arrange.
        var parameters = new Dictionary<string, string>();

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal("{name}'s attendance set to {status}.", result);
    }

    // --- Edge case: malformed JSON parameters returns raw key as fallback ---

    [Fact]
    public void Resolve_MalformedJsonParameters_ReturnsRawKey()
    {
        // Arrange.
        var parameters = "not valid json";

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Equal(TranslationKeys.HistoryAttendanceSet, result);
    }

    // --- Edge case: unknown translation key returns the key itself ---

    [Fact]
    public void Resolve_UnknownTranslationKey_ReturnsKeyItself()
    {
        // Arrange.
        var parameters = """{"name":"Alice"}""";

        // Act.
        var result = _sut.Resolve("unknown_key_xyz", parameters, Locale.En);

        // Assert.
        Assert.Equal("unknown_key_xyz", result);
    }

    // --- Edge case: unknown status value passes through unchanged ---

    [Fact]
    public void Resolve_UnknownStatusValue_PassesThroughUnchanged()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "CustomStatus" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.En);

        // Assert.
        Assert.Contains("CustomStatus", result);
    }

    [Fact]
    public void Resolve_UnknownStatusValueDutch_PassesThroughUnchanged()
    {
        // Arrange.
        var parameters = new Dictionary<string, string> { ["name"] = "Alice", ["status"] = "CustomStatus" };

        // Act.
        var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, Locale.Nl);

        // Assert.
        Assert.Contains("CustomStatus", result);
    }
}
