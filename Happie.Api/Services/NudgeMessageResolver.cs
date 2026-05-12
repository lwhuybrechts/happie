using Happie.Shared.Domain;

namespace Happie.Api.Services;

/// <summary>Resolves predefined nudge message keys to localized strings.</summary>
public static class NudgeMessageResolver
{
    /// <summary>Resolves a predefined nudge message key to a localized string for the given locale and date.</summary>
    public static string Resolve(NudgeMessageKey key, Locale locale, DateOnly date)
    {
        var dateStr = locale == Locale.Nl
            ? date.ToString("d MMMM", new System.Globalization.CultureInfo("nl-NL"))
            : date.ToString("MMMM d", new System.Globalization.CultureInfo("en-US"));

        return key switch
        {
            NudgeMessageKey.PleaseAddAttendance => locale == Locale.Nl
                ? $"Vul je aanwezigheid in voor {dateStr}."
                : $"Please add your attendance for {dateStr}.",
            NudgeMessageKey.WhatWouldYouLikeToEat => locale == Locale.Nl
                ? "Wat wil je vanavond eten?"
                : "What would you like to eat tonight?",
            NudgeMessageKey.DinnerSoonWhatsYourPlan => locale == Locale.Nl
                ? "Het eten komt eraan — doe je mee?"
                : "Dinner is coming up — are you joining?",
            _ => throw new InvalidOperationException($"Unhandled {nameof(NudgeMessageKey)}: {key}"),
        };
    }
}
