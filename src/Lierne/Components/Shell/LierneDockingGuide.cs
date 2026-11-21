namespace Lierne;

/// <summary>Chooses the docking guide the workspace presents while a tab is dragged.</summary>
public enum LierneDockingGuide
{
    /// <summary>The Flare docking guide: one restrained highlight of the target region under the pointer.</summary>
    Base,

    /// <summary>Keep dropping enabled but hide the visual guide.</summary>
    None,

    /// <summary>Hide the base guide and render <see cref="LierneWorkspace.CustomDockingGuide"/> instead.</summary>
    Custom
}
