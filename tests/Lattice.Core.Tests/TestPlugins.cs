using Lattice;

namespace Lattice.Core.Tests;

/// <summary>A plugin defined by a test with an optional configuration callback.</summary>
internal sealed class TestPlugin : ILatticePlugin
{
    private readonly Action<ILatticePluginBuilder>? _configure;

    public TestPlugin(string id, params string[] dependsOn)
        : this(id, null, dependsOn)
    {
    }

    public TestPlugin(
        string id,
        Action<ILatticePluginBuilder>? configure,
        params string[] dependsOn)
    {
        Id = new PluginId(id);
        DisplayName = TestDescriptors.Key(id);
        DependsOn = dependsOn.Select(value => new PluginId(value)).ToArray();
        _configure = configure;
    }

    public PluginId Id { get; }

    public StringKey DisplayName { get; }

    public IReadOnlyList<PluginId> DependsOn { get; }

    public void Configure(ILatticePluginBuilder builder) => _configure?.Invoke(builder);
}

/// <summary>Descriptor factories and handlers shared by the Lattice tests.</summary>
internal static class TestDescriptors
{
    public static StringKey Key(string name) => new("tests", name);

    public static CommandDescriptor Command(string id, Type? argumentType = null) =>
        new(new CommandId(id), Key(id), ArgumentType: argumentType);

    public static Task<LatticeCommandResult> NoOp(
        LatticeCommandContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(LatticeCommandResult.Ok());
}
