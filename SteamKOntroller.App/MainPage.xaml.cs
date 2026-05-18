using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamKOntroller.App.Services;
using SteamKOntroller.Core.Diagnostics;

namespace SteamKOntroller.App;

public sealed partial class MainPage : Page
{
    private readonly BridgeRuntime _runtime;
    private readonly StartupRegistrationService _startup = new();
    private readonly DispatcherTimer _timer = new();
    private bool _updatingControls;

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
        RootNavigation.SelectedItem ??= SettingsNavItem;
        ShowView("settings");

        _runtime.RecordWritten += OnRecordWritten;
        _runtime.State.EnabledChanged += OnEnabledChanged;
        _runtime.SettingsChanged += OnSettingsChanged;
        _timer.Start();
        RefreshStatus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _runtime.RecordWritten -= OnRecordWritten;
        _runtime.State.EnabledChanged -= OnEnabledChanged;
        _runtime.SettingsChanged -= OnSettingsChanged;
    }

    private void OnEnabledChanged(object? sender, bool enabled)
    {
        DispatcherQueue.TryEnqueue(RefreshStatus);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshStatus);
    }

    private void OnRecordWritten(object? sender, InputDiagnosticRecord record)
    {
        if (!_runtime.Settings.DiagnosticLoggingEnabled)
        {
            return;
        }

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
        var diagnosticsEnabled = _runtime.Settings.DiagnosticLoggingEnabled;

        _updatingControls = true;
        BridgeSwitch.IsOn = enabled;
        DiagnosticLoggingSwitch.IsOn = diagnosticsEnabled;
        RetentionDaysBox.Value = _runtime.Settings.LogRetentionDays;
        try
        {
            StartupSwitch.IsOn = _startup.IsEnabled;
        }
        catch (InvalidOperationException)
        {
            StartupSwitch.IsOn = false;
        }

        _updatingControls = false;

        BridgeActionButton.Label = enabled ? "Turn off" : "Turn on";
        BridgeActionIcon.Symbol = enabled ? Symbol.Cancel : Symbol.Play;

        StatusInfo.Severity = _runtime.LastStartupError is null
            ? (snapshot.HookInstalled ? InfoBarSeverity.Success : InfoBarSeverity.Warning)
            : InfoBarSeverity.Error;
        StatusInfo.Title = _runtime.LastStartupError is null
            ? (snapshot.HookInstalled ? "Bridge is running" : "Bridge is stopped")
            : "Bridge error";
        StatusInfo.Message = _runtime.LastStartupError ?? BuildStatusMessage(diagnosticsEnabled);

        DiagnosticsInfo.Severity = diagnosticsEnabled ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        DiagnosticsInfo.Title = diagnosticsEnabled ? "Diagnostic logging is on" : "Diagnostic logging is off";
        DiagnosticsInfo.Message = diagnosticsEnabled
            ? $"Current folder: {_runtime.LogDirectory}"
            : "Detailed input events are not written to disk until logging is enabled.";

        EnabledText.Text = enabled ? "On" : "Off";
        HookText.Text = snapshot.HookInstalled ? "Installed" : "Stopped";
        HookEventsText.Text = snapshot.HookEvents.ToString("N0");
        CandidatesText.Text = snapshot.SteamCandidates.ToString("N0");
        SuppressedText.Text = snapshot.SuppressedEvents.ToString("N0");
        ReinjectedText.Text = snapshot.ReinjectedEvents.ToString("N0");
        LoopGuardText.Text = snapshot.LoopGuardedEvents.ToString("N0");
        FailuresText.Text = snapshot.SendInputFailures.ToString("N0");
        LastKeyText.Text = diagnosticsEnabled ? snapshot.LastKeyText : "Hidden";
        LastEventText.Text = snapshot.LastEventAt?.ToString("HH:mm:ss.fff") ?? "-";
    }

    private string BuildStatusMessage(bool diagnosticsEnabled)
    {
        if (!diagnosticsEnabled)
        {
            return "Persistent diagnostic logging is off.";
        }

        return _runtime.CurrentLogPath is null
            ? $"Diagnostic logging will start in {_runtime.LogDirectory}"
            : $"Writing diagnostics to {_runtime.CurrentLogPath}";
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        ShowView(tag);
    }

    private void ShowView(string tag)
    {
        SettingsView.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        StatusView.Visibility = tag == "status" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsView.Visibility = tag == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        RootNavigation.Header = tag switch
        {
            "status" => "Status",
            "diagnostics" => "Diagnostics",
            _ => "Settings"
        };
    }

    private void BridgeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        _runtime.Bridge.Enabled = BridgeSwitch.IsOn;
        RefreshStatus();
    }

    private void BridgeActionButton_Click(object sender, RoutedEventArgs e)
    {
        _runtime.Bridge.Enabled = !_runtime.Bridge.Enabled;
        RefreshStatus();
    }

    private void StartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        try
        {
            _startup.SetEnabled(StartupSwitch.IsOn);
            RefreshStatus();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            RefreshStatus();
            StatusInfo.Severity = InfoBarSeverity.Error;
            StatusInfo.Title = "Startup registration failed";
            StatusInfo.Message = ex.Message;
        }
    }

    private void DiagnosticLoggingSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        _runtime.SetDiagnosticLoggingEnabled(DiagnosticLoggingSwitch.IsOn);
        if (!DiagnosticLoggingSwitch.IsOn)
        {
            Records.Clear();
        }

        RefreshStatus();
    }

    private void RetentionDaysBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_updatingControls || double.IsNaN(args.NewValue))
        {
            return;
        }

        _runtime.SetLogRetentionDays((int)Math.Round(args.NewValue));
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

    private void ClearEvents_Click(object sender, RoutedEventArgs e)
    {
        Records.Clear();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ExitApplication();
    }
}
