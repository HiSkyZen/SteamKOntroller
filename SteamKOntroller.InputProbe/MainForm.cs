using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using SteamKOntroller.InputProbe.Logging;
using SteamKOntroller.InputProbe.Native;

namespace SteamKOntroller.InputProbe;

public sealed class MainForm : Form
{
    private readonly BindingList<InputEventRecord> _records = new();
    private readonly JsonlLogger _logger;
    private readonly Win32KeyboardHook _keyboardHook = new();
    private readonly ProbeTextBox _inputBox = new();
    private readonly DataGridView _grid = new();
    private readonly Label _statusLabel = new();
    private readonly CheckBox _autoScrollCheck = new();
    private readonly CheckBox _logWindowMessagesCheck = new();
    private readonly CheckBox _logHookCheck = new();
    private readonly CheckBox _logRawCheck = new();
    private bool _rawRegistered;

    public MainForm()
    {
        Text = "SteamKOntroller InputProbe";
        Width = 1280;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamKOntroller",
            "InputProbe",
            "logs");

        var logPath = Path.Combine(logDir, $"input-probe-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
        _logger = new JsonlLogger(logPath);

        BuildUi(logPath);

        _inputBox.ProbeWindowMessage += (_, record) => LogRecord(record);

        _keyboardHook.KeyboardEvent += (_, record) =>
        {
            if (!_logHookCheck.Checked)
            {
                return;
            }

            record = CopyWithTextSnapshot(record, _inputBox.Text);
            LogRecordThreadSafe(record);
        };

        Load += (_, _) =>
        {
            TryStartHook();
            _inputBox.Focus();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryRegisterRawInput();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WindowMessageNames.WM_INPUT && _logRawCheck.Checked)
        {
            var record = RawInput.TryReadKeyboardInput(m.LParam, _inputBox.Text, out var error);
            if (record is not null)
            {
                LogRecord(record);
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                LogRecord(new InputEventRecord
                {
                    Timestamp = DateTimeOffset.Now,
                    Source = "raw",
                    Message = "WM_INPUT",
                    Note = error,
                    TextSnapshot = _inputBox.Text
                });
            }
        }

        base.WndProc(ref m);
    }

    private void BuildUi(string logPath)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        Controls.Add(root);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        root.Controls.Add(toolbar, 0, 0);

        var clearButton = new Button { Text = "Clear", Width = 80, Height = 28 };
        clearButton.Click += (_, _) => _records.Clear();
        toolbar.Controls.Add(clearButton);

        var openLogButton = new Button { Text = "Open log folder", Width = 120, Height = 28 };
        openLogButton.Click += (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetDirectoryName(logPath)!,
            UseShellExecute = true
        });
        toolbar.Controls.Add(openLogButton);

        var copyButton = new Button { Text = "Copy visible rows", Width = 130, Height = 28 };
        copyButton.Click += (_, _) => CopyVisibleRowsToClipboard();
        toolbar.Controls.Add(copyButton);

        _logRawCheck.Text = "Raw Input";
        _logRawCheck.Checked = true;
        _logRawCheck.AutoSize = true;
        _logRawCheck.Margin = new Padding(16, 7, 4, 4);
        toolbar.Controls.Add(_logRawCheck);

        _logHookCheck.Text = "Low-level hook";
        _logHookCheck.Checked = true;
        _logHookCheck.AutoSize = true;
        _logHookCheck.Margin = new Padding(8, 7, 4, 4);
        toolbar.Controls.Add(_logHookCheck);

        _logWindowMessagesCheck.Text = "TextBox WndProc";
        _logWindowMessagesCheck.Checked = true;
        _logWindowMessagesCheck.AutoSize = true;
        _logWindowMessagesCheck.Margin = new Padding(8, 7, 4, 4);
        toolbar.Controls.Add(_logWindowMessagesCheck);

        _autoScrollCheck.Text = "Auto-scroll";
        _autoScrollCheck.Checked = true;
        _autoScrollCheck.AutoSize = true;
        _autoScrollCheck.Margin = new Padding(8, 7, 4, 4);
        toolbar.Controls.Add(_autoScrollCheck);

        _inputBox.Dock = DockStyle.Fill;
        _inputBox.Multiline = true;
        _inputBox.ScrollBars = ScrollBars.Vertical;
        _inputBox.AcceptsReturn = true;
        _inputBox.AcceptsTab = true;
        _inputBox.Font = new Font("Consolas", 13f);
        _inputBox.PlaceholderText = "Focus here, then type with physical keyboard / Steam Keyboard / IME...";
        _inputBox.ShouldLog = () => _logWindowMessagesCheck.Checked;
        root.Controls.Add(_inputBox, 0, 1);

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.DataSource = _records;
        AddColumns();
        root.Controls.Add(_grid, 0, 2);

        var legend = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Text = "판정 힌트: Raw+Hook 모두 잡힘 = 키보드 경로 가능성 / Hook만 잡힘 = 주입 입력 가능성 / WM_CHAR만 잡힘 = 문자 주입 가능성 / WM_IME_COMPOSITION 발생 = IME 조합 경로 관여",
        };
        root.Controls.Add(legend, 0, 3);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = $"Log: {logPath}";
        root.Controls.Add(_statusLabel, 0, 4);
    }

    private void AddColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = nameof(InputEventRecord.DisplayTime), Width = 92 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", DataPropertyName = nameof(InputEventRecord.Source), Width = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", DataPropertyName = nameof(InputEventRecord.Message), Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dir", DataPropertyName = nameof(InputEventRecord.Direction), Width = 48 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "VK", DataPropertyName = nameof(InputEventRecord.VirtualKey), Width = 54 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Scan", DataPropertyName = nameof(InputEventRecord.ScanCode), Width = 54 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Char", DataPropertyName = nameof(InputEventRecord.Character), Width = 96 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Code", DataPropertyName = nameof(InputEventRecord.CodePoint), Width = 82 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Flags", DataPropertyName = nameof(InputEventRecord.FlagsHex), Width = 92 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Inj", DataPropertyName = nameof(InputEventRecord.Injected), Width = 42 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Device", DataPropertyName = nameof(InputEventRecord.DeviceName), Width = 260 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Note", DataPropertyName = nameof(InputEventRecord.Note), Width = 180 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Text", DataPropertyName = nameof(InputEventRecord.TextSnapshot), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }

    private void TryRegisterRawInput()
    {
        if (_rawRegistered)
        {
            return;
        }

        try
        {
            RawInput.RegisterKeyboard(Handle);
            _rawRegistered = true;
            _statusLabel.Text = $"Raw Input registered. Log: {_logger.Path}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Raw Input registration failed: {ex.Message}";
            LogRecord(new InputEventRecord
            {
                Timestamp = DateTimeOffset.Now,
                Source = "raw",
                Message = "register",
                Note = ex.Message
            });
        }
    }

    private void TryStartHook()
    {
        try
        {
            _keyboardHook.Start();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Keyboard hook failed: {ex.Message}";
            LogRecord(new InputEventRecord
            {
                Timestamp = DateTimeOffset.Now,
                Source = "hook",
                Message = "start",
                Note = ex.Message
            });
        }
    }

    private void LogRecordThreadSafe(InputEventRecord record)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)(() => LogRecord(record)));
        }
        else
        {
            LogRecord(record);
        }
    }

    private void LogRecord(InputEventRecord record)
    {
        _logger.Write(record);
        _records.Add(record);

        const int maxRows = 5000;
        while (_records.Count > maxRows)
        {
            _records.RemoveAt(0);
        }

        if (_autoScrollCheck.Checked && _grid.Rows.Count > 0)
        {
            var lastIndex = _grid.Rows.Count - 1;
            _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, lastIndex);
            _grid.Rows[lastIndex].Selected = true;
        }
    }

    private void CopyVisibleRowsToClipboard()
    {
        if (_records.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("time\tsource\tmessage\tdir\tvk\tscan\tchar\tcode\tflags\tinjected\tdevice\tnote\ttext");

        foreach (var r in _records)
        {
            sb.AppendLine(string.Join("\t", new[]
            {
                r.DisplayTime,
                r.Source,
                r.Message,
                r.Direction ?? string.Empty,
                r.VirtualKey?.ToString() ?? string.Empty,
                r.ScanCode?.ToString() ?? string.Empty,
                r.Character ?? string.Empty,
                r.CodePoint ?? string.Empty,
                r.FlagsHex ?? string.Empty,
                r.Injected?.ToString() ?? string.Empty,
                r.DeviceName ?? string.Empty,
                r.Note ?? string.Empty,
                r.TextSnapshot?.Replace("\r", "\\r").Replace("\n", "\\n") ?? string.Empty
            }));
        }

        Clipboard.SetText(sb.ToString());
    }

    private static InputEventRecord CopyWithTextSnapshot(InputEventRecord record, string textSnapshot) => new()
    {
        Timestamp = record.Timestamp,
        Source = record.Source,
        Message = record.Message,
        Direction = record.Direction,
        VirtualKey = record.VirtualKey,
        ScanCode = record.ScanCode,
        Character = record.Character,
        CodePoint = record.CodePoint,
        FlagsHex = record.FlagsHex,
        Injected = record.Injected,
        LowerIntegrityInjected = record.LowerIntegrityInjected,
        DeviceHandle = record.DeviceHandle,
        DeviceName = record.DeviceName,
        ExtraInfoHex = record.ExtraInfoHex,
        TextSnapshot = textSnapshot,
        Note = record.Note
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyboardHook.Dispose();
            _logger.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class ProbeTextBox : TextBox
{
    public Func<bool>? ShouldLog { get; set; }
    public event EventHandler<InputEventRecord>? ProbeWindowMessage;

    protected override void WndProc(ref Message m)
    {
        var shouldLog = ShouldLog?.Invoke() ?? true;
        var interesting = WindowMessageNames.IsInteresting(m.Msg) && m.Msg != WindowMessageNames.WM_INPUT;
        var wParam = m.WParam;
        var lParam = m.LParam;

        base.WndProc(ref m);

        if (!shouldLog || !interesting)
        {
            return;
        }

        var record = BuildRecord(m.Msg, wParam, lParam, Text);
        ProbeWindowMessage?.Invoke(this, record);
    }

    private static InputEventRecord BuildRecord(int msg, IntPtr wParam, IntPtr lParam, string textSnapshot)
    {
        var characterInfo = DescribeCharacter(msg, wParam);
        var direction = msg switch
        {
            WindowMessageNames.WM_KEYDOWN or WindowMessageNames.WM_SYSKEYDOWN => "down",
            WindowMessageNames.WM_KEYUP or WindowMessageNames.WM_SYSKEYUP => "up",
            _ => null
        };

        return new InputEventRecord
        {
            Timestamp = DateTimeOffset.Now,
            Source = "wndproc",
            Message = WindowMessageNames.NameOf(msg),
            Direction = direction,
            VirtualKey = IsVirtualKeyMessage(msg) ? wParam.ToInt32() : null,
            Character = characterInfo.Character,
            CodePoint = characterInfo.CodePoint,
            FlagsHex = $"lParam=0x{lParam.ToInt64():X}",
            TextSnapshot = textSnapshot,
            Note = characterInfo.Note
        };
    }

    private static bool IsVirtualKeyMessage(int msg) => msg is
        WindowMessageNames.WM_KEYDOWN or
        WindowMessageNames.WM_KEYUP or
        WindowMessageNames.WM_SYSKEYDOWN or
        WindowMessageNames.WM_SYSKEYUP;

    private static (string? Character, string? CodePoint, string? Note) DescribeCharacter(int msg, IntPtr wParam)
    {
        if (msg is not (WindowMessageNames.WM_CHAR or WindowMessageNames.WM_SYSCHAR or WindowMessageNames.WM_UNICHAR or WindowMessageNames.WM_IME_CHAR or WindowMessageNames.WM_DEADCHAR))
        {
            return (null, null, null);
        }

        var value = wParam.ToInt64();
        var code = unchecked((int)value);
        var note = code switch
        {
            0x08 => "backspace-char",
            0x09 => "tab-char",
            0x0A => "line-feed-char",
            0x0D => "carriage-return-char",
            0x1B => "escape-char",
            _ when code <= 0xFFFF && char.IsControl((char)code) => "control-char",
            _ => null
        };

        string character;
        if (code is >= 0 and <= 0xFFFF)
        {
            var ch = (char)code;
            character = char.IsControl(ch) ? EscapeControlChar(ch) : ch.ToString();
        }
        else if (code <= 0x10FFFF)
        {
            character = char.ConvertFromUtf32(code);
        }
        else
        {
            character = $"<invalid:{code}>";
        }

        return (character, $"U+{code:X4}", note);
    }

    private static string EscapeControlChar(char ch) => ch switch
    {
        '\b' => "\\b",
        '\t' => "\\t",
        '\n' => "\\n",
        '\r' => "\\r",
        '\u001b' => "\\e",
        _ => $"\\u{(int)ch:X4}"
    };
}
