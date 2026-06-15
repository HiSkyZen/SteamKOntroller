using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SteamKOntroller.App.Services;
using SteamKOntroller.Core.Diagnostics;

namespace SteamKOntroller.App;

public sealed partial class MainPage : Page
{
    private readonly BridgeRuntime _runtime;
    private readonly StartupRegistrationService _startup = new();
    private readonly DispatcherTimer _timer = new();
    private int _recentActivityColumns;
    private int _settingsCardColumns;
    private int _statusCardColumns;
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
        LoadRecentRecords();
        _timer.Start();
        ApplyResponsiveLayout(ActualWidth);
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
        var recordView = new DiagnosticRecordView(record);
        DispatcherQueue.TryEnqueue(() =>
        {
            AddRecordView(recordView);
            ScrollToLastRecord();
        });
    }

    private void LoadRecentRecords()
    {
        if (Records.Count > 0)
        {
            return;
        }

        foreach (var record in _runtime.RecentRecords)
        {
            AddRecordView(new DiagnosticRecordView(record));
        }

        ScrollToLastRecord();
    }

    private void AddRecordView(DiagnosticRecordView recordView)
    {
        Records.Add(recordView);
        while (Records.Count > 300)
        {
            Records[0].ClearSensitiveFields();
            Records.RemoveAt(0);
        }
    }

    private void ScrollToLastRecord()
    {
        if (EventList.Items.Count > 0)
        {
            EventList.ScrollIntoView(EventList.Items[^1]);
        }
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

        BridgeActionButton.Label = enabled ? "브리지 끄기" : "브리지 켜기";
        BridgeActionIcon.Symbol = enabled ? Symbol.Cancel : Symbol.Play;
        AutomationProperties.SetName(BridgeActionButton, BridgeActionButton.Label);

        StatusInfo.Severity = _runtime.LastStartupError is null
            ? (snapshot.HookInstalled ? InfoBarSeverity.Success : InfoBarSeverity.Warning)
            : InfoBarSeverity.Error;
        StatusInfo.Title = _runtime.LastStartupError is null
            ? (snapshot.HookInstalled ? "브리지가 실행 중입니다" : "브리지가 중지되었습니다")
            : "브리지 오류";
        StatusInfo.Message = _runtime.LastStartupError ?? BuildStatusMessage(diagnosticsEnabled);

        DiagnosticsInfo.Severity = diagnosticsEnabled ? InfoBarSeverity.Informational : InfoBarSeverity.Warning;
        DiagnosticsInfo.Title = diagnosticsEnabled ? "진단 로그가 켜져 있습니다" : "진단 로그가 꺼져 있습니다";
        DiagnosticsInfo.Message = diagnosticsEnabled
            ? $"상세 입력 이벤트를 진단 탭과 디스크에 기록합니다. 현재 폴더: {_runtime.LogDirectory}"
            : "디스크 로그는 꺼져 있습니다. 진단 탭에는 키값을 제거한 이벤트만 표시합니다.";

        EnabledText.Text = enabled ? "켬" : "끔";
        HookText.Text = snapshot.HookInstalled ? "설치됨" : "중지됨";
        HookEventsText.Text = snapshot.HookEvents.ToString("N0");
        CandidatesText.Text = snapshot.SteamCandidates.ToString("N0");
        SuppressedText.Text = snapshot.SuppressedEvents.ToString("N0");
        ReinjectedText.Text = snapshot.ReinjectedEvents.ToString("N0");
        LoopGuardText.Text = snapshot.LoopGuardedEvents.ToString("N0");
        FailuresText.Text = snapshot.SendInputFailures.ToString("N0");
        LastKeyText.Text = diagnosticsEnabled ? snapshot.LastKeyText : "숨김";
        LastEventText.Text = snapshot.LastEventAt?.ToString("HH:mm:ss.fff") ?? "-";
    }

    private string BuildStatusMessage(bool diagnosticsEnabled)
    {
        if (!diagnosticsEnabled)
        {
            return "진단 로그가 꺼져 있습니다.";
        }

        return _runtime.CurrentLogPath is null
            ? $"진단 로그는 {_runtime.LogDirectory}에 저장됩니다."
            : $"진단 로그 기록 중: {_runtime.CurrentLogPath}";
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            ShowView(tag);
        }
    }

    private void ShowView(string tag)
    {
        SettingsView.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        StatusView.Visibility = tag == "status" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsView.Visibility = tag == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        PageTitleText.Text = tag switch
        {
            "status" => "상태",
            "diagnostics" => "진단",
            _ => "설정"
        };
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var contentWidth = ContentRoot.ActualWidth > 0 ? ContentRoot.ActualWidth : width;
        var isCompact = contentWidth < 720;

        ContentRoot.Padding = isCompact
            ? new Thickness(18, 16, 18, 20)
            : new Thickness(32, 24, 32, 32);
        PageCommandBar.DefaultLabelPosition = isCompact
            ? CommandBarDefaultLabelPosition.Collapsed
            : CommandBarDefaultLabelPosition.Right;

        var availableWidth = Math.Max(0, contentWidth - ContentRoot.Padding.Left - ContentRoot.Padding.Right - 8);
        SizePanel(SettingsPanel, availableWidth, 1180);
        SizePanel(StatusPanel, availableWidth, 1180);

        ArrangeSettingsCards(contentWidth < 980 ? 1 : 2);
        ArrangeStatusCards(contentWidth < 980 ? 1 : contentWidth < 1280 ? 2 : 4);
        ArrangeRecentActivity(contentWidth < 900 ? 1 : 2);
    }

    private static void SizePanel(FrameworkElement panel, double availableWidth, double maxWidth)
    {
        panel.Width = availableWidth > 0 ? Math.Min(availableWidth, maxWidth) : double.NaN;
    }

    private void ArrangeSettingsCards(int columns)
    {
        if (_settingsCardColumns == columns)
        {
            return;
        }

        _settingsCardColumns = columns;
        SettingsColumn1.Width = columns == 1 ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumnSpan(StatusInfo, columns);

        var cards = new FrameworkElement[]
        {
            BridgeSettingCard,
            StartupSettingCard,
            DiagnosticSettingCard,
            RetentionSettingCard
        };
        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index % columns);
            Grid.SetRow(cards[index], (index / columns) + 1);
        }
    }

    private void ArrangeStatusCards(int columns)
    {
        if (_statusCardColumns == columns)
        {
            return;
        }

        _statusCardColumns = columns;
        var columnDefinitions = new[]
        {
            StatusColumn0,
            StatusColumn1,
            StatusColumn2,
            StatusColumn3
        };
        for (var index = 0; index < columnDefinitions.Length; index++)
        {
            columnDefinitions[index].Width = index < columns
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        }

        StatusCardsGrid.ColumnSpacing = columns == 1 ? 0 : 12;
        var cards = new FrameworkElement[]
        {
            EnabledCard,
            HookCard,
            HookEventsCard,
            CandidatesCard,
            SuppressedCard,
            ReinjectedCard,
            LoopGuardCard,
            FailuresCard
        };
        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index % columns);
            Grid.SetRow(cards[index], index / columns);
        }
    }

    private void ArrangeRecentActivity(int columns)
    {
        if (_recentActivityColumns == columns)
        {
            return;
        }

        _recentActivityColumns = columns;
        RecentColumn1.Width = columns == 1 ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumnSpan(RecentActivityTitle, columns);
        Grid.SetColumn(LastEventPanel, columns == 1 ? 0 : 1);
        Grid.SetRow(LastEventPanel, columns == 1 ? 2 : 1);
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
            StatusInfo.Title = "시작 등록 실패";
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
            ClearRecordViews();
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
        ClearRecordViews();
        RefreshStatus();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        _runtime.OpenLogFolder();
    }

    private void ClearEvents_Click(object sender, RoutedEventArgs e)
    {
        ClearRecordViews();
    }

    private void ClearRecordViews()
    {
        foreach (var record in Records)
        {
            record.ClearSensitiveFields();
        }

        Records.Clear();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ExitApplication();
    }
}
