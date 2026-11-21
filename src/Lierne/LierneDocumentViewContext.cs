namespace Lierne;

/// <summary>Carries the document, the selected view and the scoped services to a plugin view component.</summary>
public sealed class LierneDocumentViewContext
{
    /// <summary>Creates the view context.</summary>
    /// <param name="document">The document instance that owns the view.</param>
    /// <param name="view">The view descriptor.</param>
    /// <param name="commands">The command service of the current host scope.</param>
    /// <param name="documents">The document service that owns the instance.</param>
    /// <param name="localizer">The localizer of the current host scope.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LierneDocumentViewContext(
        LierneDocumentInstance document,
        DocumentViewDescriptor view,
        ILierneCommandService commands,
        ILierneDocumentService documents,
        ILierneLocalizer localizer,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(services);
        Document = document;
        View = view;
        Commands = commands;
        Documents = documents;
        Localizer = localizer;
        Services = services;
    }

    /// <summary>Gets the document instance that owns the view.</summary>
    public LierneDocumentInstance Document { get; }

    /// <summary>Gets the view descriptor.</summary>
    public DocumentViewDescriptor View { get; }

    /// <summary>Gets the state saved for this view, when any.</summary>
    public object? State =>
        Document.ViewStates is { } states && states.TryGetValue(View.Id, out var state) ? state : null;

    /// <summary>Gets the command service of the current host scope.</summary>
    public ILierneCommandService Commands { get; }

    /// <summary>Gets the document service that owns the instance.</summary>
    public ILierneDocumentService Documents { get; }

    /// <summary>Gets the localizer of the current host scope.</summary>
    public ILierneLocalizer Localizer { get; }

    /// <summary>Gets the scoped service provider of the running host.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Stores a state value for this view.</summary>
    /// <param name="state">The view-owned state, or <see langword="null"/> to remove it.</param>
    /// <returns>Whether the document and view exist.</returns>
    public LierneResult SetState(object? state) =>
        Documents.SetViewState(Document.Id, View.Id, state);

    /// <summary>Selects another view of the same document.</summary>
    /// <param name="view">The view id.</param>
    /// <returns>Whether the document exists and the view belongs to its type.</returns>
    public LierneResult SelectView(DocumentViewId view) =>
        Documents.SetSelectedView(Document.Id, view);

    /// <summary>Executes a command in the current scope.</summary>
    /// <param name="command">The command id.</param>
    /// <param name="argument">The typed argument, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command result.</returns>
    public Task<LierneCommandResult> ExecuteAsync(
        CommandId command,
        object? argument = null,
        CancellationToken cancellationToken = default) =>
        Commands.ExecuteAsync(command, argument, cancellationToken);

    /// <summary>Closes this document.</summary>
    /// <returns>Whether the document existed and was closed.</returns>
    public LierneResult Close() => Documents.Close(Document.Id);
}
