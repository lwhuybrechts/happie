using System.Net;
using Bunit;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Components;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Happie.Web.Tests.Components;

// Feature: day-plan-redesign, Property 5: Attendance status button highlight mapping
public class AttendanceSectionPropertyTests
{
    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
            .ToArbitrary();

    private static readonly Arbitrary<AttendanceDto> AttendanceDtoArb =
        ArbMap.Default.GeneratorFor<Guid>()
            .SelectMany(id => StatusArb.Generator
                .SelectMany(status => Gen.Elements(HousemateColors.Palette.ToArray())
                    .Select(color => new AttendanceDto(id, $"Housemate-{id.ToString()[..8]}", color, status, false))))
            .ToArbitrary();

    // Feature: day-plan-redesign, Property 5: Attendance status button highlight mapping
    // Validates: Requirements 13.4, 13.5, 13.6
    [Property(MaxTest = 100)]
    public Property Render_AttendanceStatus_HighlightsCorrectButton()
    {
        return Prop.ForAll(
            AttendanceDtoArb,
            attendance =>
            {
                using var context = CreateBunitContext();

                var attendanceList = new List<AttendanceDto> { attendance };

                var cut = context.Render<AttendanceSection>(parameters => parameters
                    .Add(x => x.Date, "2025-01-15")
                    .Add(x => x.Attendance, attendanceList));

                var buttons = cut.FindAll(".attendance-section__btn");

                // There should be exactly 3 buttons per housemate.
                if (buttons.Count != 3)
                    return false.Label($"Expected 3 buttons, got {buttons.Count}");

                var eatingInButton = buttons[0];
                var unknownButton = buttons[1];
                var notEatingInButton = buttons[2];

                bool highlightCorrect = attendance.Status switch
                {
                    AttendanceStatus.EatingIn =>
                        eatingInButton.ClassList.Contains("attendance-section__btn--eating-in")
                        && !unknownButton.ClassList.Contains("attendance-section__btn--unknown")
                        && !notEatingInButton.ClassList.Contains("attendance-section__btn--not-eating-in"),
                    AttendanceStatus.Unknown =>
                        !eatingInButton.ClassList.Contains("attendance-section__btn--eating-in")
                        && unknownButton.ClassList.Contains("attendance-section__btn--unknown")
                        && !notEatingInButton.ClassList.Contains("attendance-section__btn--not-eating-in"),
                    AttendanceStatus.NotEatingIn =>
                        !eatingInButton.ClassList.Contains("attendance-section__btn--eating-in")
                        && !unknownButton.ClassList.Contains("attendance-section__btn--unknown")
                        && notEatingInButton.ClassList.Contains("attendance-section__btn--not-eating-in"),
                    _ => false,
                };

                return highlightCorrect.Label(
                    $"Status={attendance.Status}: expected exactly one button highlighted with correct CSS class");
            });
    }

    private static BunitContext CreateBunitContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));
        context.Services.AddScoped(serviceProvider =>
            new ActiveHousemateService(serviceProvider.GetRequiredService<IJSRuntime>()));
        context.Services.AddLocalization();
        context.RegisterHttpClient(HttpStatusCode.OK, null);
        return context;
    }
}
