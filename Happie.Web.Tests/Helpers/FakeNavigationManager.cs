using Microsoft.AspNetCore.Components;

namespace Happie.Web.Tests.Helpers;

/// <summary>Fake NavigationManager for unit tests that captures navigation calls.</summary>
public class FakeNavigationManager : NavigationManager
{
    public string? LastNavigatedUri { get; private set; }
    public bool LastForceLoad { get; private set; }

    public FakeNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        LastNavigatedUri = uri;
        LastForceLoad = forceLoad;
    }
}
