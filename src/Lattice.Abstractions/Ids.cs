namespace Lattice;

/// <summary>Identifies a Lattice plugin.</summary>
public readonly record struct PluginId
{
    /// <summary>Creates a plugin id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public PluginId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a command contributed by a plugin.</summary>
public readonly record struct CommandId
{
    /// <summary>Creates a command id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection.connect</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public CommandId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a document type contributed by a plugin.</summary>
public readonly record struct DocumentTypeId
{
    /// <summary>Creates a document type id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection.explorer</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public DocumentTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a tool window contributed by a plugin.</summary>
public readonly record struct ToolWindowId
{
    /// <summary>Creates a tool window id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection.explorer</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public ToolWindowId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a ribbon group contributed by a plugin.</summary>
public readonly record struct RibbonGroupId
{
    /// <summary>Creates a ribbon group id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public RibbonGroupId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies one ribbon item contributed by a plugin.</summary>
public readonly record struct RibbonItemId
{
    /// <summary>Creates a ribbon item id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection.connect</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public RibbonItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a status-bar item contributed by a plugin.</summary>
public readonly record struct StatusBarItemId
{
    /// <summary>Creates a status-bar item id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection.status</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public StatusBarItemId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a toolbar contributed by a plugin.</summary>
public readonly record struct ToolbarId
{
    /// <summary>Creates a toolbar id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection.toolbar</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public ToolbarId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies a settings page contributed by a plugin.</summary>
public readonly record struct SettingsPageId
{
    /// <summary>Creates a settings page id.</summary>
    /// <param name="value">The stable identifier, for example <c>quas.connection</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public SettingsPageId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the identifier text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Identifies one setting contributed by a plugin.</summary>
public readonly record struct SettingsKey
{
    /// <summary>Creates a settings key.</summary>
    /// <param name="value">The stable key, for example <c>quas.connection.timeout</c>.</param>
    /// <exception cref="ArgumentException">The value is null, empty or whitespace.</exception>
    public SettingsKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the key text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
