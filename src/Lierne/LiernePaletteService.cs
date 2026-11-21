namespace Lierne;

/// <summary>Controls the command palette hosted by the shell.</summary>
public sealed class LiernePaletteService
{
    private bool _isOpen;

    /// <summary>Raised after the palette is opened or closed.</summary>
    public event Action? Changed;

    /// <summary>Gets whether the palette is open.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Opens the palette.</summary>
    public void Open() => SetOpen(true);

    /// <summary>Closes the palette.</summary>
    public void Close() => SetOpen(false);

    /// <summary>Opens the palette when it is closed and closes it when it is open.</summary>
    public void Toggle() => SetOpen(!_isOpen);

    private void SetOpen(bool open)
    {
        if (_isOpen == open)
        {
            return;
        }

        _isOpen = open;
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("A Lierne palette observer failed: {0}", exception);
        }
    }
}
