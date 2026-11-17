namespace Lattice;

/// <summary>Describes one open document.</summary>
/// <param name="Id">The stable instance id.</param>
/// <param name="TypeId">The document type.</param>
/// <param name="Title">The localized title key.</param>
/// <param name="Payload">The document payload, when any.</param>
/// <param name="Location">The region that hosts the document.</param>
public sealed record LatticeDocumentInstance(
    string Id,
    DocumentTypeId TypeId,
    StringKey Title,
    object? Payload,
    LatticeDockLocation Location);

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
