namespace Lattice;

/// <summary>Provides typed access to the settings declared by the plugins.</summary>
public interface ISettingsStore
{
    /// <summary>Raised after a setting value changes.</summary>
    event Action<SettingsKey>? Changed;

    /// <summary>Reads a setting value, falling back to its descriptor default.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The stored value, the default, or <see langword="null"/> for an unknown key.</returns>
    object? GetValue(SettingsKey key);

    /// <summary>Reads a setting value as a typed value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <returns>The value when it has the requested type; otherwise the type default.</returns>
    T? Get<T>(SettingsKey key);

    /// <summary>Stores and validates a setting value.</summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the key exists and the value matches its descriptor.</returns>
    LatticeResult Set(SettingsKey key, object? value);
}

/// <summary>Provides the localized strings contributed by the plugins.</summary>
public interface ILatticeLocalizer
{
    /// <summary>Raised after the active culture changes.</summary>
    event Action? Changed;

    /// <summary>Gets the active two-letter culture.</summary>
    string CurrentCulture { get; }

    /// <summary>Resolves a string key in the active culture, then in the fallback culture.</summary>
    /// <param name="key">The string key.</param>
    /// <returns>The localized text, or the full key text when no catalog contains it.</returns>
    string Get(StringKey key);

    /// <summary>Changes the active culture.</summary>
    /// <param name="culture">The two-letter culture, for example <c>en</c> or <c>ru</c>.</param>
    void SetCulture(string culture);
}

/// <summary>Describes one command shortcut and its effective gesture.</summary>
/// <param name="Command">The command.</param>
/// <param name="Title">The localized command title.</param>
/// <param name="DefaultShortcut">The gesture declared by the descriptor, when any.</param>
/// <param name="EffectiveShortcut">The gesture after overrides, when any.</param>
public sealed record LatticeShortcutBinding(
    CommandId Command,
    StringKey Title,
    string? DefaultShortcut,
    string? EffectiveShortcut);

/// <summary>Describes two or more commands that share the same effective gesture.</summary>
/// <param name="Gesture">The shared gesture.</param>
/// <param name="Commands">The commands that use it.</param>
public sealed record LatticeShortcutConflict(
    string Gesture,
    IReadOnlyList<CommandId> Commands);

/// <summary>Owns the command shortcuts and the user overrides.</summary>
public interface ILatticeShortcutService
{
    /// <summary>Raised after the overrides change.</summary>
    event Action? Changed;

    /// <summary>Gets the bindings in command registration order.</summary>
    IReadOnlyList<LatticeShortcutBinding> Bindings { get; }

    /// <summary>Applies user overrides.</summary>
    /// <param name="overrides">
    /// The gesture by command id. An empty gesture unbinds the command; a missing command keeps its
    /// declared shortcut.
    /// </param>
    /// <returns>Whether every overridden command exists.</returns>
    LatticeResult ApplyOverrides(IReadOnlyDictionary<string, string>? overrides);

    /// <summary>Resolves a gesture to the first command that declares or overrides it.</summary>
    /// <param name="gesture">The gesture text.</param>
    /// <returns>The command, or a not-found result.</returns>
    LatticeResult<CommandId> Resolve(string gesture);

    /// <summary>Finds effective gestures shared by more than one command.</summary>
    /// <returns>The conflicts in binding order.</returns>
    IReadOnlyList<LatticeShortcutConflict> FindConflicts();
}
