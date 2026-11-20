using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Lierne.Core.Tests;

public sealed class LierneStatePersistenceTests
{
    private static readonly SettingsKey Number = new("state.number");
    private static readonly CommandId Run = new("state.run");
    private static readonly ToolWindowId Tool = new("state.tool");
    private static readonly DocumentTypeId Note = new("state.note");
    private static readonly DocumentTypeId Temporary = new("state.temporary");

    [Fact]
    public async Task RestoresPreferencesToolsDocumentsAndTheirLayoutAcrossScopes()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var storage = new MemoryStorage();
        string noteId;
        using (var app = BuildProvider(storage, 1))
        using (var scope = app.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var persistence = provider.GetRequiredService<LierneStatePersistence>();
            await persistence.RestorePreferencesAsync(cancellation);
            await persistence.RestoreWorkspaceAsync(cancellation);

            var settings = provider.GetRequiredService<ISettingsStore>();
            Assert.True(settings.Set(Number, 7).Success);
            var shortcuts = provider.GetRequiredService<ILierneShortcutService>();
            Assert.True(shortcuts.ApplyOverrides(new Dictionary<string, string> { [Run.Value] = "" }).Success);
            Assert.True(provider.GetRequiredService<ILierneToolWindowService>().Hide(Tool).Success);

            var documents = provider.GetRequiredService<ILierneDocumentService>();
            var note = documents.Open(Note, StringKey.Literal("Workspace note"), "original", LierneDockLocation.Right).Data!;
            noteId = note.Id;
            documents.SetPayload(noteId, "updated");
            documents.SetIcon(noteId, "note", "#224466");
            documents.Open(Temporary);
            documents.Activate(noteId);
            await persistence.SavePreferencesAsync(cancellation);
            await persistence.SaveWorkspaceAsync(new LierneWorkspaceLayout
            {
                Right = new LiernePanelState(420, Pinned: false, Collapsed: true, ToolKey: Tool.Value),
                Documents = new Dictionary<LierneDockLocation, string?> { [LierneDockLocation.Right] = noteId }
            }, cancellation);
        }

        using var restoredApp = BuildProvider(storage, 2);
        using var restoredScope = restoredApp.CreateScope();
        var restoredProvider = restoredScope.ServiceProvider;
        var restoredPersistence = restoredProvider.GetRequiredService<LierneStatePersistence>();
        await restoredPersistence.RestorePreferencesAsync(cancellation);
        var layout = await restoredPersistence.RestoreWorkspaceAsync(cancellation);

        Assert.Equal(7, restoredProvider.GetRequiredService<ISettingsStore>().Get<int>(Number));
        Assert.False(restoredProvider.GetRequiredService<ILierneShortcutService>().Resolve("Ctrl+R").Success);
        Assert.False(restoredProvider.GetRequiredService<ILierneToolWindowService>().IsVisible(Tool));
        Assert.Equal(new LiernePanelState(420, false, true, Tool.Value), layout!.Right);
        Assert.Equal(noteId, layout.Documents[LierneDockLocation.Right]);

        var restoredDocuments = restoredProvider.GetRequiredService<ILierneDocumentService>();
        var reopened = Assert.Single(restoredDocuments.Documents);
        Assert.Equal(noteId, reopened.Id);
        Assert.Equal(noteId, restoredDocuments.Active?.Id);
        Assert.Equal(StringKey.Literal("Workspace note"), reopened.Title);
        Assert.Equal(LierneDockLocation.Right, reopened.Location);
        Assert.Equal("from-v1:updated", reopened.Payload);
        Assert.Equal("note", reopened.IconKey);
        Assert.Equal("#224466", reopened.IconColor);
        Assert.NotEqual(noteId, restoredDocuments.Open(Note).Data!.Id);
    }

    [Fact]
    public async Task MigratesLegacyPreferencesAndRetainsOverridesFromMissingPlugins()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var storage = new MemoryStorage();
        await storage.WriteAsync(LierneStateSection.Preferences, """
            {"version":0,"settings":{"old-number":6,"missing.plugin":"keep"},"shortcuts":{"missing.run":"Ctrl+M"}}
            """, cancellation);
        var migration = new LierneStateMigration(LierneStateSection.Preferences, 0, root =>
        {
            var settings = root["settings"]!.AsObject();
            settings[Number.Value] = settings["old-number"]!.DeepClone();
            settings.Remove("old-number");
        });
        using var app = BuildProvider(storage, 1, migration);
        using var scope = app.CreateScope();
        var persistence = scope.ServiceProvider.GetRequiredService<LierneStatePersistence>();

        await persistence.RestorePreferencesAsync(cancellation);
        Assert.Equal(6, scope.ServiceProvider.GetRequiredService<ISettingsStore>().Get<int>(Number));
        await persistence.SavePreferencesAsync(cancellation);

        using var updated = JsonDocument.Parse((await storage.ReadAsync(LierneStateSection.Preferences, cancellation))!);
        Assert.Equal(1, updated.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("keep", updated.RootElement.GetProperty("settings").GetProperty("missing.plugin").GetString());
        Assert.Equal("Ctrl+M", updated.RootElement.GetProperty("shortcuts").GetProperty("missing.run").GetString());
    }

    [Fact]
    public async Task ANewerPreferenceSnapshotIsPreservedWhenItCannotBeLoaded()
    {
        var cancellation = TestContext.Current.CancellationToken;
        const string future = """{"version":99}""";
        var storage = new MemoryStorage();
        await storage.WriteAsync(LierneStateSection.Preferences, future, cancellation);
        using var app = BuildProvider(storage, 1);
        using var scope = app.CreateScope();
        var persistence = scope.ServiceProvider.GetRequiredService<LierneStatePersistence>();

        await Assert.ThrowsAsync<InvalidDataException>(() => persistence.RestorePreferencesAsync(cancellation));
        await persistence.SavePreferencesAsync(cancellation);

        Assert.Equal(future, await storage.ReadAsync(LierneStateSection.Preferences, cancellation));
    }

    [Fact]
    public async Task FileStorageReplacesOnlyTheRequestedSection()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("lierne-state-");
        try
        {
            var storage = new LierneFileStateStorage(directory.FullName);
            Assert.Null(await storage.ReadAsync(LierneStateSection.Workspace, cancellation));
            await storage.WriteAsync(LierneStateSection.Preferences, "first", cancellation);
            await storage.WriteAsync(LierneStateSection.Workspace, "layout", cancellation);
            await storage.WriteAsync(LierneStateSection.Preferences, "second", cancellation);

            Assert.Equal("second", await storage.ReadAsync(LierneStateSection.Preferences, cancellation));
            Assert.Equal("layout", await storage.ReadAsync(LierneStateSection.Workspace, cancellation));
            Assert.Equal(2, directory.GetFiles().Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static ServiceProvider BuildProvider(
        ILierneStateStorage storage, int payloadVersion, LierneStateMigration? migration = null)
    {
        var services = new ServiceCollection().AddSingleton(storage);
        if (migration is not null)
        {
            services.AddSingleton(migration);
        }

        services.AddLierne(options => options.AddPlugin(new TestPlugin("state", plugin =>
        {
            plugin.AddSetting(new SettingDescriptor(Number, TestDescriptors.Key("number"), LierneSettingKind.Integer, 1));
            plugin.AddCommand(TestDescriptors.Command(Run.Value) with { DefaultShortcut = "Ctrl+R" }, TestDescriptors.NoOp);
            plugin.AddToolWindow(new ToolWindowDescriptor(Tool, TestDescriptors.Key("tool"), null, typeof(object)));
            plugin.AddDocument(new DocumentDescriptor(Temporary, TestDescriptors.Key("temporary"), null, typeof(object),
                Persistent: false));
            plugin.AddDocument(new DocumentDescriptor(Note, TestDescriptors.Key("note"), null, typeof(object))
            {
                StateCodec = new LierneDocumentStateCodec(payloadVersion,
                    payload => JsonSerializer.SerializeToElement(payload),
                    (version, payload) => version == payloadVersion ? payload.GetString() : $"from-v{version}:{payload.GetString()}")
            });
        })));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class MemoryStorage : ILierneStateStorage
    {
        private readonly Dictionary<LierneStateSection, string> _snapshots = [];

        public ValueTask<string?> ReadAsync(LierneStateSection section, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_snapshots.GetValueOrDefault(section));

        public ValueTask WriteAsync(LierneStateSection section, string json, CancellationToken cancellationToken = default)
        {
            _snapshots[section] = json;
            return ValueTask.CompletedTask;
        }
    }
}
