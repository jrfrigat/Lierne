namespace Lierne;

/// <summary>Carries the document and the scoped services to a plugin document component.</summary>
public sealed class LierneDocumentContext
{
    /// <summary>Creates the document context.</summary>
    /// <param name="document">The document instance being rendered.</param>
    /// <param name="commands">The command service of the current host scope.</param>
    /// <param name="documents">The document service that owns the instance.</param>
    /// <param name="localizer">The localizer of the current host scope.</param>
    /// <param name="services">The scoped service provider of the running host.</param>
    public LierneDocumentContext(
        LierneDocumentInstance document,
        ILierneCommandService commands,
        ILierneDocumentService documents,
        ILierneLocalizer localizer,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(services);
        Document = document;
        Commands = commands;
        Documents = documents;
        Localizer = localizer;
        Services = services;
    }

    /// <summary>Gets the document instance being rendered.</summary>
    public LierneDocumentInstance Document { get; }

    /// <summary>Gets the command service of the current host scope.</summary>
    public ILierneCommandService Commands { get; }

    /// <summary>Gets the document service that owns the instance.</summary>
    public ILierneDocumentService Documents { get; }

    /// <summary>Gets the localizer of the current host scope.</summary>
    public ILierneLocalizer Localizer { get; }

    /// <summary>Gets the scoped service provider of the running host.</summary>
    public IServiceProvider Services { get; }

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
