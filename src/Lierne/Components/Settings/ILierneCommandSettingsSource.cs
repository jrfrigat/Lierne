namespace Lierne;

/// <summary>Contributes commands that a host registers outside the Lierne plugin catalog.</summary>
public interface ILierneCommandSettingsSource
{
    /// <summary>Raised when the listing or an effective shortcut changes.</summary>
    event Action? Changed;

    /// <summary>Gets the host's commands with their current shortcut values.</summary>
    IReadOnlyList<LierneCommandSetting> GetCommands();
}

/// <summary>A command and its shortcut settings as shown in the command catalog.</summary>
/// <param name="Id">The stable command identifier.</param>
/// <param name="Title">The localized command title.</param>
/// <param name="Category">The localized group title.</param>
/// <param name="DefaultShortcut">The default gesture, if any.</param>
/// <param name="Shortcut">The effective gesture, if any.</param>
/// <param name="SetShortcut">Updates the gesture, or is null for a command that needs arguments.</param>
public sealed record LierneCommandSetting(
    CommandId Id,
    string Title,
    string Category,
    string? DefaultShortcut,
    string? Shortcut,
    Func<string?, Task>? SetShortcut);
