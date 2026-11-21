using Microsoft.Extensions.DependencyInjection;

namespace Lierne.Core.Tests;

/// <summary>Tests the semantic panel placements and their visibility.</summary>
public sealed class LiernePanelServiceTests
{
    private static readonly ToolWindowId Left = new("tests.left");
    private static readonly ToolWindowId Bottom = new("tests.bottom");

    [Fact]
    public void PlacementsFollowDescriptorsAndVisibility()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var panels = scope.ServiceProvider.GetRequiredService<IPanelService>();
        var changes = 0;
        panels.Changed += () => changes++;

        Assert.Equal(2, panels.Placements.Count);
        var left = panels.Get(Left)!;
        Assert.Equal(LierneToolRegion.Left, left.Region);
        Assert.True(left.Visible);
        var bottom = panels.Get(Bottom)!;
        Assert.Equal(LierneToolRegion.Bottom, bottom.Region);
        Assert.False(bottom.Visible);

        Assert.True(panels.Hide(Left).Success);
        Assert.False(panels.Get(Left)!.Visible);
        Assert.True(panels.Show(Left).Success);
        Assert.True(panels.Get(Left)!.Visible);

        var moved = left with
        {
            Region = LierneToolRegion.Right,
            Slot = PanelSlot.Secondary,
            Mode = PanelMode.AutoHide,
            Order = 3,
            Size = 320,
            SlotSize = 320
        };
        Assert.True(panels.Move(Left, moved).Success);
        Assert.Equal(moved with { Panel = Left }, panels.Get(Left));

        Assert.True(panels.SetPinned(Left, false).Success);
        Assert.False(panels.Get(Left)!.Pinned);
        Assert.True(changes >= 4);
    }

    [Fact]
    public void UnknownPanelReturnsStableError()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var panels = scope.ServiceProvider.GetRequiredService<IPanelService>();
        var missing = new ToolWindowId("tests.missing");

        Assert.Equal("lattice.panel.unknown", panels.Show(missing).ErrorCode);
        Assert.Equal("lattice.panel.unknown", panels.Hide(missing).ErrorCode);
        Assert.Equal("lattice.panel.unknown", panels.SetPinned(missing, false).ErrorCode);
        Assert.Null(panels.Get(missing));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLierne(options => options.AddPlugin(new TestPlugin("tests.panels", builder =>
        {
            builder.AddToolWindow(new ToolWindowDescriptor(
                Left, TestDescriptors.Key("left"), null, typeof(object)));
            builder.AddToolWindow(new ToolWindowDescriptor(
                Bottom,
                TestDescriptors.Key("bottom"),
                null,
                typeof(object),
                LierneToolRegion.Bottom,
                DefaultVisible: false));
        })));
        return services.BuildServiceProvider();
    }
}
