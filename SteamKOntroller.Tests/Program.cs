using SteamKOntroller.Core;
using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Native;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

var tests = new (string Name, Action Body)[]
{
    ("classifier accepts lower-integrity injected supported keys", ClassifierAcceptsSteamCandidate),
    ("classifier ignores own reinjected marker", ClassifierIgnoresOwnMarker),
    ("policy suppresses candidate key down and reinjects", PolicySuppressesCandidateKeyDown),
    ("policy suppresses candidate key up without reinjecting", PolicySuppressesCandidateKeyUp),
    ("policy bypasses disabled bridge", PolicyBypassesDisabledBridge),
    ("policy bypasses shortcut modifiers", PolicyBypassesShortcutModifiers),
    ("policy toggles on Ctrl+Alt+H", PolicyTogglesOnHotkey),
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

static void PolicyTogglesOnHotkey()
{
    var evt = Key('H', 0x23, flags: LowLevelKeyboardFlags.None);
    var decision = new SuppressionPolicy().Decide(evt, new ModifierKeyState(Control: true, Alt: true, Windows: false, Shift: false), enabled: true, new CandidateScore(0, [], 6));
    Assert(decision.Action == BridgeAction.Toggle, $"got {decision.Action}");
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
    new(DateTimeOffset.Now, message, (ushort)vk, scanCode, flags, extraInfo);

static ModifierKeyState NoModifiers() => new(Control: false, Alt: false, Windows: false, Shift: false);

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
