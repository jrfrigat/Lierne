using System.Globalization;

namespace Lierne;

/// <summary>Owns the open documents of one host scope.</summary>
public sealed class LierneDocumentService : ILierneDocumentService
{
    private readonly Dictionary<DocumentTypeId, DocumentDescriptor> _types;
    private readonly Dictionary<DocumentTypeId, IReadOnlyList<DocumentViewDescriptor>> _viewsByType;
    private readonly List<LierneDocumentInstance> _documents = [];
    private long _nextId;
    private string? _activeId;

    /// <summary>Creates the document service.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LierneDocumentService(LiernePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _types = catalog.Documents.ToDictionary(descriptor => descriptor.Id);
        _viewsByType = catalog.DocumentViews
            .GroupBy(view => view.DocumentType)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DocumentViewDescriptor>)group
                    .OrderBy(view => view.Order)
                    .ThenBy(view => view.Id.Value, StringComparer.Ordinal)
                    .ToArray());
    }

    /// <inheritdoc />
    public event Action? Changed;

    internal event Action<LierneDocumentChange>? DocumentChanged;

    /// <inheritdoc />
    public IReadOnlyList<LierneDocumentInstance> Documents => _documents;

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
                var change = LierneDocumentChange.Selection | LierneDocumentChange.Workspace;
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

                if (view is { } selectedView &&
                    updated.SelectedView != selectedView &&
                    IsKnownView(typeId, selectedView))
                {
                    updated = updated with { SelectedView = selectedView };
                    change |= LierneDocumentChange.Tabs;
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

                _documents[existingIndex] = updated;
                _activeId = updated.Id;
                NotifyChanged(change);
                return LierneResult<LierneDocumentInstance>.Ok(updated);
            }
        }

        var selected = view ?? descriptor.DefaultView ?? ViewsFor(typeId).FirstOrDefault()?.Id;
        var document = new LierneDocumentInstance(
            $"document-{(++_nextId).ToString(CultureInfo.InvariantCulture)}",
            typeId,
            title ?? descriptor.Title,
            payload,
            location ?? descriptor.DefaultLocation,
            Key: key,
            SelectedView: selected,
            Preview: preview);
        _documents.Add(document);
        _activeId = document.Id;
        NotifyChanged(LierneDocumentChange.Structure);
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
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace);
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
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace);
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
        NotifyChanged(LierneDocumentChange.Tabs);
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
        NotifyChanged(LierneDocumentChange.Structure);
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
        NotifyChanged(LierneDocumentChange.Tabs);
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

        if (document.SelectedView == view)
        {
            return LierneResult.Ok;
        }

        _documents[index] = document with { SelectedView = view };
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace);
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
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace);
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
        NotifyChanged(LierneDocumentChange.Tabs | LierneDocumentChange.Workspace);
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
        NotifyChanged(LierneDocumentChange.Workspace);
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
        if (string.Equals(_activeId, instanceId, StringComparison.Ordinal))
        {
            _activeId = _documents.LastOrDefault()?.Id;
        }

        NotifyChanged(LierneDocumentChange.Structure);
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
        _viewsByType.TryGetValue(typeId, out var views)
            ? views
            : [];

    private bool IsKnownView(DocumentTypeId typeId, DocumentViewId view) =>
        ViewsFor(typeId).Any(descriptor => descriptor.Id == view);

    private LierneDocumentInstance? Find(string instanceId)
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

        _documents.AddRange(documents);
        _activeId = activeId is not null && IndexOf(activeId) >= 0 ? activeId : _documents.LastOrDefault()?.Id;
        if (_documents.Count > 0)
        {
            NotifyChanged(LierneDocumentChange.Structure);
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

    private int IndexOf(string instanceId)
    {
        for (var index = 0; index < _documents.Count; index++)
        {
            if (string.Equals(_documents[index].Id, instanceId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void NotifyChanged(LierneDocumentChange change)
    {
        DocumentChanged?.Invoke(change);
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
    Structure = Tabs | Selection | Workspace
}
