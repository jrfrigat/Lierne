namespace Lattice;

/// <summary>Provides the current document and its localized title to a tab header template.</summary>
/// <param name="Document">The document whose tab is being rendered.</param>
/// <param name="Title">The document title in the current UI culture.</param>
public readonly record struct LatticeDocumentHeaderContext(LatticeDocumentInstance Document, string Title);
