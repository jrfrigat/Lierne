namespace Lierne;

/// <summary>Identifies a resource-backed document so the host can reuse it.</summary>
/// <param name="Value">The stable resource key, for example a file path or a connection id.</param>
public readonly record struct DocumentKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Chooses whether a new document request opens another instance or activates an existing one.</summary>
public enum DocumentReusePolicy
{
    /// <summary>Always open a new document instance.</summary>
    AlwaysNew,

    /// <summary>Activate the open document with the same key, or open a new one when none exists.</summary>
    ReuseByKey
}

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
/// <param name="Key">The resource key used for reuse, when the document has one.</param>
/// <param name="Dirty">Whether the document has unsaved content changes.</param>
/// <param name="Version">The content version, increased on every content change.</param>
/// <param name="SelectedView">The view shown for the document, or <see langword="null"/>.</param>
/// <param name="Preview">Whether the document host is a preview tab.</param>
/// <param name="Pinned">Whether the document host is pinned.</param>
/// <param name="ViewStates">Per-view state values keyed by view id.</param>
/// <param name="GroupSelectedViews">
/// The selected view of each tab group, keyed by the group index in document view layout order.
/// </param>
/// <param name="SplitFractions">
/// The first-branch share of each split, keyed by the split index in document view layout order.
/// </param>
public sealed record LierneDocumentInstance(
    string Id,
    DocumentTypeId TypeId,
    StringKey Title,
    object? Payload,
    LierneDockLocation Location,
    string? IconKey = null,
    string? IconColor = null,
    string? StatusIconKey = null,
    string? StatusIconColor = null,
    DocumentKey? Key = null,
    bool Dirty = false,
    long Version = 0,
    DocumentViewId? SelectedView = null,
    bool Preview = false,
    bool Pinned = false,
    IReadOnlyDictionary<DocumentViewId, object?>? ViewStates = null,
    IReadOnlyDictionary<int, DocumentViewId>? GroupSelectedViews = null,
    IReadOnlyDictionary<int, double>? SplitFractions = null);

/// <summary>Owns the open documents of one host scope.</summary>
public interface ILierneDocumentService
{
    /// <summary>Raised after the document list or the active document changes.</summary>
    event Action? Changed;

    /// <summary>Gets the open documents in opening order.</summary>
    IReadOnlyList<LierneDocumentInstance> Documents { get; }

    /// <summary>Gets the active document, or <see langword="null"/> when none is active.</summary>
    LierneDocumentInstance? Active { get; }

    /// <summary>Opens a document of a contributed type and makes it active.</summary>
    /// <param name="typeId">The document type.</param>
    /// <param name="title">A title key overriding the descriptor title, or <see langword="null"/>.</param>
    /// <param name="payload">The document payload, when any.</param>
    /// <param name="location">The document region, or <see langword="null"/> for the descriptor default.</param>
    /// <param name="key">The resource key used for reuse, when the document has one.</param>
    /// <param name="reusePolicy">The reuse policy, or <see langword="null"/> for the descriptor policy.</param>
    /// <param name="view">The view to select, or <see langword="null"/> for the descriptor default.</param>
    /// <param name="preview">Whether the document opens as a preview tab.</param>
    /// <returns>
    /// The opened or reused document, or an unknown-type error.
    /// </returns>
    LierneResult<LierneDocumentInstance> Open(
        DocumentTypeId typeId,
        StringKey? title = null,
        object? payload = null,
        LierneDockLocation? location = null,
        DocumentKey? key = null,
        DocumentReusePolicy? reusePolicy = null,
        DocumentViewId? view = null,
        bool preview = false);

    /// <summary>Activates a document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <returns>Whether the document exists and became active.</returns>
    LierneResult Activate(string instanceId);

    /// <summary>Changes the title of an open document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="title">The new title key.</param>
    /// <returns>Whether the document exists and the title changed.</returns>
    LierneResult SetTitle(string instanceId, StringKey title);

    /// <summary>Changes the icon of an open document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="iconKey">The host icon key override, or <see langword="null"/> for the type icon.</param>
    /// <param name="iconColor">The CSS icon color, or <see langword="null"/> for the default.</param>
    /// <returns>Whether the document exists and the icon changed.</returns>
    LierneResult SetIcon(string instanceId, string? iconKey, string? iconColor);

    /// <summary>Changes the status icon shown next to the document icon.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="iconKey">The host status icon key, or <see langword="null"/> to remove the status icon.</param>
    /// <param name="iconColor">The CSS status icon color, or <see langword="null"/> for the default.</param>
    /// <returns>Whether the document exists and the status icon changed.</returns>
    LierneResult SetStatusIcon(string instanceId, string? iconKey, string? iconColor);

    /// <summary>Replaces a document's payload and notifies the host so it can save the new state.</summary>
    LierneResult SetPayload(string instanceId, object? payload) =>
        LierneResult.Fail("lattice.document.payload_not_supported", "The document service cannot replace payloads.");

    /// <summary>Marks the content version as saved and clears the dirty flag when it still matches.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="version">The content version that was written.</param>
    /// <returns>Whether the document exists.</returns>
    LierneResult MarkSaved(string instanceId, long version) => LierneResult.Ok;

    /// <summary>Selects the visible view of a document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="view">The view id, or <see langword="null"/> to clear the selection.</param>
    /// <returns>Whether the document exists and the view belongs to its type.</returns>
    LierneResult SetSelectedView(string instanceId, DocumentViewId? view) => LierneResult.Ok;

    /// <summary>
    /// Selects the visible view of one tab group and makes it the document's selected view.
    /// </summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="groupIndex">The tab group index in document view layout order.</param>
    /// <param name="view">The view id; it must belong to that group.</param>
    /// <returns>Whether the document, the group and the view exist.</returns>
    LierneResult SetGroupSelectedView(string instanceId, int groupIndex, DocumentViewId view) =>
        LierneResult.Ok;

    /// <summary>Sets the first-branch share of one split, clamped to 0.05..0.95.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="splitIndex">The split index in document view layout order.</param>
    /// <param name="fraction">The share of the first branch.</param>
    /// <returns>Whether the document and the split exist and the fraction is a finite number.</returns>
    LierneResult SetSplitFraction(string instanceId, int splitIndex, double fraction) =>
        LierneResult.Ok;

    /// <summary>Marks the document host as a preview tab.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="preview">Whether the document is a preview.</param>
    /// <returns>Whether the document exists.</returns>
    LierneResult SetPreview(string instanceId, bool preview) => LierneResult.Ok;

    /// <summary>Pins or unpins the document host.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="pinned">Whether the document is pinned.</param>
    /// <returns>Whether the document exists.</returns>
    LierneResult SetPinned(string instanceId, bool pinned) => LierneResult.Ok;

    /// <summary>Stores one view state value for the document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="view">The view id.</param>
    /// <param name="state">The view-owned state value, or <see langword="null"/> to remove it.</param>
    /// <returns>Whether the document exists and the view belongs to its type.</returns>
    LierneResult SetViewState(string instanceId, DocumentViewId view, object? state) => LierneResult.Ok;

    /// <summary>Moves a document to another region.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <param name="location">The destination region.</param>
    /// <returns>Whether the document exists and was moved.</returns>
    LierneResult Move(string instanceId, LierneDockLocation location);

    /// <summary>Closes a document.</summary>
    /// <param name="instanceId">The document instance id.</param>
    /// <returns>Whether the document existed and was closed.</returns>
    LierneResult Close(string instanceId);
}

/// <summary>Describes one tool window and its current visibility.</summary>
/// <param name="Id">The tool window id.</param>
/// <param name="Region">The region that hosts the tool window.</param>
/// <param name="Visible">Whether the tool window is visible.</param>
public sealed record LierneToolWindowInstance(
    ToolWindowId Id,
    LierneToolRegion Region,
    bool Visible);

/// <summary>Owns the tool windows of one host scope.</summary>
public interface ILierneToolWindowService
{
    /// <summary>Raised after a tool window becomes visible or hidden.</summary>
    event Action? Changed;

    /// <summary>Gets the contributed tool windows.</summary>
    IReadOnlyList<LierneToolWindowInstance> ToolWindows { get; }

    /// <summary>Shows a tool window.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists.</returns>
    LierneResult Show(ToolWindowId toolWindow);

    /// <summary>Hides a tool window.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists.</returns>
    LierneResult Hide(ToolWindowId toolWindow);

    /// <summary>Toggles a tool window.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists.</returns>
    LierneResult Toggle(ToolWindowId toolWindow);

    /// <summary>Checks whether a tool window is visible.</summary>
    /// <param name="toolWindow">The tool window id.</param>
    /// <returns>Whether the tool window exists and is visible.</returns>
    bool IsVisible(ToolWindowId toolWindow);
}
