using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SteamKOntroller.InputProbe.Logging;

namespace SteamKOntroller.InputProbe.Native;

public sealed class Win32KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int LLKHF_EXTENDED = 0x01;
    private const int LLKHF_LOWER_IL_INJECTED = 0x02;
    private const int LLKHF_INJECTED = 0x10;
    private const int LLKHF_ALTDOWN = 0x20;
    private const int LLKHF_UP = 0x80;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId;

    public event EventHandler<InputEventRecord>? KeyboardEvent;

    public Win32KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = GetModuleHandle(currentModule?.ModuleName);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_KEYBOARD_LL) failed.");
        }
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        if (!UnhookWindowsHookEx(_hookId))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UnhookWindowsHookEx failed.");
        }

        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var flags = unchecked((int)info.flags);

            KeyboardEvent?.Invoke(this, new InputEventRecord
            {
                Timestamp = DateTimeOffset.Now,
                Source = "hook",
                Message = WindowMessageNames.NameOf(wParam.ToInt32()),
                Direction = (flags & LLKHF_UP) != 0 ? "up" : "down",
                VirtualKey = unchecked((int)info.vkCode),
                ScanCode = unchecked((int)info.scanCode),
                FlagsHex = $"0x{info.flags:X8}",
                Injected = (flags & LLKHF_INJECTED) != 0,
                LowerIntegrityInjected = (flags & LLKHF_LOWER_IL_INJECTED) != 0,
                ExtraInfoHex = $"0x{unchecked((long)info.dwExtraInfo.ToUInt64()):X}",
                Note = BuildNote(flags)
            });
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static string BuildNote(int flags)
    {
        var parts = new List<string>();
        if ((flags & LLKHF_EXTENDED) != 0) parts.Add("extended");
        if ((flags & LLKHF_LOWER_IL_INJECTED) != 0) parts.Add("lower_il_injected");
        if ((flags & LLKHF_INJECTED) != 0) parts.Add("injected");
        if ((flags & LLKHF_ALTDOWN) != 0) parts.Add("alt_down");
        if ((flags & LLKHF_UP) != 0) parts.Add("up");
        return parts.Count == 0 ? "normal" : string.Join(",", parts);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
