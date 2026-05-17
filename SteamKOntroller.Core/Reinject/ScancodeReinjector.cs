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

    private readonly Channel<ScanCodeInjectionRequest> _queue = Channel.CreateUnbounded<ScanCodeInjectionRequest>(
        new UnboundedChannelOptions
        {
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

    public bool TryEnqueueTap(ushort virtualKey, ushort scanCode, bool extended)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _queue.Writer.TryWrite(new ScanCodeInjectionRequest(virtualKey, scanCode, extended));
    }

    private async Task WorkerLoopAsync()
    {
        try
        {
            await foreach (var request in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                var result = SendTap(request);
                InjectionCompleted?.Invoke(this, result);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static SendInputResult SendTap(ScanCodeInjectionRequest request)
    {
        var flagsDown = KEYEVENTF_SCANCODE;
        var flagsUp = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;

        if (request.Extended)
        {
            flagsDown |= KEYEVENTF_EXTENDEDKEY;
            flagsUp |= KEYEVENTF_EXTENDEDKEY;
        }

        var inputs = new[]
        {
            new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
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
                        wVk = 0,
                        wScan = request.ScanCode,
                        dwFlags = flagsUp,
                        time = 0,
                        dwExtraInfo = InjectionMarker.Value
                    }
                }
            }
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        var lastError = sent == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        var error = lastError == 0 ? null : new Win32Exception(lastError).Message;
        return new SendInputResult(request.VirtualKey, request.ScanCode, sent, lastError, error);
    }

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
