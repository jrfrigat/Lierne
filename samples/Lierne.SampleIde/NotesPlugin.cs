using System.Resources;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne.SampleIde;

public sealed class NotesPlugin : ILiernePlugin
{
    private const string Namespace = "sample.notes";
    private static readonly DocumentTypeId Note = new("sample.note");
    private static readonly SettingsKey NoteTitle = new("sample.noteTitle");
    public static readonly CommandId NewNote = new("sample.newNote");

    public PluginId Id => new(Namespace);
    public StringKey DisplayName => Text("app");
    public IReadOnlyList<PluginId> DependsOn => [];

    public void Configure(ILiernePluginBuilder builder)
    {
        var group = new RibbonGroupId("sample.notes");
        var page = new SettingsPageId("sample.notes");
        builder.AddStrings(new(Namespace, new ResourceManager("Lierne.SampleIde.Resources.SampleStrings", typeof(NotesPlugin).Assembly)));
        builder.AddDocument(new DocumentDescriptor(Note, Text("note"), null, typeof(NoteDocument))
        {
            StateCodec = new LierneDocumentStateCodec(1, payload => JsonSerializer.SerializeToElement(payload as string),
                (version, json) => version == 1 ? json.GetString() : throw new JsonException("Unknown note version."))
        });
        builder.AddToolWindow(new ToolWindowDescriptor(new("sample.tools"), Text("tools"), null, typeof(NotesTool)));
        builder.AddSettingsPage(new SettingsPageDescriptor(page, Text("settings")));
        builder.AddSetting(new SettingDescriptor(NoteTitle, Text("noteTitle"), LierneSettingKind.Text, Page: page));
        builder.AddCommand(new CommandDescriptor(NewNote, Text("newNote"), DefaultShortcut: "Ctrl+Alt+N"), OpenNote);
        builder.AddRibbonGroup(new RibbonGroupDescriptor(group, Text("tools")));
        builder.AddRibbonItem(new RibbonItemDescriptor(new("sample.notes.new"), group, NewNote));
    }

    public static StringKey Text(string name) => new(Namespace, name);

    private static Task<LierneCommandResult> OpenNote(LierneCommandContext context, CancellationToken cancellationToken)
    {
        var title = context.Services.GetRequiredService<ISettingsStore>().Get<string>(NoteTitle);
        var result = context.Services.GetRequiredService<ILierneDocumentService>().Open(
            Note, string.IsNullOrWhiteSpace(title) ? null : StringKey.Literal(title), payload: string.Empty);
        return Task.FromResult(result.Success
            ? LierneCommandResult.Ok(result.Data)
            : LierneCommandResult.Fail(result.ErrorCode!, result.ErrorMessage!));
    }
}
