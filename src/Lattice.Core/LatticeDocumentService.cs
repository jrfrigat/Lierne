namespace Lattice;

/// <summary>Owns the open documents of one host scope.</summary>
public sealed class LatticeDocumentService : ILatticeDocumentService
{
    private readonly Dictionary<DocumentTypeId, DocumentDescriptor> _types;
    private readonly List<LatticeDocumentInstance> _documents = [];
    private int _nextId;

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
    public LatticeDocumentInstance? Active { get; private set; }

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
            $"document-{++_nextId}",
            typeId,
            title ?? descriptor.Title,
            payload,
            location ?? descriptor.DefaultLocation);
        _documents.Add(document);
        Active = document;
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

        if (ReferenceEquals(Active, document))
        {
            return LatticeResult.Ok;
        }

        Active = document;
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
        if (ReferenceEquals(Active, document))
        {
            Active = _documents.LastOrDefault();
        }

        Changed?.Invoke();
        return LatticeResult.Ok;
    }

    private LatticeDocumentInstance? Find(string instanceId) =>
        _documents.FirstOrDefault(document =>
            string.Equals(document.Id, instanceId, StringComparison.Ordinal));
}
