using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Native;

namespace SteamKOntroller.Core.Diagnostics;

public sealed class BridgeCounters
{
    private long _hookInstalled;
    private long _hookEvents;
    private long _steamCandidates;
    private long _suppressedEvents;
    private long _reinjectedEvents;
    private long _loopGuardedEvents;
    private long _sendInputFailures;
    private long _lastVirtualKey;
    private long _lastScanCode;
    private long _lastEventTicks;

    public void SetHookInstalled(bool installed) => Interlocked.Exchange(ref _hookInstalled, installed ? 1 : 0);

    public void RecordHookEvent(LowLevelKeyboardEvent keyboardEvent)
    {
        Interlocked.Increment(ref _hookEvents);
        Interlocked.Exchange(ref _lastVirtualKey, keyboardEvent.VirtualKey);
        Interlocked.Exchange(ref _lastScanCode, keyboardEvent.ScanCode);
        Interlocked.Exchange(ref _lastEventTicks, keyboardEvent.Timestamp.UtcTicks);
    }

    public void IncrementSteamCandidates() => Interlocked.Increment(ref _steamCandidates);
    public void IncrementSuppressed() => Interlocked.Increment(ref _suppressedEvents);
    public void IncrementReinjected() => Interlocked.Increment(ref _reinjectedEvents);
    public void IncrementLoopGuarded() => Interlocked.Increment(ref _loopGuardedEvents);
    public void IncrementSendInputFailures() => Interlocked.Increment(ref _sendInputFailures);

    public void Reset(bool hookInstalled)
    {
        Interlocked.Exchange(ref _hookInstalled, hookInstalled ? 1 : 0);
        Interlocked.Exchange(ref _hookEvents, 0);
        Interlocked.Exchange(ref _steamCandidates, 0);
        Interlocked.Exchange(ref _suppressedEvents, 0);
        Interlocked.Exchange(ref _reinjectedEvents, 0);
        Interlocked.Exchange(ref _loopGuardedEvents, 0);
        Interlocked.Exchange(ref _sendInputFailures, 0);
        Interlocked.Exchange(ref _lastVirtualKey, 0);
        Interlocked.Exchange(ref _lastScanCode, 0);
        Interlocked.Exchange(ref _lastEventTicks, 0);
    }

    public BridgeCounterSnapshot Snapshot()
    {
        var ticks = Volatile.Read(ref _lastEventTicks);
        return new BridgeCounterSnapshot(
            Volatile.Read(ref _hookInstalled) == 1,
            Volatile.Read(ref _hookEvents),
            Volatile.Read(ref _steamCandidates),
            Volatile.Read(ref _suppressedEvents),
            Volatile.Read(ref _reinjectedEvents),
            Volatile.Read(ref _loopGuardedEvents),
            Volatile.Read(ref _sendInputFailures),
            (ushort)Volatile.Read(ref _lastVirtualKey),
            (ushort)Volatile.Read(ref _lastScanCode),
            ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero).ToLocalTime());
    }
}

public sealed record BridgeCounterSnapshot(
    bool HookInstalled,
    long HookEvents,
    long SteamCandidates,
    long SuppressedEvents,
    long ReinjectedEvents,
    long LoopGuardedEvents,
    long SendInputFailures,
    ushort LastVirtualKey,
    ushort LastScanCode,
    DateTimeOffset? LastEventAt)
{
    public string LastKeyText => LastVirtualKey == 0
        ? "-"
        : $"{VirtualKeys.NameOf(LastVirtualKey)} / 0x{LastScanCode:X2}";
}
