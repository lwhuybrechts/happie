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

// Feature: day-plan-redesign, Property 6: Nudge recipient filtering
// Feature: day-plan-redesign, Property 7: Nudge send button disabled state
public class NudgeModalPropertyTests
{
    private static readonly Arbitrary<AttendanceStatus> StatusArb =
        Gen.Elements(AttendanceStatus.EatingIn, AttendanceStatus.NotEatingIn, AttendanceStatus.Unknown)
            .ToArbitrary();

    private static readonly Arbitrary<(List<AttendanceDto> Attendance, Guid ActiveHousemateId)> RecipientFilterArb =
        Gen.Choose(1, 8)
            .SelectMany(count =>
                Gen.ListOf(
                    ArbMap.Default.GeneratorFor<Guid>()
                        .SelectMany(id => StatusArb.Generator
                            .SelectMany(status => Gen.Elements(HousemateColors.Palette.ToArray())
                                .Select(color => new AttendanceDto(id, $"Housemate-{id.ToString()[..8]}", color, status, false)))),
                    count)
                .SelectMany(attendance =>
                {
                    var list = attendance.ToList();
                    // Pick the active housemate ID from the list or generate a new one.
                    var activeIdGen = list.Count > 0
                        ? Gen.Frequency(
                            (3, Gen.Elements(list.Select(x => x.HousemateId).ToArray())),
                            (1, ArbMap.Default.GeneratorFor<Guid>()))
                        : ArbMap.Default.GeneratorFor<Guid>();
                    return activeIdGen.Select(activeId => (Attendance: list, ActiveHousemateId: activeId));
                }))
            .ToArbitrary();

    private static readonly Arbitrary<int> DeselectionCountArb =
        Gen.Choose(0, 10).ToArbitrary();

    // Feature: day-plan-redesign, Property 6: Nudge recipient filtering
    // Validates: Requirements 17.3
    [Property(MaxTest = 100)]
    public Property Open_RecipientList_ContainsAllNonActiveHousematesWithUnknownPreSelected()
    {
        return Prop.ForAll(
            RecipientFilterArb,
            input =>
            {
                var (attendance, activeHousemateId) = input;
                using var context = CreateBunitContext();

                var cut = context.Render<NudgeModal>(parameters => parameters
                    .Add(x => x.Attendance, attendance)
                    .Add(x => x.ActiveHousemateId, activeHousemateId)
                    .Add(x => x.Date, "2025-01-15"));

                // Open the modal.
                cut.Instance.Open();
                cut.Render();

                // Find recipient chips.
                var recipientChips = cut.FindAll(".nudge-modal__recipient-chip");
                var recipientNames = recipientChips
                    .Select(x => x.QuerySelector(".nudge-modal__recipient-name")?.TextContent.Trim())
                    .ToList();

                // Expected recipients: all housemates except the active one.
                var expectedRecipients = attendance
                    .Where(x => x.HousemateId != activeHousemateId)
                    .ToList();

                var countMatches = recipientNames.Count == expectedRecipients.Count;
                var allExpectedPresent = expectedRecipients.All(x => recipientNames.Contains(x.HousemateName));

                // Only Unknown housemates should be pre-selected.
                var selectedChips = cut.FindAll(".nudge-modal__recipient-chip--selected");
                var expectedSelectedCount = expectedRecipients.Count(x => x.Status == AttendanceStatus.Unknown);
                var selectedCountMatches = selectedChips.Count == expectedSelectedCount;

                return (countMatches && allExpectedPresent && selectedCountMatches).Label(
                    $"Expected {expectedRecipients.Count} chips ({expectedSelectedCount} pre-selected), got {recipientNames.Count} chips ({selectedChips.Count} selected)");
            });
    }

    // Feature: day-plan-redesign, Property 7: Nudge send button disabled state
    // Validates: Requirements 17.8
    [Property(MaxTest = 100)]
    public Property SendButton_DisabledIfAndOnlyIfNoRecipientsSelected()
    {
        return Prop.ForAll(
            RecipientFilterArb,
            DeselectionCountArb,
            (input, deselectionCount) =>
            {
                var (attendance, activeHousemateId) = input;
                using var context = CreateBunitContext();

                var cut = context.Render<NudgeModal>(parameters => parameters
                    .Add(x => x.Attendance, attendance)
                    .Add(x => x.ActiveHousemateId, activeHousemateId)
                    .Add(x => x.Date, "2025-01-15"));

                // Open the modal.
                cut.Instance.Open();
                cut.Render();

                // Deselect some recipients by clicking their chips.
                var recipientChips = cut.FindAll(".nudge-modal__recipient-chip");
                var chipsToDeselect = Math.Min(deselectionCount, recipientChips.Count);

                for (var i = 0; i < chipsToDeselect; i++)
                {
                    // Re-query because the DOM may have changed.
                    var chips = cut.FindAll(".nudge-modal__recipient-chip");
                    if (i < chips.Count)
                        chips[i].Click();
                }

                // Count how many are still selected.
                var selectedChips = cut.FindAll(".nudge-modal__recipient-chip--selected");
                var noRecipientsSelected = selectedChips.Count == 0;

                // Check the send button disabled state.
                var sendButton = cut.Find(".nudge-modal__send-btn");
                var isDisabled = sendButton.HasAttribute("disabled");

                return (isDisabled == noRecipientsSelected).Label(
                    $"Selected={selectedChips.Count}, disabled={isDisabled}: button should be disabled iff no recipients selected");
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
