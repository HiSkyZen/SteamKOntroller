using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using SteamKOntroller.Core.Native;

namespace SteamKOntroller.Core.Capture;

public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;

    private readonly Func<LowLevelKeyboardEvent, bool> _processor;
    private readonly LowLevelKeyboardProc _callback;
    private readonly ManualResetEventSlim _started = new(false);
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hookId;
    private ExceptionDispatchInfo? _startupException;
    private bool _disposed;

    public LowLevelKeyboardHook(Func<LowLevelKeyboardEvent, bool> processor)
    {
        _processor = processor;
        _callback = HookCallback;
    }

    public bool IsRunning => _hookId != IntPtr.Zero;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread is { IsAlive: true })
        {
            return;
        }

        _started.Reset();
        _startupException = null;
        _thread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "SteamKOntroller keyboard hook"
        };
        if (OperatingSystem.IsWindows())
        {
            _thread.SetApartmentState(ApartmentState.STA);
        }
        _thread.Start();

        if (!_started.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("WH_KEYBOARD_LL hook thread did not start within 5 seconds.");
        }

        _startupException?.Throw();
    }

    public void Stop()
    {
        var thread = _thread;
        if (thread is null)
        {
            return;
        }

        var threadId = Volatile.Read(ref _threadId);
        if (threadId != 0)
        {
            PostThreadMessage(threadId, WindowMessages.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        if (Thread.CurrentThread.ManagedThreadId != thread.ManagedThreadId)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }

        _thread = null;
        _threadId = 0;
    }

    private void HookThreadMain()
    {
        Volatile.Write(ref _threadId, GetCurrentThreadId());

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            var moduleHandle = GetModuleHandle(currentModule?.ModuleName);

            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, moduleHandle, 0);
            if (_hookId == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_KEYBOARD_LL) failed.");
            }

            _started.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception ex)
        {
            _startupException = ExceptionDispatchInfo.Capture(ex);
            _started.Set();
        }
        finally
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var nativeEvent = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var keyboardEvent = new LowLevelKeyboardEvent(
                DateTimeOffset.Now,
                wParam.ToInt32(),
                unchecked((ushort)nativeEvent.vkCode),
                unchecked((ushort)nativeEvent.scanCode),
                (LowLevelKeyboardFlags)nativeEvent.flags,
                nativeEvent.dwExtraInfo);

            if (_processor(keyboardEvent))
            {
                return new IntPtr(1);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _started.Dispose();
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
}
