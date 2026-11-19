namespace Lattice;

/// <summary>Describes one open document.</summary>
/// <param name="Id">The stable instance id.</param>
/// <param name="TypeId">The document type.</param>
/// <param name="Title">The localized title key.</param>
/// <param name="Payload">The document payload, when any.</param>
/// <param name="Location">The region that hosts the document.</param>
/// <param name="IconKey">The host icon key override, or <see langword="null"/> for the type icon.</param>
/// <param name="IconColor">
/// The icon color as a CSS color, or <see langword="null"/> for the host's default icon color.
/// </param>
/// <param name="StatusIconKey">
/// The optional status icon key shown between the document icon and the title, or <see langword="null"/>.
/// </param>
/// <param name="StatusIconColor">
/// The status icon color as a CSS color, or <see langword="null"/> for the host's default color.
/// </param>
public sealed record LatticeDocumentInstance(
    string Id,
    DocumentTypeId TypeId,
    StringKey Title,
    object? Payload,
    LatticeDockLocation Location,
    string? IconKey = null,
    string? IconColor = null,
    string? StatusIconKey = null,
    string? StatusIconColor = null);

/// <summary>Owns the open documents of one host scope.</summary>
public interface ILatticeDocumentService
{
    /// <summary>Raised after the document list or the active document changes.</summary>
    event Action? Changed;

    /// <summary>Gets the open documents in opening order.</summary>
    IReadOnlyList<LatticeDocumentInstance> Documents { get; }

    /// <summary>Gets the active document, or <see langword="null"/> when none is active.</summary>
    LatticeDocumentInstance? Active { get; }

    /// <summary>Opens a document of a contributed type and makes it active.</summary>
    /// <param name="typeId">The document type.</param>
    /// <param name="title">A title key overriding the descriptor title, or <see langword="null"/>.</param>
    /// <param name="payload">The document payload, when any.</param>
    /// <param name="location">The document region, or <see langword="null"/> for the descriptor default.</param>
    /// <returns>The opened document, or an unknown-type error.</returns>
    LatticeResult<LatticeDocumentInstance> Open(
        DocumentTypeId typeId,
        StringKey? title = null,
        object? payload = null,
        LatticeDockLocation? location = null);

    /// <summary>Activates a document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <returns>Whether the document exists and became active.</returns>
    LatticeResult Activate(string instanceId);

    /// <summary>Changes the title of an open document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="title">The new title key.</param>
    /// <returns>Whether the document exists and the title changed.</returns>
    LatticeResult SetTitle(string instanceId, StringKey title);

    /// <summary>Changes the icon of an open document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="iconKey">The host icon key override, or <see langword="null"/> for the type icon.</param>
    /// <param name="iconColor">The CSS icon color, or <see langword="null"/> for the default.</param>
    /// <returns>Whether the document exists and the icon changed.</returns>
    LatticeResult SetIcon(string instanceId, string? iconKey, string? iconColor);

    /// <summary>Changes the status icon shown next to the document icon.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="iconKey">The host status icon key, or <see langword="null"/> to remove the status icon.</param>
    /// <param name="iconColor">The CSS status icon color, or <see langword="null"/> for the default.</param>
    /// <returns>Whether the document exists and the status icon changed.</returns>
    LatticeResult SetStatusIcon(string instanceId, string? iconKey, string? iconColor);

    /// <summary>Replaces a document's payload and notifies the host so it can save the new state.</summary>
    LatticeResult SetPayload(string instanceId, object? payload) =>
        LatticeResult.Fail("lattice.document.payload_not_supported", "The document service cannot replace payloads.");

    /// <summary>Moves a document to another region.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="location">The destination region.</param>
    /// <returns>Whether the document exists and was moved.</returns>
    LatticeResult Move(string instanceId, LatticeDockLocation location);

    /// <summary>Closes a document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <returns>Whether the document existed and was closed.</returns>
    LatticeResult Close(string instanceId);
}

/// <summary>Describes one tool window and its current visibility.</summary>
/// <param name="Id">The tool window id.</param>
/// <param name="Region">The region that hosts the tool window.</param>
/// <param name="Visible">Whether the tool window is visible.</param>
public sealed record LatticeToolWindowInstance(
    ToolWindowId Id,
    LatticeToolRegion Region,
    bool Visible);

/// <summary>Owns the tool windows of one host scope.</summary>
public interface ILatticeToolWindowService
{
    /// <summary>Raised after a tool window becomes visible or hidden.</summary>
    event Action? Changed;

    /// <summary>Gets the contributed tool windows.</summary>
    IReadOnlyList<LatticeToolWindowInstance> ToolWindows { get; }

    /// <summary>Shows a tool window.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists.</returns>
    LatticeResult Show(ToolWindowId toolWindow);

    /// <summary>Hides a tool window.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists.</returns>
    LatticeResult Hide(ToolWindowId toolWindow);

    /// <summary>Toggles a tool window.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists.</returns>
    LatticeResult Toggle(ToolWindowId toolWindow);

    /// <summary>Checks whether a tool window is visible.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists and is visible.</returns>
    bool IsVisible(ToolWindowId toolWindow);
}
