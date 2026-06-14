using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace SteamKOntroller.Core.Reinject;

public sealed class ScancodeReinjector : IDisposable
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const ushort LeftShiftScanCode = 0x2A;
    private const int MaxQueuedRequests = 128;

    private readonly Channel<ScanCodeInjectionRequest> _queue = Channel.CreateBounded<ScanCodeInjectionRequest>(
        new BoundedChannelOptions(MaxQueuedRequests)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource _cts = new();
    private Task? _worker;
    private bool _disposed;

    public event EventHandler<SendInputResult>? InjectionCompleted;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _worker ??= Task.Run(WorkerLoopAsync);
    }

    public bool TryEnqueueTap(ushort virtualKey, ushort scanCode, bool extended, bool mockShift = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _queue.Writer.TryWrite(new ScanCodeInjectionRequest(virtualKey, scanCode, extended, mockShift));
    }

    public bool TryEnqueueVirtualKeyTap(ushort virtualKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _queue.Writer.TryWrite(new ScanCodeInjectionRequest(virtualKey, ScanCode: 0, Extended: false));
    }

    private async Task WorkerLoopAsync()
    {
        try
        {
            await foreach (var request in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                var result = SendTap(request);
                InjectionCompleted?.Invoke(this, result);
                result = default;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static SendInputResult SendTap(ScanCodeInjectionRequest request)
    {
        var useVirtualKey = request.ScanCode == 0;
        var flagsDown = useVirtualKey ? 0 : KEYEVENTF_SCANCODE;
        var flagsUp = useVirtualKey ? KEYEVENTF_KEYUP : KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;

        if (request.Extended)
        {
            flagsDown |= KEYEVENTF_EXTENDEDKEY;
            flagsUp |= KEYEVENTF_EXTENDEDKEY;
        }

        INPUT[]? keyInputs = null;
        INPUT[]? inputs = null;

        try
        {
            keyInputs = new[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = useVirtualKey ? request.VirtualKey : (ushort)0,
                            wScan = request.ScanCode,
                            dwFlags = flagsDown,
                            time = 0,
                            dwExtraInfo = InjectionMarker.Value
                        }
                    }
                },
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = useVirtualKey ? request.VirtualKey : (ushort)0,
                            wScan = request.ScanCode,
                            dwFlags = flagsUp,
                            time = 0,
                            dwExtraInfo = InjectionMarker.Value
                        }
                    }
                }
            };

            inputs = request.MockShift
                ? new[]
                {
                    CreateScanCodeInput(LeftShiftScanCode, 0),
                    keyInputs[0],
                    keyInputs[1],
                    CreateScanCodeInput(LeftShiftScanCode, KEYEVENTF_KEYUP)
                }
                : keyInputs;

            var expectedCount = (uint)inputs.Length;
            var sent = SendInput(expectedCount, inputs, Marshal.SizeOf<INPUT>());
            var lastError = sent == expectedCount ? 0 : Marshal.GetLastWin32Error();
            var error = lastError == 0 ? null : new Win32Exception(lastError).Message;
            return new SendInputResult(request.VirtualKey, request.ScanCode, sent, lastError, error, expectedCount);
        }
        finally
        {
            if (inputs is not null)
            {
                Array.Clear(inputs, 0, inputs.Length);
            }

            if (keyInputs is not null && !ReferenceEquals(keyInputs, inputs))
            {
                Array.Clear(keyInputs, 0, keyInputs.Length);
            }
        }
    }

    private static INPUT CreateScanCodeInput(ushort scanCode, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = scanCode,
                dwFlags = KEYEVENTF_SCANCODE | flags,
                time = 0,
                dwExtraInfo = InjectionMarker.Value
            }
        }
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);
}
