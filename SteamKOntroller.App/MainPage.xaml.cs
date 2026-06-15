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
#if !DEBUG
        SensitiveLoggingSettingCard.Visibility = Visibility.Collapsed;
#endif

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
        var sensitiveInputLoggingEnabled = diagnosticsEnabled && _runtime.Settings.SensitiveInputLoggingEnabled;

        _updatingControls = true;
        BridgeSwitch.IsOn = enabled;
        DiagnosticLoggingSwitch.IsOn = diagnosticsEnabled;
        SensitiveLoggingSwitch.IsOn = sensitiveInputLoggingEnabled;
        SensitiveLoggingSwitch.IsEnabled = diagnosticsEnabled;
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
        StatusInfo.Message = _runtime.LastStartupError ?? BuildStatusMessage(diagnosticsEnabled, sensitiveInputLoggingEnabled);

        DiagnosticsInfo.Severity = diagnosticsEnabled
            ? (sensitiveInputLoggingEnabled ? InfoBarSeverity.Error : InfoBarSeverity.Informational)
            : InfoBarSeverity.Warning;
        DiagnosticsInfo.Title = diagnosticsEnabled
            ? (sensitiveInputLoggingEnabled ? "민감 입력 로그가 켜져 있습니다" : "마스킹된 진단 로그가 켜져 있습니다")
            : "진단 로그가 꺼져 있습니다";
        DiagnosticsInfo.Message = diagnosticsEnabled
            ? BuildDiagnosticsMessage(sensitiveInputLoggingEnabled)
            : "진단 탭과 디스크에 입력 이벤트를 기록하지 않습니다.";

        EnabledText.Text = enabled ? "켬" : "끔";
        HookText.Text = snapshot.HookInstalled ? "설치됨" : "중지됨";
        HookEventsText.Text = snapshot.HookEvents.ToString("N0");
        CandidatesText.Text = snapshot.SteamCandidates.ToString("N0");
        SuppressedText.Text = snapshot.SuppressedEvents.ToString("N0");
        ReinjectedText.Text = snapshot.ReinjectedEvents.ToString("N0");
        LoopGuardText.Text = snapshot.LoopGuardedEvents.ToString("N0");
        FailuresText.Text = snapshot.SendInputFailures.ToString("N0");
        LastKeyText.Text = sensitiveInputLoggingEnabled ? snapshot.LastKeyText : "숨김";
        LastEventText.Text = snapshot.LastEventAt?.ToString("HH:mm:ss.fff") ?? "-";
    }

    private string BuildStatusMessage(bool diagnosticsEnabled, bool sensitiveInputLoggingEnabled)
    {
        if (!diagnosticsEnabled)
        {
            return "진단 로그가 꺼져 있습니다. 진단 탭과 디스크에 입력 이벤트를 기록하지 않습니다.";
        }

        var mode = sensitiveInputLoggingEnabled ? "민감 입력 진단 로그" : "마스킹된 진단 로그";
        return _runtime.CurrentLogPath is null
            ? $"{mode}는 {_runtime.LogDirectory}에 저장됩니다."
            : $"{mode} 기록 중: {_runtime.CurrentLogPath}";
    }

    private string BuildDiagnosticsMessage(bool sensitiveInputLoggingEnabled)
    {
        var mode = sensitiveInputLoggingEnabled
            ? "키 값과 스캔 코드를 포함한 위험한 디버그 로그"
            : "키 값을 제거한 마스킹 로그";
        return $"{mode}를 진단 탭과 디스크에 기록합니다. 현재 폴더: {_runtime.LogDirectory}";
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
            RetentionSettingCard,
#if DEBUG
            SensitiveLoggingSettingCard
#endif
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

    private async void SensitiveLoggingSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        var requestedEnabled = SensitiveLoggingSwitch.IsOn;
#if DEBUG
        if (requestedEnabled && !await ConfirmSensitiveInputLoggingAsync())
        {
            _updatingControls = true;
            SensitiveLoggingSwitch.IsOn = false;
            _updatingControls = false;
            return;
        }

        _runtime.SetSensitiveInputLoggingEnabled(requestedEnabled);
#else
        _runtime.SetSensitiveInputLoggingEnabled(false);
#endif
        if (!requestedEnabled)
        {
            ClearRecordViews();
        }

        RefreshStatus();
    }

    private async Task<bool> ConfirmSensitiveInputLoggingAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "위험: 키 입력 원문을 기록합니다",
            Content = "이 옵션은 누른 키와 스캔 코드를 그대로 저장합니다. 암호, 인증 코드, 개인 메시지, 업무 자료가 로그 파일과 진단 탭에 남을 수 있습니다. 필요한 디버깅이 끝나면 즉시 끄고 생성된 로그를 삭제하십시오.",
            PrimaryButtonText = "위험을 이해하고 켜기",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
