using SteamKOntroller.Core;
using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Diagnostics;
using SteamKOntroller.Core.Native;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

var tests = new (string Name, Action Body)[]
{
    ("classifier accepts lower-integrity injected supported keys", ClassifierAcceptsSteamCandidate),
    ("classifier accepts injected packet ASCII letters without lower-integrity", ClassifierAcceptsInjectedPacketAsciiLettersWithoutLowerIntegrity),
    ("classifier ignores own reinjected marker", ClassifierIgnoresOwnMarker),
    ("policy suppresses candidate key down and reinjects", PolicySuppressesCandidateKeyDown),
    ("policy marks shifted Hangul double jamo keys for Shift mock", PolicyMarksShiftedHangulDoubleJamoKeysForShiftMock),
    ("policy does not mark other shifted letters for Shift mock", PolicyDoesNotMarkOtherShiftedLettersForShiftMock),
    ("policy does not mark shifted non-letter candidate for Shift mock", PolicyDoesNotMarkShiftedNonLetterCandidateForShiftMock),
    ("policy suppresses candidate key up without reinjecting", PolicySuppressesCandidateKeyUp),
    ("policy bypasses disabled bridge", PolicyBypassesDisabledBridge),
    ("policy bypasses shortcut modifiers", PolicyBypassesShortcutModifiers),
    ("policy converts packet sentinel key down to Hangul toggle", PolicyConvertsPacketSentinelKeyDownToHangulToggle),
    ("policy suppresses packet sentinel key up without reinjecting", PolicySuppressesPacketSentinelKeyUp),
    ("policy maps uppercase packet QWERTOP to shifted QWERTY scancodes", PolicyMapsUppercasePacketQwertopToShiftedQwertyScancodes),
    ("policy maps lowercase packet letter to unshifted QWERTY scancode", PolicyMapsLowercasePacketLetterToUnshiftedQwertyScancode),
    ("policy loop guards own Hangul reinjection", PolicyLoopGuardsOwnHangulReinjection),
    ("policy toggles on Ctrl+Alt+H", PolicyTogglesOnHotkey),
    ("diagnostics forwards redacted records when sensitive logging disabled", DiagnosticsForwardsRedactedRecordsWhenSensitiveLoggingDisabled),
    ("diagnostics writes sensitive records when enabled", DiagnosticsWritesSensitiveRecordsWhenEnabled),
    ("diagnostics event sink retains recent records", DiagnosticsEventSinkRetainsRecentRecords),
    ("counters do not retain last key", CountersDoNotRetainLastKey),
    ("send input result accepts expected Shift mock count", SendInputResultAcceptsExpectedShiftMockCount),
    ("app state toggles atomically", AppStateToggles)
};

foreach (var test in tests)
{
    test.Body();
    Console.WriteLine($"PASS {test.Name}");
}

static void ClassifierAcceptsSteamCandidate()
{
    var score = new SteamInputClassifier().Score(Key('G', 0x22));
    Assert(score.Score == 6, $"expected score 6, got {score.Score}");
    Assert(score.IsCandidate, "expected candidate");
}

static void ClassifierAcceptsInjectedPacketAsciiLettersWithoutLowerIntegrity()
{
    var score = new SteamInputClassifier().Score(Packet('Q', flags: LowLevelKeyboardFlags.Injected));
    Assert(score.Score == 6, $"expected score 6, got {score.Score}");
    Assert(score.IsCandidate, "injected packet ASCII letter must be a candidate");
}

static void ClassifierIgnoresOwnMarker()
{
    var score = new SteamInputClassifier().Score(Key('G', 0x22, extraInfo: InjectionMarker.Value));
    Assert(score.Score == 0, $"expected score 0, got {score.Score}");
    Assert(!score.IsCandidate, "own marker must not be candidate");
}

static void PolicySuppressesCandidateKeyDown()
{
    var classifier = new SteamInputClassifier();
    var evt = Key('G', 0x22);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressAndReinject, $"got {decision.Action}");
    Assert(!decision.MockShift, "plain candidate must not mock Shift");
}

static void PolicyMarksShiftedHangulDoubleJamoKeysForShiftMock()
{
    var classifier = new SteamInputClassifier();
    foreach (var virtualKey in "QWERTOP")
    {
        var evt = Key(virtualKey, 0x10);
        var decision = new SuppressionPolicy().Decide(evt, ShiftOnly(), enabled: true, classifier.Score(evt));
        Assert(decision.Action == BridgeAction.SuppressAndReinject, $"got {decision.Action}");
        Assert(decision.MockShift, $"shifted {virtualKey} must mock Shift");
    }
}

static void PolicyDoesNotMarkOtherShiftedLettersForShiftMock()
{
    var classifier = new SteamInputClassifier();
    var evt = Key('G', 0x22);
    var decision = new SuppressionPolicy().Decide(evt, ShiftOnly(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressAndReinject, $"got {decision.Action}");
    Assert(!decision.MockShift, "shifted letters outside QWERTOP must not mock Shift");
}

static void PolicyDoesNotMarkShiftedNonLetterCandidateForShiftMock()
{
    var classifier = new SteamInputClassifier();
    var evt = VirtualKey((ushort)VirtualKeys.VK_SPACE, scanCode: 0x39);
    var decision = new SuppressionPolicy().Decide(evt, ShiftOnly(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressAndReinject, $"got {decision.Action}");
    Assert(!decision.MockShift, "shifted non-letter candidate must not mock Shift");
}

static void PolicySuppressesCandidateKeyUp()
{
    var classifier = new SteamInputClassifier();
    var evt = Key('G', 0x22, WindowMessages.WM_KEYUP, LowLevelKeyboardFlags.Injected | LowLevelKeyboardFlags.LowerIntegrityInjected | LowLevelKeyboardFlags.Up);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressOnly, $"got {decision.Action}");
}

static void PolicyBypassesDisabledBridge()
{
    var classifier = new SteamInputClassifier();
    var evt = Key('G', 0x22);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: false, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.PassThrough, $"got {decision.Action}");
}

static void PolicyBypassesShortcutModifiers()
{
    var classifier = new SteamInputClassifier();
    var evt = Key('G', 0x22);
    var decision = new SuppressionPolicy().Decide(evt, new ModifierKeyState(Control: true, Alt: false, Windows: false, Shift: false), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.PassThrough, $"got {decision.Action}");
}

static void PolicyConvertsPacketSentinelKeyDownToHangulToggle()
{
    var classifier = new SteamInputClassifier();
    var evt = VirtualKey(
        (ushort)VirtualKeys.VK_PACKET,
        SupportedKeyPolicy.HangulToggleSentinelScanCode,
        flags: LowLevelKeyboardFlags.Injected);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressAndSendHangulToggle, $"got {decision.Action}");
}

static void PolicySuppressesPacketSentinelKeyUp()
{
    var classifier = new SteamInputClassifier();
    var evt = VirtualKey(
        (ushort)VirtualKeys.VK_PACKET,
        SupportedKeyPolicy.HangulToggleSentinelScanCode,
        WindowMessages.WM_KEYUP,
        LowLevelKeyboardFlags.Injected | LowLevelKeyboardFlags.Up);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressOnly, $"got {decision.Action}");
}

static void PolicyMapsUppercasePacketQwertopToShiftedQwertyScancodes()
{
    var classifier = new SteamInputClassifier();
    var expected = new (char Key, ushort ScanCode)[]
    {
        ('Q', 0x10),
        ('W', 0x11),
        ('E', 0x12),
        ('R', 0x13),
        ('T', 0x14),
        ('O', 0x18),
        ('P', 0x19)
    };

    foreach (var item in expected)
    {
        var evt = Packet(item.Key, flags: LowLevelKeyboardFlags.Injected);
        var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
        Assert(decision.Action == BridgeAction.SuppressAndReinject, $"got {decision.Action}");
        Assert(decision.MockShift, $"packet {item.Key} must mock Shift");
        Assert(decision.ReinjectVirtualKey == item.Key, $"packet {item.Key} must reinject VK {item.Key}");
        Assert(decision.ReinjectScanCode == item.ScanCode, $"packet {item.Key} must reinject scan 0x{item.ScanCode:X2}");
        Assert(decision.ReinjectExtended == false, $"packet {item.Key} must not be extended");
    }
}

static void PolicyMapsLowercasePacketLetterToUnshiftedQwertyScancode()
{
    var classifier = new SteamInputClassifier();
    var evt = Packet('q', flags: LowLevelKeyboardFlags.Injected);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.SuppressAndReinject, $"got {decision.Action}");
    Assert(!decision.MockShift, "lowercase packet letter must not mock Shift");
    Assert(decision.ReinjectVirtualKey == 'Q', "lowercase packet q must normalize to VK Q");
    Assert(decision.ReinjectScanCode == 0x10, "lowercase packet q must reinject Q scancode");
}

static void PolicyLoopGuardsOwnHangulReinjection()
{
    var classifier = new SteamInputClassifier();
    var evt = VirtualKey((ushort)VirtualKeys.VK_HANGUL, scanCode: 0, extraInfo: InjectionMarker.Value);
    var decision = new SuppressionPolicy().Decide(evt, NoModifiers(), enabled: true, classifier.Score(evt));
    Assert(decision.Action == BridgeAction.LoopGuarded, $"got {decision.Action}");
}

static void PolicyTogglesOnHotkey()
{
    var evt = Key('H', 0x23, flags: LowLevelKeyboardFlags.None);
    var decision = new SuppressionPolicy().Decide(evt, new ModifierKeyState(Control: true, Alt: true, Windows: false, Shift: false), enabled: true, new CandidateScore(0, [], 6));
    Assert(decision.Action == BridgeAction.Toggle, $"got {decision.Action}");
}

static void DiagnosticsForwardsRedactedRecordsWhenSensitiveLoggingDisabled()
{
    var sink = new CapturingDiagnosticSink(sensitiveInputEnabled: false);
    var composite = new CompositeDiagnosticSink(sink);
    var record = InputDiagnosticRecord.FromDecision(
        Key('G', 0x22),
        new CandidateScore(6, ["injected", "lower_il_injected", "supported_key"], 6),
        new BridgeDecision(BridgeAction.SuppressAndReinject, "candidate_key_down"));

    Assert(composite.TryWrite(record), "disabled sensitive logging must still forward redacted diagnostics");
    Assert(sink.Count == 1, $"expected one redacted record, got {sink.Count}");
    Assert(sink.LastRecord?.Vk is null && sink.LastRecord?.ScanCode is null, "redacted record must not include key data");
    Assert(sink.LastRecord?.Action == BridgeAction.SuppressAndReinject.ToString(), "redacted record must preserve diagnostic action");
    Assert(record.Vk is null && record.ScanCode is null, "source sensitive record must be scrubbed after write");
}

static void DiagnosticsWritesSensitiveRecordsWhenEnabled()
{
    var sink = new CapturingDiagnosticSink(sensitiveInputEnabled: true);
    var composite = new CompositeDiagnosticSink(sink);
    var record = InputDiagnosticRecord.FromDecision(
        Key('G', 0x22),
        new CandidateScore(6, ["injected", "lower_il_injected", "supported_key"], 6),
        new BridgeDecision(BridgeAction.SuppressAndReinject, "candidate_key_down"));

    Assert(composite.TryWrite(record), "enabled diagnostics must accept sensitive records");
    Assert(sink.Count == 1, $"expected one record, got {sink.Count}");
    Assert(sink.LastRecord?.Vk == 'G', "accepted record must preserve key data for the enabled sink");
    Assert(record.Vk is null && record.ScanCode is null, "source sensitive record must be scrubbed after write");
}

static void DiagnosticsEventSinkRetainsRecentRecords()
{
    var eventSink = new EventDiagnosticSink(() => true);
    var composite = new CompositeDiagnosticSink(eventSink);
    var record = InputDiagnosticRecord.FromDecision(
        Packet('Q', flags: LowLevelKeyboardFlags.Injected),
        new CandidateScore(6, ["injected", "packet_ascii_key", "supported_key"], 6),
        new BridgeDecision(BridgeAction.SuppressAndReinject, "candidate_key_down"));

    Assert(composite.TryWrite(record), "event sink must accept diagnostic records");
    var snapshot = eventSink.Snapshot();
    Assert(snapshot.Count == 1, $"expected one retained record, got {snapshot.Count}");
    Assert(snapshot[0].Vk == VirtualKeys.VK_PACKET, "enabled event sink must retain key data");

    eventSink.ClearSensitiveFields();
    snapshot = eventSink.Snapshot();
    Assert(snapshot[0].Vk is null && snapshot[0].Action == BridgeAction.SuppressAndReinject.ToString(), "clearing history must preserve action but remove key data");
}

static void CountersDoNotRetainLastKey()
{
    var counters = new BridgeCounters();
    counters.RecordHookEvent(Key('G', 0x22));
    var snapshot = counters.Snapshot();

    Assert(snapshot.HookEvents == 1, $"expected one hook event, got {snapshot.HookEvents}");
    Assert(snapshot.LastKeyText == "-", "counters must not retain the last key");
}

static void SendInputResultAcceptsExpectedShiftMockCount()
{
    var result = new SendInputResult((ushort)'G', 0x22, ReturnCount: 4, LastError: 0, ErrorMessage: null, ExpectedCount: 4);
    Assert(result.Success, "Shift mock sends four inputs and must be counted as success");
}

static void AppStateToggles()
{
    var state = new AppState();
    Assert(state.Enabled, "default enabled");
    Assert(!state.ToggleEnabled(), "first toggle disables");
    Assert(state.ToggleEnabled(), "second toggle enables");
}

static LowLevelKeyboardEvent Key(
    char vk,
    ushort scanCode,
    int message = WindowMessages.WM_KEYDOWN,
    LowLevelKeyboardFlags flags = LowLevelKeyboardFlags.Injected | LowLevelKeyboardFlags.LowerIntegrityInjected,
    UIntPtr extraInfo = default) =>
    VirtualKey((ushort)vk, scanCode, message, flags, extraInfo);

static LowLevelKeyboardEvent Packet(
    char codePoint,
    int message = WindowMessages.WM_KEYDOWN,
    LowLevelKeyboardFlags flags = LowLevelKeyboardFlags.Injected | LowLevelKeyboardFlags.LowerIntegrityInjected,
    UIntPtr extraInfo = default) =>
    VirtualKey((ushort)VirtualKeys.VK_PACKET, (ushort)codePoint, message, flags, extraInfo);

static LowLevelKeyboardEvent VirtualKey(
    ushort vk,
    ushort scanCode,
    int message = WindowMessages.WM_KEYDOWN,
    LowLevelKeyboardFlags flags = LowLevelKeyboardFlags.Injected | LowLevelKeyboardFlags.LowerIntegrityInjected,
    UIntPtr extraInfo = default) =>
    new(DateTimeOffset.Now, message, vk, scanCode, flags, extraInfo);

static ModifierKeyState NoModifiers() => new(Control: false, Alt: false, Windows: false, Shift: false);
static ModifierKeyState ShiftOnly() => new(Control: false, Alt: false, Windows: false, Shift: true);
static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class CapturingDiagnosticSink(bool sensitiveInputEnabled) : IInputDiagnosticSink
{
    public bool IsSensitiveInputEnabled { get; } = sensitiveInputEnabled;
    public int Count { get; private set; }
    public InputDiagnosticRecord? LastRecord { get; private set; }

    public bool TryWrite(InputDiagnosticRecord record)
    {
        Count++;
        LastRecord = record;
        return true;
    }
}
