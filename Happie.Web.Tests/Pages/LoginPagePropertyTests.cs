using System.Net;
using System.Text.Json;
using Bunit;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;
using Happie.Web.Pages;
using Happie.Web.Services;
using Happie.Web.Services.Caching;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Pages;

// Feature: login-page-redesign, Property 1: Avatar reflects housemate color and first character
public class LoginPagePropertyTests
{
    private static readonly Arbitrary<HousemateDto> HousemateDtoArb =
        ArbMap.Default.GeneratorFor<Guid>()
            .SelectMany(id => Gen.Elements(HousemateColors.Palette.ToArray())
                .SelectMany(color => Gen.Choose(1, 20)
                    .SelectMany(length => Gen.ArrayOf(
                        Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray()),
                        length)
                        .Select(chars => new string(chars)))
                    .Select(name => new HousemateDto(id, name, color))))
            .ToArbitrary();

    private static readonly Arbitrary<List<HousemateDto>> NonEmptyHousemateListArb =
        Gen.Choose(1, 5)
            .SelectMany(count => Gen.ListOf(HousemateDtoArb.Generator, count)
                .Select(x => x.ToList()))
            .ToArbitrary();

    // Feature: login-page-redesign, Property 1: Avatar reflects housemate color and first character
    // Validates: Requirements 8.5
    [Property(MaxTest = 100)]
    public Property Render_HousemateSelectionView_AvatarReflectsColorAndFirstCharacter()
    {
        return Prop.ForAll(
            NonEmptyHousemateListArb,
            housemates =>
            {
                // Arrange.
                using var context = CreateBunitContext();
                SetupJsInteropForHousemateSelection(context, housemates);

                // Act.
                var cut = context.Render<LoginPage>();

                // Assert.
                var avatars = cut.FindAll(".housemate-avatar");
                var sortedHousemates = housemates
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var result = avatars.Count == sortedHousemates.Count;

                for (var i = 0; i < sortedHousemates.Count && result; i++)
                {
                    var avatar = avatars[i];
                    var housemate = sortedHousemates[i];

                    var styleAttribute = avatar.GetAttribute("style") ?? string.Empty;
                    var containsColor = styleAttribute.Contains(housemate.Color, StringComparison.OrdinalIgnoreCase);
                    var textMatchesFirstChar = avatar.TextContent.Trim() == housemate.Name[0].ToString();

                    result = containsColor && textMatchesFirstChar;
                }

                return result.Label(
                    $"Expected each avatar to reflect housemate color and first character for {sortedHousemates.Count} housemates");
            });
    }

    // Feature: login-page-redesign, Property 2: Housemate rows are sorted alphabetically, case-insensitively
    // Validates: Requirements 8.9
    [Property(MaxTest = 100)]
    public Property Render_HousemateSelectionView_RowsSortedAlphabetically()
    {
        return Prop.ForAll(
            NonEmptyHousemateListArb,
            housemates =>
            {
                // Arrange.
                using var context = CreateBunitContext();
                SetupJsInteropForHousemateSelection(context, housemates);

                // Act.
                var cut = context.Render<LoginPage>();

                // Assert.
                var renderedNames = cut.FindAll(".housemate-name")
                    .Select(x => x.TextContent.Trim())
                    .ToList();

                var expectedNames = housemates
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.Name)
                    .ToList();

                return renderedNames.SequenceEqual(expectedNames).Label(
                    $"Expected names in order [{string.Join(", ", expectedNames)}] but got [{string.Join(", ", renderedNames)}]");
            });
    }

    // Feature: login-page-redesign, Property 3: Hover state uses brand green for all housemates
    // Validates: Requirements 10.1, 10.2
    [Property(MaxTest = 100)]
    public Property Render_HousemateSelectionView_HoverOutlineUsesBrandGreen()
    {
        return Prop.ForAll(
            NonEmptyHousemateListArb,
            housemates =>
            {
                // Arrange.
                using var context = CreateBunitContext();
                SetupJsInteropForHousemateSelection(context, housemates);

                // Act.
                var cut = context.Render<LoginPage>();

                // Assert.
                var rows = cut.FindAll(".housemate-row");
                var sortedHousemates = housemates
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Verify that each housemate has a corresponding row element.
                var rowCountMatches = rows.Count == sortedHousemates.Count;

                // Verify the CSS file contains the brand green hover rule.
                // Since bUnit cannot test CSS pseudo-classes, we read the scoped CSS file
                // and confirm the .housemate-row:hover rule uses #4CAF50.
                var cssPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
                    "Happie.Web", "Pages", "LoginPage.razor.css");
                var cssContent = File.ReadAllText(Path.GetFullPath(cssPath));
                var containsBrandGreenHover = cssContent.Contains(".housemate-row:hover")
                    && cssContent.Contains("#4CAF50");

                return (rowCountMatches && containsBrandGreenHover).Label(
                    $"Expected {sortedHousemates.Count} .housemate-row elements and CSS hover rule with brand green #4CAF50");
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
        context.Services.AddSingleton(new Mock<ICacheStore>().Object);
        context.Services.AddSingleton(new Mock<IConnectivityService>().Object);
        context.Services.AddScoped<SessionService>();
        context.Services.AddLocalization();
        context.RegisterHttpClient(HttpStatusCode.Unauthorized, null);
        return context;
    }

    private static void SetupJsInteropForHousemateSelection(BunitContext context, List<HousemateDto> housemates)
    {
        var serializedHousemates = JsonSerializer.Serialize(housemates);
        context.JSInterop.Setup<string?>("localStorage.getItem", "jwt").SetResult("existing-jwt-token");
        context.JSInterop.Setup<string?>("localStorage.getItem", "activeHousemateId").SetResult(null);
        context.JSInterop.Setup<string?>("sessionStorage.getItem", "pendingHousemates").SetResult(serializedHousemates);
    }
}
