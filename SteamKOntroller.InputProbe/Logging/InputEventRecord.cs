using System.Text.Json.Serialization;

namespace SteamKOntroller.InputProbe.Logging;

public sealed class InputEventRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Direction { get; set; }
    public int? VirtualKey { get; set; }
    public int? ScanCode { get; set; }
    public string? Character { get; set; }
    public string? CodePoint { get; set; }
    public string? FlagsHex { get; set; }
    public bool? Injected { get; set; }
    public bool? LowerIntegrityInjected { get; set; }
    public string? DeviceHandle { get; set; }
    public string? DeviceName { get; set; }
    public string? ExtraInfoHex { get; set; }
    public string? TextSnapshot { get; set; }
    public string? Note { get; set; }

    [JsonIgnore]
    public string DisplayTime => Timestamp.ToString("HH:mm:ss.fff");

    public void ClearSensitiveFields()
    {
        Direction = null;
        VirtualKey = null;
        ScanCode = null;
        Character = null;
        CodePoint = null;
        FlagsHex = null;
        Injected = null;
        LowerIntegrityInjected = null;
        DeviceHandle = null;
        DeviceName = null;
        ExtraInfoHex = null;
        TextSnapshot = null;
        Note = null;
    }
}
