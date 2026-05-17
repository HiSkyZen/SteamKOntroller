using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamKOntroller.App.Services;
using SteamKOntroller.Core.Diagnostics;

namespace SteamKOntroller.App;

public sealed partial class MainPage : Page
{
    private readonly BridgeRuntime _runtime;
    private readonly DispatcherTimer _timer = new();
    private bool _updatingToggle;

    public MainPage()
    {
        _runtime = ((App)Application.Current).Runtime;
        Records = new ObservableCollection<DiagnosticRecordView>();
        InitializeComponent();

        EventList.ItemsSource = Records;
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => RefreshStatus();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ObservableCollection<DiagnosticRecordView> Records { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _runtime.RecordWritten += OnRecordWritten;
        _runtime.State.EnabledChanged += OnEnabledChanged;
        _timer.Start();
        RefreshStatus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _runtime.RecordWritten -= OnRecordWritten;
        _runtime.State.EnabledChanged -= OnEnabledChanged;
    }

    private void OnEnabledChanged(object? sender, bool enabled)
    {
        DispatcherQueue.TryEnqueue(RefreshStatus);
    }

    private void OnRecordWritten(object? sender, InputDiagnosticRecord record)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Records.Add(new DiagnosticRecordView(record));
            while (Records.Count > 300)
            {
                Records.RemoveAt(0);
            }

            if (EventList.Items.Count > 0)
            {
                EventList.ScrollIntoView(EventList.Items[^1]);
            }
        });
    }

    private void RefreshStatus()
    {
        var snapshot = _runtime.Counters.Snapshot();
        var enabled = _runtime.Bridge.Enabled;
        _updatingToggle = true;
        EnabledToggle.IsChecked = enabled;
        EnabledToggle.Label = enabled ? "ON" : "OFF";
        _updatingToggle = false;

        StatusInfo.Severity = _runtime.LastStartupError is null
            ? (snapshot.HookInstalled ? InfoBarSeverity.Success : InfoBarSeverity.Warning)
            : InfoBarSeverity.Error;
        StatusInfo.Title = _runtime.LastStartupError is null
            ? (snapshot.HookInstalled ? "Hook installed" : "Hook stopped")
            : "Hook error";
        StatusInfo.Message = _runtime.LastStartupError ?? _runtime.LogPath;

        EnabledText.Text = enabled ? "ON" : "OFF";
        HookText.Text = snapshot.HookInstalled ? "Installed" : "Stopped";
        HookEventsText.Text = snapshot.HookEvents.ToString("N0");
        CandidatesText.Text = snapshot.SteamCandidates.ToString("N0");
        SuppressedText.Text = snapshot.SuppressedEvents.ToString("N0");
        ReinjectedText.Text = snapshot.ReinjectedEvents.ToString("N0");
        LoopGuardText.Text = snapshot.LoopGuardedEvents.ToString("N0");
        FailuresText.Text = snapshot.SendInputFailures.ToString("N0");
        LastKeyText.Text = snapshot.LastKeyText;
        LastEventText.Text = snapshot.LastEventAt?.ToString("HH:mm:ss.fff") ?? "-";
    }

    private void EnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_updatingToggle)
        {
            return;
        }

        _runtime.Bridge.Enabled = EnabledToggle.IsChecked == true;
        RefreshStatus();
    }

    private void ResetCounters_Click(object sender, RoutedEventArgs e)
    {
        _runtime.Bridge.ResetCounters();
        Records.Clear();
        RefreshStatus();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        _runtime.OpenLogFolder();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ExitApplication();
    }
}
