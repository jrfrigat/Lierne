namespace Lierne;

/// <summary>Identifies a top-level menu group contributed by a plugin.</summary>
public readonly record struct MenuGroupId
{
    /// <summary>Creates a menu group id.</summary>
    /// <param name="value">The stable identifier, for example <c>lattice.tools</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public MenuGroupId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies one menu item contributed by a plugin.</summary>
public readonly record struct MenuItemId
{
    /// <summary>Creates a menu item id.</summary>
    /// <param name="value">The stable identifier, for example <c>lattice.tools.options</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public MenuItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Describes a top-level menu group.</summary>
/// <param name="Id">The stable group id.</param>
/// <param name="Title">The localized group title.</param>
/// <param name="Order">The order of the group in the menu bar.</param>
public sealed record MenuGroupDescriptor(
    MenuGroupId Id,
    StringKey Title,
    int Order = 0);

/// <summary>Describes one menu item or submenu.</summary>
/// <param name="Id">The stable item id.</param>
/// <param name="Group">The top-level group that hosts the item.</param>
/// <param name="Title">The localized title.</param>
/// <param name="Command">The command the item invokes, or <see langword="null"/> for a submenu.</param>
/// <param name="Order">The order of the item inside its parent.</param>
/// <param name="IsSeparator">Whether the item is a separator.</param>
/// <param name="Children">The items of a submenu, or <see langword="null"/> for a leaf item.</param>
public sealed record MenuItemDescriptor(
    MenuItemId Id,
    MenuGroupId Group,
    StringKey Title,
    CommandId? Command = null,
    int Order = 0,
    bool IsSeparator = false,
    IReadOnlyList<MenuItemDescriptor>? Children = null);
