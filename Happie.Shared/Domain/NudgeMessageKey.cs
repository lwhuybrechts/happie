namespace Happie.Shared.Domain;

/// <summary>Predefined nudge message keys resolved server-side in the recipient's locale.</summary>
public enum NudgeMessageKey
{
    /// <summary>"Please add your attendance for {date}" / "Vul je aanwezigheid in voor {datum}".</summary>
    PleaseAddAttendance,

    /// <summary>"What would you like to eat tonight?" / "Wat wil je vanavond eten?".</summary>
    WhatWouldYouLikeToEat,

    /// <summary>"Dinner is coming up — are you joining?" / "Het eten komt eraan — doe je mee?".</summary>
    DinnerSoonWhatsYourPlan,

    /// <summary>A custom message provided directly by the sender (max 20 chars).</summary>
    Custom,
}
