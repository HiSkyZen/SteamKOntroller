using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Diagnostics;
using SteamKOntroller.Core.Native;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.Core;

public sealed class SteamInputBridge : IDisposable
{
    private readonly AppState _state;
    private readonly LowLevelKeyboardHook _hook;
    private readonly SteamInputClassifier _classifier;
    private readonly SuppressionPolicy _policy;
    private readonly ScancodeReinjector _reinjector;
    private readonly IInputDiagnosticSink _diagnostics;
    private readonly BridgeCounters _counters;
    private bool _disposed;

    public SteamInputBridge(
        AppState state,
        SteamInputClassifier classifier,
        SuppressionPolicy policy,
        ScancodeReinjector reinjector,
        IInputDiagnosticSink diagnostics,
        BridgeCounters counters)
    {
        _state = state;
        _classifier = classifier;
        _policy = policy;
        _reinjector = reinjector;
        _diagnostics = diagnostics;
        _counters = counters;
        _hook = new LowLevelKeyboardHook(ProcessKeyboardEvent);
        _reinjector.InjectionCompleted += OnInjectionCompleted;
    }

    public bool Enabled
    {
        get => _state.Enabled;
        set => _state.SetEnabled(value);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reinjector.Start();
        _hook.Start();
        _counters.SetHookInstalled(true);
        _diagnostics.TryWrite(InputDiagnosticRecord.Status("hook", "installed"));
    }

    public void Stop()
    {
        _hook.Stop();
        _counters.SetHookInstalled(false);
        _diagnostics.TryWrite(InputDiagnosticRecord.Status("hook", "stopped"));
    }

    public bool ToggleEnabled() => _state.ToggleEnabled();

    public void ResetCounters() => _counters.Reset(_hook.IsRunning);

    private bool ProcessKeyboardEvent(LowLevelKeyboardEvent keyboardEvent)
    {
        _counters.RecordHookEvent(keyboardEvent);

        var modifiers = KeyboardStateReader.ReadModifiers();
        var score = _classifier.Score(keyboardEvent);
        var decision = _policy.Decide(keyboardEvent, modifiers, _state.Enabled, score);

        if (score.IsCandidate)
        {
            _counters.IncrementSteamCandidates();
        }

        switch (decision.Action)
        {
            case BridgeAction.LoopGuarded:
                _counters.IncrementLoopGuarded();
                _diagnostics.TryWrite(InputDiagnosticRecord.FromDecision(keyboardEvent, score, decision));
                return false;

            case BridgeAction.Toggle:
                var enabled = _state.ToggleEnabled();
                _diagnostics.TryWrite(InputDiagnosticRecord.FromDecision(
                    keyboardEvent,
                    score,
                    decision with { Reason = enabled ? "enabled" : "disabled" }));
                return false;

            case BridgeAction.SuppressAndReinject:
                _counters.IncrementSuppressed();
                _diagnostics.TryWrite(InputDiagnosticRecord.FromDecision(keyboardEvent, score, decision));
                if (!_reinjector.TryEnqueueTap(
                    keyboardEvent.VirtualKey,
                    keyboardEvent.ScanCode,
                    keyboardEvent.IsExtended,
                    decision.MockShift))
                {
                    _counters.IncrementSendInputFailures();
                    _diagnostics.TryWrite(InputDiagnosticRecord.Error(
                        "reinject",
                        "reinject queue rejected request",
                        keyboardEvent));
                }

                return true;

            case BridgeAction.SuppressAndSendHangulToggle:
                _counters.IncrementSuppressed();
                _diagnostics.TryWrite(InputDiagnosticRecord.FromDecision(keyboardEvent, score, decision));
                if (!_reinjector.TryEnqueueVirtualKeyTap(VirtualKeys.VK_HANGUL))
                {
                    _counters.IncrementSendInputFailures();
                    _diagnostics.TryWrite(InputDiagnosticRecord.Error(
                        "reinject",
                        "hangul toggle queue rejected request",
                        keyboardEvent));
                }

                return true;

            case BridgeAction.SuppressOnly:
                _counters.IncrementSuppressed();
                _diagnostics.TryWrite(InputDiagnosticRecord.FromDecision(keyboardEvent, score, decision));
                return true;

            default:
                if (score.IsCandidate)
                {
                    _diagnostics.TryWrite(InputDiagnosticRecord.FromDecision(keyboardEvent, score, decision));
                }

                return false;
        }
    }

    private void OnInjectionCompleted(object? sender, SendInputResult result)
    {
        if (result.Success)
        {
            _counters.IncrementReinjected();
            _diagnostics.TryWrite(InputDiagnosticRecord.Reinjected(result));
            return;
        }

        _counters.IncrementSendInputFailures();
        _diagnostics.TryWrite(InputDiagnosticRecord.SendInputFailure(result));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reinjector.InjectionCompleted -= OnInjectionCompleted;
        _hook.Dispose();
        _reinjector.Dispose();
        _counters.SetHookInstalled(false);
    }
}
