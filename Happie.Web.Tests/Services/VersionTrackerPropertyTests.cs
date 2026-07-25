using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Web.Services;
using Happie.Web.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Happie.Web.Tests.Services;

// Feature: version-tracking, Property 5: At-most-once reporting per app lifecycle.
public class VersionTrackerPropertyTests
{
    private static readonly Arbitrary<int> CallCountArb =
        Gen.Choose(1, 10).ToArbitrary();

    // Feature: version-tracking, Property 5: At-most-once reporting per app lifecycle.
    [Property(MaxTest = 100)]
    public Property ReportVersionAsync_MultipleInvocations_InitiatesAtMostOneHttpRequest()
    {
        return Prop.ForAll(
            CallCountArb,
            async callCount =>
            {
                // Arrange.
                var handler = new CountingHttpMessageHandler();
                var httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri("http://localhost/api/")
                };
                var connectivityMock = new Mock<IConnectivityService>();
                connectivityMock.Setup(x => x.IsOnline).Returns(true);
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AppVersion"] = "2.1.0.42" })
                    .Build();
                var sut = new VersionTracker(httpClient, connectivityMock.Object, configuration);

                // Act.
                for (var i = 0; i < callCount; i++)
                    sut.ReportVersionAsync();

                // Allow the fire-and-forget task to complete.
                await Task.Delay(100);

                // Assert.
                return (handler.CallCount <= 1)
                    .Label($"Expected at most 1 HTTP request but got {handler.CallCount} after {callCount} invocations");
            });
    }
}
