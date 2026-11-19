using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lattice;

/// <summary>Restores and saves the preferences and workspace of one Lattice scope.</summary>
public sealed class LatticeStatePersistence(
    LatticePluginCatalog catalog,
    ISettingsStore settings,
    ILatticeShortcutService shortcuts,
    ILatticeDocumentService documents,
    ILatticeToolWindowService tools,
    ILatticeStateStorage? storage = null,
    IEnumerable<LatticeStateMigration>? migrations = null)
{
    private const int Version = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _writes = new(1);
    private readonly Lock _workspaceQueue = new();
    private readonly Dictionary<DocumentTypeId, DocumentDescriptor> _persistentTypes =
        catalog.Documents.Where(type => type.Persistent).ToDictionary(type => type.Id);
    private WorkspaceSave? _pendingWorkspace;
    private Task? _workspaceWrites;
    private bool _preferencesLoaded;
    private bool _workspaceLoaded;
    private LatticeWorkspaceLayout? _layout;
    private PreferencesState? _storedPreferences;

    /// <summary>Gets whether the host registered storage for this scope.</summary>
    public bool IsEnabled => storage is not null;

    /// <summary>Applies supported saved settings and shortcut overrides before the host starts.</summary>
    public async Task RestorePreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (storage is null || _preferencesLoaded)
        {
            return;
        }

        var text = await storage.ReadAsync(LatticeStateSection.Preferences, cancellationToken);
        if (text is not null)
        {
            var snapshot = Read<PreferencesState>(LatticeStateSection.Preferences, text);
            _storedPreferences = snapshot;
            foreach (var descriptor in catalog.Settings)
            {
                if (snapshot.Settings?.TryGetValue(descriptor.Key.Value, out var value) == true &&
                    TryReadSetting(descriptor.Kind, value, out var typed))
                {
                    settings.Set(descriptor.Key, typed);
                }
            }

            var known = shortcuts.Bindings.Select(binding => binding.Command.Value).ToHashSet(StringComparer.Ordinal);
            var overrides = snapshot.Shortcuts?.Where(pair => known.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty, StringComparer.Ordinal);
            shortcuts.ApplyOverrides(overrides);
        }

        _preferencesLoaded = true;
    }

    /// <summary>Applies the stored tool visibility and restores eligible documents into an empty session.</summary>
    /// <returns>The saved shell layout, when one exists.</returns>
    public async Task<LatticeWorkspaceLayout?> RestoreWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (storage is null || _workspaceLoaded)
        {
            return _layout;
        }

        var text = await storage.ReadAsync(LatticeStateSection.Workspace, cancellationToken);
        if (text is not null)
        {
            var snapshot = Read<WorkspaceState>(LatticeStateSection.Workspace, text);
            foreach (var tool in tools.ToolWindows)
            {
                if (snapshot.Tools?.TryGetValue(tool.Id.Value, out var visible) == true)
                {
                    _ = visible ? tools.Show(tool.Id) : tools.Hide(tool.Id);
                }
            }

            _layout = snapshot.Layout;
            if (documents is LatticeDocumentService service && service.Documents.Count == 0)
            {
                service.Restore(ReadDocuments(snapshot.Documents ?? []), snapshot.ActiveDocument);
            }
            else if (_layout is not null)
            {
                _layout = _layout with { Documents = [] };
            }
        }

        _workspaceLoaded = true;
        return _layout;
    }

    /// <summary>Enqueues the current preference overrides for storage after a successful load.</summary>
    public Task SavePreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (!_preferencesLoaded)
        {
            return Task.CompletedTask;
        }

        // Keep keys contributed by plugins that are absent in this installation.
        var known = catalog.Settings.Select(setting => setting.Key.Value).ToHashSet(StringComparer.Ordinal);
        var values = _storedPreferences?.Settings?.Where(pair => !known.Contains(pair.Key))
            .ToDictionary(StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        foreach (var setting in catalog.Settings)
        {
            var value = settings.GetValue(setting.Key);
            if (!Equals(value, setting.DefaultValue))
            {
                values.Add(setting.Key.Value, JsonSerializer.SerializeToElement(value, Json));
            }
        }

        var knownCommands = shortcuts.Bindings.Select(binding => binding.Command.Value).ToHashSet(StringComparer.Ordinal);
        var overrides = _storedPreferences?.Shortcuts?.Where(pair => !knownCommands.Contains(pair.Key))
            .ToDictionary(StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        foreach (var binding in shortcuts.Bindings)
        {
            if (!string.Equals(binding.DefaultShortcut, binding.EffectiveShortcut, StringComparison.OrdinalIgnoreCase))
            {
                overrides.Add(binding.Command.Value, binding.EffectiveShortcut);
            }
        }

        return WriteAsync(LatticeStateSection.Preferences, new PreferencesState(Version, values, overrides), cancellationToken);
    }

    /// <summary>Enqueues open persistent documents, tools, and the host layout after a successful load.</summary>
    public Task SaveWorkspaceAsync(LatticeWorkspaceLayout layout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!_workspaceLoaded)
        {
            return Task.CompletedTask;
        }

        return WriteAsync(LatticeStateSection.Workspace, CaptureWorkspace(layout), cancellationToken);
    }

    internal void QueueWorkspaceSave(Func<LatticeWorkspaceLayout> getLayout, Action<Exception> reportError)
    {
        if (!_workspaceLoaded)
        {
            return;
        }

        lock (_workspaceQueue)
        {
            _pendingWorkspace = new WorkspaceSave(getLayout, reportError);
            _workspaceWrites ??= WritePendingWorkspacesAsync();
        }
    }

    private async Task WritePendingWorkspacesAsync()
    {
        // Combine changes from the same renderer dispatch before taking the first snapshot.
        await Task.Yield();
        while (true)
        {
            lock (_workspaceQueue)
            {
                if (_pendingWorkspace is null)
                {
                    _workspaceWrites = null;
                    return;
                }
            }

            await _writes.WaitAsync();
            WorkspaceSave request;
            lock (_workspaceQueue)
            {
                // Only this worker takes requests, so one is still pending after acquiring storage.
                request = _pendingWorkspace!.Value;
                _pendingWorkspace = null;
            }

            Exception? failure = null;
            try
            {
                var text = JsonSerializer.Serialize(CaptureWorkspace(request.GetLayout()), Json);
                await storage!.WriteAsync(LatticeStateSection.Workspace, text);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                _writes.Release();
            }
            if (failure is not null)
            {
                request.ReportError(failure);
            }
        }
    }

    /// <summary>Waits for the writes currently queued by this scope.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Task? workspaceWrites;
        lock (_workspaceQueue)
        {
            workspaceWrites = _workspaceWrites;
        }
        if (workspaceWrites is not null)
        {
            await workspaceWrites.WaitAsync(cancellationToken);
        }

        await _writes.WaitAsync(cancellationToken);
        _writes.Release();
    }

    private WorkspaceState CaptureWorkspace(LatticeWorkspaceLayout layout)
    {
        _layout = layout;
        var openDocuments = documents.Documents;
        var savedDocuments = new List<DocumentState>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < openDocuments.Count; index++)
        {
            var document = openDocuments[index];
            if (!_persistentTypes.TryGetValue(document.TypeId, out var type) ||
                (type.StateCodec is null && document.Payload is not null))
            {
                continue;
            }

            savedDocuments.Add(new DocumentState(
                document.Id, document.TypeId.Value, document.Title.Namespace, document.Title.Name,
                document.Location, document.IconKey, document.IconColor, type.StateCodec?.Version ?? 0,
                type.StateCodec?.Write(document.Payload)));
            ids.Add(document.Id);
        }

        var selection = new Dictionary<LatticeDockLocation, string?>(layout.Documents.Count);
        foreach (var (location, id) in layout.Documents)
        {
            if (id is null || ids.Contains(id))
            {
                selection.Add(location, id);
            }
        }

        var active = documents.Active?.Id;
        return new WorkspaceState(
            Version,
            savedDocuments,
            active is not null && ids.Contains(active) ? active : null,
            tools.ToolWindows.ToDictionary(tool => tool.Id.Value, tool => tool.Visible, StringComparer.Ordinal),
            layout with { Documents = selection });
    }

    private List<LatticeDocumentInstance> ReadDocuments(IReadOnlyList<DocumentState> saved)
    {
        var restored = new List<LatticeDocumentInstance>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in saved)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Type) ||
                string.IsNullOrWhiteSpace(entry.TitleNamespace) || string.IsNullOrWhiteSpace(entry.TitleName) ||
                !Enum.IsDefined(entry.Location) || !LatticeDocumentService.TryReadSequence(entry.Id, out _) ||
                !_persistentTypes.TryGetValue(new DocumentTypeId(entry.Type), out var type) || !ids.Add(entry.Id))
            {
                continue;
            }

            object? payload = null;
            if (entry.PayloadVersion != 0)
            {
                // A JSON-null codec value is deserialized as a nullable JsonElement with no value.
                if (entry.PayloadVersion < 1 || type.StateCodec is not { } codec)
                {
                    continue;
                }

                try
                {
                    payload = codec.Read(entry.PayloadVersion, entry.Payload ?? JsonSerializer.SerializeToElement<object?>(null));
                }
                catch (Exception exception)
                {
                    Trace.TraceWarning("Could not restore Lattice document {0} of type {1}: {2}",
                        entry.Id, entry.Type, exception.GetType().Name);
                    continue;
                }
            }

            restored.Add(new LatticeDocumentInstance(
                entry.Id, type.Id, new StringKey(entry.TitleNamespace, entry.TitleName),
                payload, entry.Location, entry.IconKey, entry.IconColor));
        }

        return restored;
    }

    private static bool TryReadSetting(LatticeSettingKind kind, JsonElement source, out object? value)
    {
        value = null;
        switch (source.ValueKind)
        {
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.String when kind is LatticeSettingKind.Text or LatticeSettingKind.Select:
                value = source.GetString();
                return true;
            case JsonValueKind.True or JsonValueKind.False when kind == LatticeSettingKind.Boolean:
                value = source.GetBoolean();
                return true;
            case JsonValueKind.Number when kind == LatticeSettingKind.Integer && source.TryGetInt32(out var number):
                value = number;
                return true;
            default:
                return false;
        }
    }

    private T Read<T>(LatticeStateSection section, string text) where T : class
    {
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("version", out var versionNode) ||
            versionNode.ValueKind != JsonValueKind.Number ||
            !versionNode.TryGetInt32(out var version) || version is < 0 or > Version)
        {
            throw new InvalidDataException("The Lattice state document version is unsupported.");
        }

        if (version == Version)
        {
            return document.RootElement.Deserialize<T>(Json)!;
        }

        var state = JsonNode.Parse(text)!.AsObject();
        while (version < Version)
        {
            var migration = migrations?.SingleOrDefault(candidate =>
                candidate.Section == section && candidate.FromVersion == version);
            if (migration is null)
            {
                throw new InvalidDataException($"No Lattice migration is registered for {section} version {version}.");
            }

            migration.Upgrade(state);
            state["version"] = ++version;
        }

        return state.Deserialize<T>(Json)!;
    }

    private async Task WriteAsync<T>(LatticeStateSection section, T snapshot, CancellationToken cancellationToken)
    {
        var text = JsonSerializer.Serialize(snapshot, Json);
        await _writes.WaitAsync(cancellationToken);
        try
        {
            await storage!.WriteAsync(section, text, cancellationToken);
        }
        finally
        {
            _writes.Release();
        }
    }

    private sealed record PreferencesState(
        int Version, Dictionary<string, JsonElement>? Settings, Dictionary<string, string?>? Shortcuts);

    private sealed record WorkspaceState(
        int Version, IReadOnlyList<DocumentState>? Documents, string? ActiveDocument,
        Dictionary<string, bool>? Tools, LatticeWorkspaceLayout? Layout);

    private sealed record DocumentState(
        string Id, string Type, string TitleNamespace, string TitleName, LatticeDockLocation Location,
        string? IconKey, string? IconColor, int PayloadVersion, JsonElement? Payload);

    private readonly record struct WorkspaceSave(Func<LatticeWorkspaceLayout> GetLayout, Action<Exception> ReportError);
}
