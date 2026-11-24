using System.Globalization;

namespace Lierne;

/// <summary>Owns the open documents of one host scope.</summary>
public sealed class LierneDocumentService : ILierneDocumentService
{
    private readonly Dictionary<DocumentTypeId, DocumentDescriptor> _types;
    private readonly LiernePluginCatalog _catalog;
    private readonly Dictionary<DocumentTypeId, LierneDocumentViewLayout> _layoutsByType;
    private readonly List<LierneDocumentInstance> _documents = [];
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    private long _nextId;
    private string? _activeId;

    /// <summary>Creates the document service.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneDocumentService(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        Documents = _documents.AsReadOnly();
        _types = catalog.Documents.ToDictionary(descriptor => descriptor.Id);
        _layoutsByType = _types.Values.ToDictionary(
            type => type.Id,
            type => LierneDocumentViewLayoutResolver.Resolve(type.ViewLayout, ViewsFor(type.Id)));
    }

    /// <inheritdoc />
    public event Action? Changed;

    internal event Action<LierneDocumentChange, string?>? DocumentChanged;

    /// <inheritdoc />
    public IReadOnlyList<LierneDocumentInstance> Documents { get; }

    /// <inheritdoc />
    public LierneDocumentInstance? Active => _activeId is { } id ? Find(id) : null;

    /// <inheritdoc />
    public LierneResult<LierneDocumentInstance> Open(
        DocumentTypeId typeId,
        StringKey? title = null,
        object? payload = null,
        LierneDockLocation? location = null,
        DocumentKey? key = null,
        DocumentReusePolicy? reusePolicy = null,
        DocumentViewId? view = null,
        bool preview = false)
    {
        if (!_types.TryGetValue(typeId, out var descriptor))
        {
            return LierneResult<LierneDocumentInstance>.Fail(
                "lattice.document.unknown_type",
                $"The document type '{typeId}' is not registered.");
        }

        if (key is { } documentKey && string.IsNullOrWhiteSpace(documentKey.Value))
        {
            return LierneResult<LierneDocumentInstance>.Fail(
                "lattice.document.invalid_key",
                "A document key needs a non-empty value.");
        }

        var policy = reusePolicy ?? descriptor.ReusePolicy;
        if (key is { } existingKey && policy == DocumentReusePolicy.ReuseByKey)
        {
            var existingIndex = IndexOfKey(existingKey);
            if (existingIndex >= 0)
            {
                var updated = _documents[existingIndex];
                var change = _activeId == updated.Id ? 0 : LierneDocumentChange.Selection;
                if (title is { } newTitle && updated.Title != newTitle)
                {
                    updated = updated with { Title = newTitle };
                    change |= LierneDocumentChange.Tabs;
                }

                if (payload is not null && !ReferenceEquals(payload, updated.Payload))
                {
                    updated = updated with
                    {
                        Payload = payload,
                        Dirty = true,
                        Version = updated.Version + 1
                    };
                    change |= LierneDocumentChange.Content;
                }

                if (view is { } selectedView && IsKnownView(updated.TypeId, selectedView))
                {
                    var selectedDocument = WithSelectedView(updated, selectedView);
                    if (!ReferenceEquals(selectedDocument, updated))
                    {
                        updated = selectedDocument;
                        change |= LierneDocumentChange.Content;
                    }
                }

                if (preview && !updated.Preview)
                {
                    updated = updated with { Preview = true };
                    change |= LierneDocumentChange.Tabs;
                }

                if (location is { } newLocation && updated.Location != newLocation)
                {
                    updated = updated with { Location = newLocation };
                    change |= LierneDocumentChange.Structure;
                }

                if (change != 0)
                {
                    _documents[existingIndex] = updated;
                    _activeId = updated.Id;
                    NotifyChanged(change | LierneDocumentChange.Workspace, updated.Id);
                }
                return LierneResult<LierneDocumentInstance>.Ok(updated);
            }
        }

        var selected = view ?? descriptor.DefaultView ?? ViewsFor(typeId).FirstOrDefault()?.Id;
        IReadOnlyDictionary<int, DocumentViewId>? groupSelectedViews = null;
        if (selected is { } selectedId && LayoutFor(typeId).GroupFor(selectedId) is { } selectedGroup)
        {
            groupSelectedViews = new Dictionary<int, DocumentViewId>
            {
                [selectedGroup.Index] = selectedId
            };
        }

        var document = new LierneDocumentInstance(
            $"document-{(++_nextId).ToString(CultureInfo.InvariantCulture)}",
            typeId,
            title ?? descriptor.Title,
            payload,
            location ?? descriptor.DefaultLocation,
            Key: key,
            SelectedView: selected,
            Preview: preview,
            GroupSelectedViews: groupSelectedViews);
        _indices.Add(document.Id, _documents.Count);
        _documents.Add(document);
        _activeId = document.Id;
        NotifyChanged(LierneDocumentChange.Structure | LierneDocumentChange.Workspace);
        return LierneResult<LierneDocumentInstance>.Ok(document);
    }

    /// <inheritdoc />
    public LierneResult Activate(string instanceId)
    {
        var document = Find(instanceId);
        if (document is null)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        if (string.Equals(_activeId, document.Id, StringComparison.Ordinal))
        {
            return LierneResult.Ok;
        }

        _activeId = document.Id;
        NotifyChanged(LierneDocumentChange.Selection | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetTitle(string instanceId, StringKey title)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (document.Title == title)
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { Title = title };
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace, instanceId);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetIcon(string instanceId, string? iconKey, string? iconColor)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (string.Equals(document.IconKey, iconKey, StringComparison.Ordinal) &&
            string.Equals(document.IconColor, iconColor, StringComparison.Ordinal))
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { IconKey = iconKey, IconColor = iconColor };
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace, instanceId);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetStatusIcon(string instanceId, string? iconKey, string? iconColor)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (string.Equals(document.StatusIconKey, iconKey, StringComparison.Ordinal) &&
            string.Equals(document.StatusIconColor, iconColor, StringComparison.Ordinal))
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { StatusIconKey = iconKey, StatusIconColor = iconColor };
        NotifyChanged(LierneDocumentChange.Tabs, instanceId);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult Move(string instanceId, LierneDockLocation location)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (document.Location == location)
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { Location = location };
        NotifyChanged(LierneDocumentChange.Structure | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetPayload(string instanceId, object? payload)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        var change = LierneDocumentChange.Workspace;
        if (!ReferenceEquals(document.Payload, payload))
        {
            _documents[index] = document with
            {
                Payload = payload,
                Dirty = true,
                Version = document.Version + 1
            };
            change |= LierneDocumentChange.Content;
        }
        else if (payload is not null and not string)
        {
            // Passing the same object can signal an in-place edit that still needs rendering.
            _documents[index] = document with
            {
                Dirty = true,
                Version = document.Version + 1
            };
            change |= LierneDocumentChange.Content;
        }

        NotifyChanged(change);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult MarkSaved(string instanceId, long version)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (!document.Dirty || document.Version != version)
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { Dirty = false };
        NotifyChanged(LierneDocumentChange.Tabs, instanceId);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetSelectedView(string instanceId, DocumentViewId? view)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (view is { } selected && !IsKnownView(document.TypeId, selected))
        {
            return LierneResult.Fail(
                "lattice.document.unknown_view",
                $"The view '{selected}' is not registered for document type '{document.TypeId}'.");
        }

        var updated = WithSelectedView(document, view);
        if (ReferenceEquals(updated, document))
        {
            return LierneResult.Ok;
        }

        _documents[index] = updated;
        NotifyChanged(LierneDocumentChange.Content | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetGroupSelectedView(string instanceId, int groupIndex, DocumentViewId view)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (!IsKnownView(document.TypeId, view))
        {
            return LierneResult.Fail(
                "lattice.document.unknown_view",
                $"The view '{view}' is not registered for document type '{document.TypeId}'.");
        }

        var layout = LayoutFor(document.TypeId);
        if (groupIndex < 0 || groupIndex >= layout.Groups.Count)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_group",
                $"The document type '{document.TypeId}' has no view group '{groupIndex}'.");
        }

        var group = layout.Groups[groupIndex];
        if (!group.Contains(view))
        {
            return LierneResult.Fail(
                "lattice.document.unknown_view",
                $"The view '{view}' is not shown in group '{groupIndex}' of document type '{document.TypeId}'.");
        }

        var current = document.GroupSelectedViews is { } selections &&
                      selections.TryGetValue(groupIndex, out var selected)
            ? selected
            : (DocumentViewId?)null;
        if (document.SelectedView == view && current == view)
        {
            return LierneResult.Ok;
        }

        var updatedSelections = document.GroupSelectedViews is { Count: > 0 } existing
            ? new Dictionary<int, DocumentViewId>(existing)
            : new Dictionary<int, DocumentViewId>();
        updatedSelections[groupIndex] = view;
        _documents[index] = document with
        {
            SelectedView = view,
            GroupSelectedViews = updatedSelections
        };
        NotifyChanged(LierneDocumentChange.Content | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetSplitFraction(string instanceId, int splitIndex, double fraction)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        if (!double.IsFinite(fraction))
        {
            return LierneResult.Fail(
                "lattice.document.invalid_split_fraction",
                "A split fraction needs a finite number.");
        }

        var document = _documents[index];
        var layout = LayoutFor(document.TypeId);
        if (splitIndex < 0 || splitIndex >= layout.Splits.Count)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_split",
                $"The document type '{document.TypeId}' has no view split '{splitIndex}'.");
        }

        var clamped = Math.Round(
            Math.Clamp(
                fraction,
                DocumentViewSplitLayout.MinimumFraction,
                DocumentViewSplitLayout.MaximumFraction),
            4);
        if (document.SplitFractions is { } fractions &&
            fractions.TryGetValue(splitIndex, out var current) &&
            Math.Abs(current - clamped) < 0.00005)
        {
            return LierneResult.Ok;
        }

        var updatedFractions = document.SplitFractions is { Count: > 0 } existing
            ? new Dictionary<int, double>(existing)
            : new Dictionary<int, double>();
        updatedFractions[splitIndex] = clamped;
        _documents[index] = document with { SplitFractions = updatedFractions };
        NotifyChanged(LierneDocumentChange.Content | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetPreview(string instanceId, bool preview)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (document.Preview == preview)
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { Preview = preview };
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace, instanceId);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetPinned(string instanceId, bool pinned)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (document.Pinned == pinned)
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { Pinned = pinned };
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace, instanceId);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult SetViewState(string instanceId, DocumentViewId view, object? state)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (!IsKnownView(document.TypeId, view))
        {
            return LierneResult.Fail(
                "lattice.document.unknown_view",
                $"The view '{view}' is not registered for document type '{document.TypeId}'.");
        }

        if (state is null && document.ViewStates?.ContainsKey(view) != true)
        {
            return LierneResult.Ok;
        }

        var states = document.ViewStates is { } existing
            ? new Dictionary<DocumentViewId, object?>(existing)
            : new Dictionary<DocumentViewId, object?>();
        if (state is null)
        {
            states.Remove(view);
        }
        else
        {
            states[view] = state;
        }

        _documents[index] = document with { ViewStates = states.Count == 0 ? null : states };
        NotifyChanged(LierneDocumentChange.Content | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    /// <inheritdoc />
    public LierneResult Close(string instanceId)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LierneResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        _documents.RemoveAt(index);
        _indices.Remove(instanceId);
        for (var following = index; following < _documents.Count; following++)
        {
            _indices[_documents[following].Id] = following;
        }
        if (string.Equals(_activeId, instanceId, StringComparison.Ordinal))
        {
            _activeId = _documents.LastOrDefault()?.Id;
        }

        NotifyChanged(LierneDocumentChange.Structure | LierneDocumentChange.Workspace);
        return LierneResult.Ok;
    }

    private int IndexOfKey(DocumentKey key)
    {
        for (var index = 0; index < _documents.Count; index++)
        {
            if (_documents[index].Key is { } candidate && candidate == key)
            {
                return index;
            }
        }

        return -1;
    }

    private IReadOnlyList<DocumentViewDescriptor> ViewsFor(DocumentTypeId typeId) =>
        _catalog.ViewsFor(typeId);

    private bool IsKnownView(DocumentTypeId typeId, DocumentViewId view)
    {
        var views = ViewsFor(typeId);
        for (var index = 0; index < views.Count; index++)
        {
            if (views[index].Id == view)
            {
                return true;
            }
        }

        return false;
    }

    private LierneDocumentViewLayout LayoutFor(DocumentTypeId typeId) =>
        _layoutsByType.TryGetValue(typeId, out var layout)
            ? layout
            : LierneDocumentViewLayoutResolver.Resolve(null, ViewsFor(typeId));

    private LierneDocumentInstance WithSelectedView(
        LierneDocumentInstance document,
        DocumentViewId? view)
    {
        var selections = document.GroupSelectedViews;
        if (view is { } selected && LayoutFor(document.TypeId).GroupFor(selected) is { } group &&
            (selections is null || !selections.TryGetValue(group.Index, out var previous) || previous != selected))
        {
            var updated = selections is { Count: > 0 }
                ? new Dictionary<int, DocumentViewId>(selections)
                : new Dictionary<int, DocumentViewId>();
            updated[group.Index] = selected;
            selections = updated;
        }

        if (selections is { Count: 0 })
        {
            selections = null;
        }

        return document.SelectedView == view && ReferenceEquals(selections, document.GroupSelectedViews)
            ? document
            : document with
            {
                SelectedView = view,
                GroupSelectedViews = selections
            };
    }

    internal LierneDocumentInstance? Find(string instanceId)
    {
        var index = IndexOf(instanceId);
        return index < 0 ? null : _documents[index];
    }

    internal bool Restore(IReadOnlyList<LierneDocumentInstance> documents, string? activeId)
    {
        if (_documents.Count != 0)
        {
            return false;
        }

        foreach (var document in documents)
        {
            if (TryReadSequence(document.Id, out var sequence))
            {
                _nextId = Math.Max(_nextId, sequence);
            }
        }

        foreach (var document in documents)
        {
            _indices.Add(document.Id, _documents.Count);
            _documents.Add(document);
        }
        _activeId = activeId is not null && IndexOf(activeId) >= 0 ? activeId : _documents.LastOrDefault()?.Id;
        if (_documents.Count > 0)
        {
            NotifyChanged(LierneDocumentChange.Structure | LierneDocumentChange.Workspace);
        }

        return true;
    }

    internal static bool TryReadSequence(string id, out long sequence)
    {
        const string prefix = "document-";
        sequence = 0;
        return id.StartsWith(prefix, StringComparison.Ordinal) &&
            long.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out sequence) &&
            sequence is > 0 and < long.MaxValue;
    }

    private int IndexOf(string instanceId) =>
        instanceId is not null && _indices.TryGetValue(instanceId, out var index) ? index : -1;

    private void NotifyChanged(LierneDocumentChange change, string? tabDocumentId = null)
    {
        DocumentChanged?.Invoke(change, tabDocumentId);
        Changed?.Invoke();
    }
}

[Flags]
internal enum LierneDocumentChange
{
    Tabs = 1,
    Selection = 2,
    Workspace = 4,
    Content = 8,
    Structure = 16
}
