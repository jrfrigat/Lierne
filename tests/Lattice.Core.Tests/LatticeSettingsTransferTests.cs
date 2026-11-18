using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Lattice.Core.Tests;

/// <summary>Tests the settings document the Import/Export page builds and applies.</summary>
public sealed class LatticeSettingsTransferTests
{
    [Fact]
    public void ExportThenImportRestoresEverySection()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["first"] = "old-first",
            ["second"] = "old-second"
        };
        var transfer = CreateTransfer(state);

        var exported = transfer.Export(EmptyServices);
        Assert.True(exported.Success);
        Assert.NotNull(exported.Data);

        state["first"] = "new-first";
        state["second"] = "new-second";

        var imported = transfer.Import(exported.Data!, EmptyServices);

        Assert.True(imported.Success);
        Assert.Equal("old-first", state["first"]);
        Assert.Equal("old-second", state["second"]);
    }

    [Fact]
    public void FailedImportRestoresTheSectionsAppliedBeforeIt()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["first"] = "old-first",
            ["second"] = "old-second"
        };
        var transfer = CreateTransfer(state, failSecond: true);
        var document = """
            {
              "format": "lattice.settings",
              "version": 1,
              "sections": {
                "app.first": "changed-first",
                "app.second": "changed-second"
              }
            }
            """;

        var imported = transfer.Import(document, EmptyServices);

        Assert.False(imported.Success);
        Assert.Equal("app.second.rejected", imported.ErrorCode);
        Assert.Equal("old-first", state["first"]);
        Assert.Equal("old-second", state["second"]);
    }

    [Fact]
    public void SnapshotFailurePreventsWritesToEarlierSections()
    {
        var writes = 0;
        var transfer = CreateTransfer(
            new SettingsTransferDescriptor("app.first", TestDescriptors.Key("first"),
                _ => JsonValue.Create("old"),
                (_, _) =>
                {
                    writes++;
                    return LatticeResult.Ok;
                }),
            new SettingsTransferDescriptor("app.second", TestDescriptors.Key("second"),
                _ => throw new IOException("Snapshot unavailable"),
                (_, _) => LatticeResult.Ok));

        var imported = transfer.Import(
            """{"format":"lattice.settings","version":1,"sections":{"app.first":"new","app.second":"bad"}}""",
            EmptyServices);

        Assert.Equal("lattice.settings.snapshot_failed", imported.ErrorCode);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void ASectionThatThrowsAfterWritingIsRestored()
    {
        var current = "old";
        var transfer = CreateTransfer(new SettingsTransferDescriptor(
            "app.first", TestDescriptors.Key("first"),
            _ => JsonValue.Create(current),
            (_, value) =>
            {
                current = value!.GetValue<string>();
                return current == "new"
                    ? throw new IOException("Failed after applying")
                    : LatticeResult.Ok;
            }));

        var imported = transfer.Import(
            """{"format":"lattice.settings","version":1,"sections":{"app.first":"new"}}""",
            EmptyServices);

        Assert.Equal("lattice.settings.import_failed", imported.ErrorCode);
        Assert.Equal("old", current);
    }

    [Fact]
    public void RejectedRestorationReportsWhichSectionCouldNotBeRestored()
    {
        var transfer = CreateTransfer(
            new SettingsTransferDescriptor("app.first", TestDescriptors.Key("first"),
                _ => JsonValue.Create("old"),
                (_, value) => value!.GetValue<string>() == "old"
                    ? LatticeResult.Fail("app.first.rejected", "Cannot restore first.")
                    : LatticeResult.Ok),
            new SettingsTransferDescriptor("app.second", TestDescriptors.Key("second"),
                _ => JsonValue.Create("old"),
                (_, _) => LatticeResult.Fail("app.second.rejected", "Cannot import second.")));

        var imported = transfer.Import(
            """{"format":"lattice.settings","version":1,"sections":{"app.first":"new","app.second":"bad"}}""",
            EmptyServices);

        Assert.Equal("lattice.settings.rollback_failed", imported.ErrorCode);
        Assert.Contains("app.first", imported.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Cannot import second", imported.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSectionKeepsItsCurrentValue()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["first"] = "old-first",
            ["second"] = "old-second"
        };
        var transfer = CreateTransfer(state);
        var document = """
            {
              "format": "lattice.settings",
              "version": 1,
              "sections": { "app.first": "changed-first" }
            }
            """;

        var imported = transfer.Import(document, EmptyServices);

        Assert.True(imported.Success);
        Assert.Equal("changed-first", state["first"]);
        Assert.Equal("old-second", state["second"]);
    }

    [Theory]
    [InlineData("", "lattice.settings.empty")]
    [InlineData("{", "lattice.settings.invalid_json")]
    [InlineData("[]", "lattice.settings.invalid_format")]
    [InlineData("""{"format":"other","version":1,"sections":{}}""", "lattice.settings.invalid_format")]
    [InlineData("""{"format":"lattice.settings","version":99,"sections":{}}""", "lattice.settings.unsupported_version")]
    [InlineData("""{"format":"lattice.settings","version":1}""", "lattice.settings.missing_sections")]
    [InlineData("""{"format":"lattice.settings","version":1,"sections":{"app.unknown":{}}}""", "lattice.settings.unknown_section")]
    [InlineData("""{"format":"lattice.settings","version":1,"sections":{"app.first":1,"app.first":2}}""", "lattice.settings.duplicate_section")]
    [InlineData("""{"format":"lattice.settings","\u0066ormat":"lattice.settings","version":1,"sections":{}}""", "lattice.settings.duplicate_property")]
    [InlineData("""{"format":"lattice.settings","version":1,"version":1,"sections":{}}""", "lattice.settings.duplicate_property")]
    public void RejectsDocumentsNamingTheProblem(string json, string errorCode)
    {
        var transfer = CreateTransfer(new Dictionary<string, string>(StringComparer.Ordinal));

        var imported = transfer.Import(json, EmptyServices);

        Assert.False(imported.Success);
        Assert.Equal(errorCode, imported.ErrorCode);
    }

    private static IServiceProvider EmptyServices => new ServiceCollection().BuildServiceProvider();

    private static LatticeSettingsTransfer CreateTransfer(params SettingsTransferDescriptor[] sections) =>
        new(LatticePluginCatalog.Build([new TestPlugin("app.settings", builder =>
        {
            foreach (var section in sections)
            {
                builder.AddSettingsTransfer(section);
            }
        })]));

    private static LatticeSettingsTransfer CreateTransfer(
        Dictionary<string, string> state,
        bool failSecond = false)
    {
        var plugin = new TestPlugin(
            "app.settings",
            builder =>
            {
                builder.AddSettingsTransfer(new SettingsTransferDescriptor(
                    "app.first",
                    TestDescriptors.Key("first"),
                    _ => JsonValue.Create(state["first"]),
                    (_, value) =>
                    {
                        state["first"] = value?.GetValue<string>() ?? string.Empty;
                        return LatticeResult.Ok;
                    }));
                builder.AddSettingsTransfer(new SettingsTransferDescriptor(
                    "app.second",
                    TestDescriptors.Key("second"),
                    _ => JsonValue.Create(state["second"]),
                    (_, value) =>
                    {
                        if (failSecond)
                        {
                            return LatticeResult.Fail(
                                "app.second.rejected",
                                "The second section rejects its value.");
                        }

                        state["second"] = value?.GetValue<string>() ?? string.Empty;
                        return LatticeResult.Ok;
                    }));
            });

        var catalog = LatticePluginCatalog.Build([plugin]);
        return new LatticeSettingsTransfer(catalog);
    }
}
