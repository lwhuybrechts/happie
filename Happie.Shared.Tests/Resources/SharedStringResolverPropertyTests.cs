using System.Globalization;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Domain;
using Happie.Shared.Resources;

namespace Happie.Shared.Tests.Resources;

/// <summary>Property-based tests for <see cref="SharedStringResolver"/>.</summary>
public class SharedStringResolverPropertyTests
{
    private readonly SharedStringResolver _sut = new();

    // Feature: happie, Property 2: Resolution produces fully-substituted strings
    /// <summary>
    /// For any known translation key, any parameters dictionary containing all required placeholder
    /// values (with non-empty string values), and any supported Locale, the SharedStringResolver.Resolve
    /// method SHALL return a non-empty string that contains no unresolved {placeholder} tokens.
    /// Validates: Requirements 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_KnownKeyWithValidParameters_ProducesFullySubstitutedString()
    {
        return Prop.ForAll(
            KnownKeyWithParametersArb(),
            LocaleArb(),
            (keyWithParams, locale) =>
            {
                // Arrange.
                var (translationKey, parameters) = keyWithParams;

                // Act.
                var result = _sut.Resolve(translationKey, parameters, locale);

                // Assert.
                var noTokens = !result.Contains('{') && !result.Contains('}');
                var nonEmpty = result.Length > 0;
                return (noTokens && nonEmpty)
                    .Label($"Expected non-empty result with no '{{...}}' tokens but got: '{result}'");
            });
    }

    // Feature: happie, Property 3: AttendanceStatus values resolve to localized display names
    /// <summary>
    /// For any AttendanceStatus enum value and any supported Locale, resolving a history_attendance_set
    /// entry with that status value SHALL produce a string that contains the localized display name
    /// for that status (not the raw enum member name like "EatingIn").
    /// Validates: Requirements 2.5, 8.2, 8.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_AttendanceStatus_ResolvesToLocalizedDisplayName()
    {
        return Prop.ForAll(
            AttendanceStatusArb(),
            LocaleArb(),
            Arb.From(NonEmptyNameGen()),
            (status, locale, name) =>
            {
                // Arrange.
                var parameters = new Dictionary<string, string>
                {
                    ["name"] = name,
                    ["status"] = status.ToString(),
                };

                // Act.
                var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, locale);

                // Assert.
                var localizedName = GetExpectedStatusDisplayName(status, locale);
                var containsLocalized = result.Contains(localizedName);

                // For EatingIn and NotEatingIn, the raw enum name must NOT appear.
                if (status is AttendanceStatus.EatingIn or AttendanceStatus.NotEatingIn)
                {
                    var rawEnumName = status.ToString();
                    var doesNotContainRaw = !result.Contains(rawEnumName);
                    return (containsLocalized && doesNotContainRaw)
                        .Label($"Expected '{localizedName}' and NOT '{rawEnumName}' in: '{result}'");
                }

                // For Unknown: Dutch should show "Onbekend" not "Unknown"; English shows "Unknown".
                if (status == AttendanceStatus.Unknown && locale == Locale.Nl)
                {
                    var doesNotContainEnglish = !result.Contains("Unknown");
                    return (containsLocalized && doesNotContainEnglish)
                        .Label($"Expected 'Onbekend' and NOT 'Unknown' in Dutch result: '{result}'");
                }

                return containsLocalized
                    .Label($"Expected '{localizedName}' in: '{result}'");
            });
    }

    // Feature: happie, Property 4: Unknown translation keys fall back gracefully
    /// <summary>
    /// For any string that is NOT one of the known translation keys, calling SharedStringResolver.Resolve
    /// SHALL return the raw key string itself without throwing an exception.
    /// Validates: Requirements 2.6, 7.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_UnknownTranslationKey_ReturnsRawKey()
    {
        return Prop.ForAll(
            UnknownTranslationKeyArb(),
            LocaleArb(),
            (unknownKey, locale) =>
            {
                // Arrange & Act.
                var result = _sut.Resolve(unknownKey, (Dictionary<string, string>?)null, locale);

                // Assert.
                return (result == unknownKey)
                    .Label($"Expected '{unknownKey}' but got: '{result}'");
            });
    }

    // Feature: happie, Property 5: Unknown status values pass through unchanged
    /// <summary>
    /// For any string that is NOT a valid AttendanceStatus enum member name,
    /// resolving a history_attendance_set entry with that value as the status parameter
    /// SHALL include the raw string value in the output unchanged.
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_UnknownStatusValue_PassesThroughVerbatim()
    {
        return Prop.ForAll(
            UnknownStatusArb(),
            LocaleArb(),
            (unknownStatus, locale) =>
            {
                // Arrange.
                var parameters = new Dictionary<string, string>
                {
                    ["name"] = "SomeName",
                    ["status"] = unknownStatus,
                };

                // Act.
                var result = _sut.Resolve(TranslationKeys.HistoryAttendanceSet, parameters, locale);

                // Assert.
                return result.Contains(unknownStatus)
                    .Label($"Expected output to contain '{unknownStatus}' verbatim but got: '{result}'");
            });
    }

    // Feature: history-translation, Property 6: Nudge messages resolve with locale-formatted dates
    /// <summary>
    /// For any valid DateOnly value (within 2020–2030) and any supported Locale,
    /// resolving the nudge_please_add_attendance key with a locale-formatted date parameter
    /// SHALL produce a string containing that formatted date string.
    /// **Validates: Requirements 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Resolve_NudgePleaseAddAttendance_ContainsLocaleFormattedDate()
    {
        return Prop.ForAll(
            DateOnlyArb(),
            LocaleArb(),
            (date, locale) =>
            {
                // Arrange.
                var formattedDate = FormatDateForLocale(date, locale);
                var parameters = new Dictionary<string, string>
                {
                    ["date"] = formattedDate,
                };

                // Act.
                var result = _sut.Resolve(TranslationKeys.NudgePleaseAddAttendance, parameters, locale);

                // Assert.
                return result.Contains(formattedDate)
                    .Label($"Expected output to contain '{formattedDate}' but got: '{result}'");
            });
    }

    private static string FormatDateForLocale(DateOnly date, Locale locale) =>
        locale switch
        {
            Locale.Nl => date.ToString("d MMMM", new CultureInfo("nl-NL")),
            Locale.En => date.ToString("MMMM d", new CultureInfo("en-US")),
            _ => throw new InvalidOperationException($"Unhandled {nameof(Locale)}: {locale}"),
        };

    private static Arbitrary<DateOnly> DateOnlyArb()
    {
        var minDay = new DateOnly(2020, 1, 1).DayNumber;
        var maxDay = new DateOnly(2030, 12, 31).DayNumber;

        var gen = Gen.Choose(minDay, maxDay).Select(DateOnly.FromDayNumber);
        return Arb.From(gen);
    }

    private static Arbitrary<string> UnknownStatusArb()
    {
        var knownStatuses = new HashSet<string> { "Unknown", "EatingIn", "NotEatingIn" };

        var gen = Gen.Choose(1, 30)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z',
                             'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                             'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                             'U', 'V', 'W', 'X', 'Y', 'Z',
                             '0', '1', '2', '3', '4', '5', '6', '7', '8', '9')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)))
            .Where(x => !knownStatuses.Contains(x));

        return Arb.From(gen);
    }

    private static Arbitrary<Locale> LocaleArb()
    {
        return Arb.From(Gen.Elements(Locale.Nl, Locale.En));
    }

    private static Arbitrary<AttendanceStatus> AttendanceStatusArb()
    {
        return Arb.From(Gen.Elements(AttendanceStatus.Unknown, AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn));
    }

    private static Gen<string> NonEmptyNameGen()
    {
        return Gen.Choose(1, 20)
            .SelectMany(len =>
                Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                             'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                             'U', 'V', 'W', 'X', 'Y', 'Z',
                             'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)));
    }

    private static Arbitrary<(string Key, Dictionary<string, string> Parameters)> KnownKeyWithParametersArb()
    {
        var nameGen = NonEmptyNameGen();
        var textGen = NonEmptyNameGen();
        var descriptionGen = NonEmptyNameGen();
        var dateGen = DateOnlyArb().Generator.SelectMany(date =>
            Gen.Elements(Locale.Nl, Locale.En).Select(locale => FormatDateForLocale(date, locale)));

        var historyAttendanceGen = nameGen.Select(name => (
            Key: TranslationKeys.HistoryAttendanceSet,
            Parameters: new Dictionary<string, string>
            {
                ["name"] = name,
                ["status"] = "EatingIn",
            }));

        var historyDishGen = descriptionGen.Select(description => (
            Key: TranslationKeys.HistoryDishSet,
            Parameters: new Dictionary<string, string>
            {
                ["description"] = description,
            }));

        var historyCommentSetGen = nameGen.SelectMany(name =>
            textGen.Select(text => (
                Key: TranslationKeys.HistoryCommentSet,
                Parameters: new Dictionary<string, string>
                {
                    ["name"] = name,
                    ["text"] = text,
                })));

        var historyCommentDeletedGen = nameGen.Select(name => (
            Key: TranslationKeys.HistoryCommentDeleted,
            Parameters: new Dictionary<string, string>
            {
                ["name"] = name,
            }));

        var historyChefStatusGen = nameGen.SelectMany(name =>
            Gen.Elements("true", "false").Select(enabled => (
                Key: TranslationKeys.HistoryChefStatusChanged,
                Parameters: new Dictionary<string, string>
                {
                    ["name"] = name,
                    ["enabled"] = enabled,
                })));

        var nudgePleaseAddGen = nameGen.SelectMany(name =>
            dateGen.Select(date => (
                Key: TranslationKeys.NudgePleaseAddAttendance,
                Parameters: new Dictionary<string, string>
                {
                    ["name"] = name,
                    ["date"] = date,
                })));

        var nudgeWhatWouldYouLikeGen = Gen.Constant((
            Key: TranslationKeys.NudgeWhatWouldYouLikeToEat,
            Parameters: new Dictionary<string, string>()));

        var nudgeDinnerSoonGen = Gen.Constant((
            Key: TranslationKeys.NudgeDinnerSoonWhatsYourPlan,
            Parameters: new Dictionary<string, string>()));

        var combined = Gen.OneOf(
            historyAttendanceGen,
            historyDishGen,
            historyCommentSetGen,
            historyCommentDeletedGen,
            historyChefStatusGen,
            nudgePleaseAddGen,
            nudgeWhatWouldYouLikeGen,
            nudgeDinnerSoonGen);

        return Arb.From(combined);
    }

    private static Arbitrary<string> UnknownTranslationKeyArb()
    {
        var allKnownKeys = new HashSet<string>
        {
            "history_attendance_set",
            "history_dish_set",
            "history_comment_set",
            "history_comment_deleted",
            "history_chef_status_changed",
            "nudge_please_add_attendance",
            "nudge_what_would_you_like_to_eat",
            "nudge_dinner_soon_whats_your_plan",
            "status_Unknown",
            "status_EatingIn",
            "status_NotEatingIn",
            "enabled_true",
            "enabled_false",
        };

        var gen = Gen.Choose(1, 30)
            .SelectMany(len =>
                Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                             'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                             'u', 'v', 'w', 'x', 'y', 'z',
                             'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                             'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                             'U', 'V', 'W', 'X', 'Y', 'Z',
                             '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                             '_')
                    .ArrayOf(len)
                    .Select(chars => new string(chars)))
            .Where(x => !allKnownKeys.Contains(x));

        return Arb.From(gen);
    }

    private static string GetExpectedStatusDisplayName(AttendanceStatus status, Locale locale) =>
        (status, locale) switch
        {
            (AttendanceStatus.EatingIn, Locale.Nl) => "Mee-eten",
            (AttendanceStatus.EatingIn, Locale.En) => "Eating in",
            (AttendanceStatus.NotEatingIn, Locale.Nl) => "Niet mee-eten",
            (AttendanceStatus.NotEatingIn, Locale.En) => "Not eating in",
            (AttendanceStatus.Unknown, Locale.Nl) => "Onbekend",
            (AttendanceStatus.Unknown, Locale.En) => "Unknown",
            _ => throw new InvalidOperationException($"Unhandled status/locale: {status}/{locale}"),
        };
}
