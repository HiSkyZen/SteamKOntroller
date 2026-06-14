using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SteamKOntroller.InputProbe.Logging;

namespace SteamKOntroller.InputProbe.Native;

public static class RawInput
{
    private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    private const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;

    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIDI_DEVICENAME = 0x20000007;
    private const uint RIM_TYPEKEYBOARD = 1;

    private const ushort RI_KEY_BREAK = 0x0001;
    private const ushort RI_KEY_E0 = 0x0002;
    private const ushort RI_KEY_E1 = 0x0004;

    public static void RegisterKeyboard(IntPtr hwnd)
    {
        var devices = new[]
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = hwnd
            }
        };

        var ok = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        if (!ok)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterRawInputDevices failed.");
        }
    }

    public static InputEventRecord? TryReadKeyboardInput(IntPtr rawInputHandle, string? textSnapshot, out string? error)
    {
        error = null;
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();

        var initial = GetRawInputData(rawInputHandle, RID_INPUT, IntPtr.Zero, ref size, headerSize);
        if (initial == unchecked((uint)-1) || size == 0)
        {
            error = $"GetRawInputData(size) failed: {Marshal.GetLastWin32Error()}";
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var read = GetRawInputData(rawInputHandle, RID_INPUT, buffer, ref size, headerSize);
            if (read == unchecked((uint)-1))
            {
                error = $"GetRawInputData(data) failed: {Marshal.GetLastWin32Error()}";
                return null;
            }

            var raw = Marshal.PtrToStructure<RAWINPUTKEYBOARD>(buffer);
            if (raw.header.dwType != RIM_TYPEKEYBOARD)
            {
                return null;
            }

            var keyboard = raw.keyboard;
            var flags = keyboard.Flags;
            var direction = (flags & RI_KEY_BREAK) != 0 ? "up" : "down";
            var deviceName = GetDeviceName(raw.header.hDevice);

            return new InputEventRecord
            {
                Timestamp = DateTimeOffset.Now,
                Source = "raw",
                Message = WindowMessageNames.NameOf(unchecked((int)keyboard.Message)),
                Direction = direction,
                VirtualKey = keyboard.VKey,
                ScanCode = keyboard.MakeCode,
                FlagsHex = $"0x{flags:X4}",
                Injected = null,
                DeviceHandle = raw.header.hDevice == IntPtr.Zero ? "0x0" : $"0x{raw.header.hDevice.ToInt64():X}",
                DeviceName = deviceName,
                ExtraInfoHex = $"0x{keyboard.ExtraInformation:X8}",
                TextSnapshot = textSnapshot,
                Note = BuildRawNote(flags)
            };
        }
        finally
        {
            ZeroUnmanagedMemory(buffer, (int)size);
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ZeroUnmanagedMemory(IntPtr buffer, int length)
    {
        if (buffer == IntPtr.Zero || length <= 0)
        {
            return;
        }

        var zeros = new byte[length];
        Marshal.Copy(zeros, 0, buffer, length);
        CryptographicOperations.ZeroMemory(zeros);
    }

    private static string BuildRawNote(ushort flags)
    {
        var parts = new List<string>();
        if ((flags & RI_KEY_BREAK) == 0) parts.Add("make");
        if ((flags & RI_KEY_BREAK) != 0) parts.Add("break");
        if ((flags & RI_KEY_E0) != 0) parts.Add("e0");
        if ((flags & RI_KEY_E1) != 0) parts.Add("e1");
        return parts.Count == 0 ? "normal" : string.Join(",", parts);
    }

    private static string? GetDeviceName(IntPtr deviceHandle)
    {
        if (deviceHandle == IntPtr.Zero)
        {
            return null;
        }

        uint charCount = 0;
        var query = GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICENAME, null, ref charCount);
        if (query == unchecked((uint)-1) || charCount == 0)
        {
            return null;
        }

        var builder = new StringBuilder((int)charCount);
        var result = GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICENAME, builder, ref charCount);
        return result == unchecked((uint)-1) ? null : builder.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTKEYBOARD
    {
        public RAWINPUTHEADER header;
        public RAWKEYBOARD keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }


    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice,
        uint uiCommand,
        StringBuilder? pData,
        ref uint pcbSize);
}
