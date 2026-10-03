using System.Net;
using Bunit;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Components;
using Happie.Web.Resources;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;

namespace Happie.Web.Tests.Components;

// Feature: loading-strategy, Property 3: LoadingDots accessibility attributes
public class LoadingDotsPropertyTests
{
    private static readonly Arbitrary<string> NonEmptyAriaLabelArb =
        ArbMap.Default.GeneratorFor<NonEmptyString>()
            .Select(x => x.Get.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArbitrary();

    // Feature: loading-strategy, Property 3: LoadingDots accessibility attributes
    // Validates: Requirements 5.7
    [Property(MaxTest = 100)]
    public Property Render_AlwaysHasRoleStatusAndNonEmptyAriaLabel()
    {
        return Prop.ForAll(
            NonEmptyAriaLabelArb,
            ariaLabelValue =>
            {
                using var context = CreateBunitContext(ariaLabelValue);

                var cut = context.Render<LoadingDots>();

                var rootElement = cut.Find(".loading-dots");
                var roleAttribute = rootElement.GetAttribute("role");
                var ariaLabelAttribute = rootElement.GetAttribute("aria-label");

                var hasRoleStatus = roleAttribute == "status";
                var hasNonEmptyAriaLabel = !string.IsNullOrEmpty(ariaLabelAttribute);

                return (hasRoleStatus && hasNonEmptyAriaLabel).Label(
                    $"role=\"{roleAttribute}\", aria-label=\"{ariaLabelAttribute}\": expected role=\"status\" and non-empty aria-label");
            });
    }

    private static BunitContext CreateBunitContext(string ariaLabelValue)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(serviceProvider =>
            new LocaleService(serviceProvider.GetRequiredService<IJSRuntime>()));

        var localizerMock = new Mock<IStringLocalizer<AppStrings>>();
        localizerMock
            .Setup(x => x["LoadingIndicator_AriaLabel"])
            .Returns(new LocalizedString("LoadingIndicator_AriaLabel", ariaLabelValue));
        localizerMock
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        context.Services.AddSingleton(localizerMock.Object);

        context.RegisterHttpClient(HttpStatusCode.OK, null);
        return context;
    }
}
