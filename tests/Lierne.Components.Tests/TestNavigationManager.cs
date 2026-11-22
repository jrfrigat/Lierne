using Microsoft.AspNetCore.Components;

namespace Lierne.Components.Tests;

/// <summary>Provides the navigation service Flare's nav links expect in a static test render.</summary>
internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
    }
}
