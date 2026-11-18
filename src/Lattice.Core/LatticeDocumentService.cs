using System.Globalization;

namespace Lattice;

/// <summary>Owns the open documents of one host scope.</summary>
public sealed class LatticeDocumentService : ILatticeDocumentService
{
    private readonly Dictionary<DocumentTypeId, DocumentDescriptor> _types;
    private readonly List<LatticeDocumentInstance> _documents = [];
    private long _nextId;
    private string? _activeId;

    /// <summary>Creates the document service.</summary>
    /// <param name="catalog">The composed plugin catalog.</param>
    public LatticeDocumentService(LatticePluginCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _types = catalog.Documents.ToDictionary(descriptor => descriptor.Id);
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<LatticeDocumentInstance> Documents => _documents;

    /// <inheritdoc />
    public LatticeDocumentInstance? Active =>
        _documents.FirstOrDefault(document =>
            string.Equals(document.Id, _activeId, StringComparison.Ordinal));

    /// <inheritdoc />
    public LatticeResult<LatticeDocumentInstance> Open(
        DocumentTypeId typeId,
        StringKey? title = null,
        object? payload = null,
        LatticeDockLocation? location = null)
    {
        if (!_types.TryGetValue(typeId, out var descriptor))
        {
            return LatticeResult<LatticeDocumentInstance>.Fail(
                "lattice.document.unknown_type",
                $"The document type '{typeId}' is not registered.");
        }

        var document = new LatticeDocumentInstance(
            $"document-{(++_nextId).ToString(CultureInfo.InvariantCulture)}",
            typeId,
            title ?? descriptor.Title,
            payload,
            location ?? descriptor.DefaultLocation);
        _documents.Add(document);
        _activeId = document.Id;
        Changed?.Invoke();
        return LatticeResult<LatticeDocumentInstance>.Ok(document);
    }

    /// <inheritdoc />
    public LatticeResult Activate(string instanceId)
    {
        var document = Find(instanceId);
        if (document is null)
        {
            return LatticeResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        if (string.Equals(_activeId, document.Id, StringComparison.Ordinal))
        {
            return LatticeResult.Ok;
        }

        _activeId = document.Id;
        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    /// <inheritdoc />
    public LatticeResult SetTitle(string instanceId, StringKey title)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LatticeResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (document.Title == title)
        {
            return LatticeResult.Ok;
        }

        _documents[index] = document with { Title = title };
        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    /// <inheritdoc />
    public LatticeResult SetIcon(string instanceId, string? iconKey, string? iconColor)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LatticeResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (string.Equals(document.IconKey, iconKey, StringComparison.Ordinal) &&
            string.Equals(document.IconColor, iconColor, StringComparison.Ordinal))
        {
            return LatticeResult.Ok;
        }

        _documents[index] = document with { IconKey = iconKey, IconColor = iconColor };
        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    /// <inheritdoc />
    public LatticeResult Move(string instanceId, LatticeDockLocation location)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LatticeResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        var document = _documents[index];
        if (document.Location == location)
        {
            return LatticeResult.Ok;
        }

        _documents[index] = document with { Location = location };
        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    /// <inheritdoc />
    public LatticeResult SetPayload(string instanceId, object? payload)
    {
        var index = IndexOf(instanceId);
        if (index < 0)
        {
            return LatticeResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        _documents[index] = _documents[index] with { Payload = payload };
        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    /// <inheritdoc />
    public LatticeResult Close(string instanceId)
    {
        var document = Find(instanceId);
        if (document is null)
        {
            return LatticeResult.Fail(
                "lattice.document.unknown_instance",
                $"The document '{instanceId}' is not open.");
        }

        _documents.Remove(document);
        if (string.Equals(_activeId, document.Id, StringComparison.Ordinal))
        {
            _activeId = _documents.LastOrDefault()?.Id;
        }

        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    private LatticeDocumentInstance? Find(string instanceId)
    {
        var index = IndexOf(instanceId);
        return index < 0 ? null : _documents[index];
    }

    internal bool Restore(IReadOnlyList<LatticeDocumentInstance> documents, string? activeId)
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
        _activeId = _documents.Any(document => document.Id == activeId) ? activeId : _documents.LastOrDefault()?.Id;
        if (_documents.Count > 0)
        {
            Changed?.Invoke();
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
        _documents.FindIndex(document =>
            string.Equals(document.Id, instanceId, StringComparison.Ordinal));
}
