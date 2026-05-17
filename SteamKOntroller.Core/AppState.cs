namespace SteamKOntroller.Core;

public sealed class AppState
{
    private int _enabled = 1;

    public event EventHandler<bool>? EnabledChanged;

    public bool Enabled => Volatile.Read(ref _enabled) == 1;

    public void SetEnabled(bool enabled)
    {
        var value = enabled ? 1 : 0;
        var previous = Interlocked.Exchange(ref _enabled, value);
        if (previous != value)
        {
            EnabledChanged?.Invoke(this, enabled);
        }
    }

    public bool ToggleEnabled()
    {
        while (true)
        {
            var current = Volatile.Read(ref _enabled);
            var next = current == 1 ? 0 : 1;
            if (Interlocked.CompareExchange(ref _enabled, next, current) == current)
            {
                var enabled = next == 1;
                EnabledChanged?.Invoke(this, enabled);
                return enabled;
            }
        }
    }
}
