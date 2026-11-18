namespace Lattice;

/// <summary>Stores each Lattice state section in an atomically replaced JSON file.</summary>
public sealed class LatticeFileStateStorage : ILatticeStateStorage
{
    private readonly string _directory;

    /// <summary>Creates a state store in the application data directory chosen by the host.</summary>
    /// <param name="directory">Directory holding the state files.</param>
    public LatticeFileStateStorage(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <inheritdoc />
    public async ValueTask<string?> ReadAsync(
        LatticeStateSection section, CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = new FileStream(
                SectionPath(section), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 4096, useAsync: true);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        LatticeStateSection section, string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        var path = SectionPath(section);
        Directory.CreateDirectory(_directory);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private string SectionPath(LatticeStateSection section) => Path.Combine(_directory, section switch
    {
        LatticeStateSection.Preferences => "preferences.json",
        LatticeStateSection.Workspace => "workspace.json",
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    });
}
