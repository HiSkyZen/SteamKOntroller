using System.Text.Json.Serialization;

namespace SteamKOntroller.InputProbe.Logging;

public sealed class InputEventRecord
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Direction { get; init; }
    public int? VirtualKey { get; init; }
    public int? ScanCode { get; init; }
    public string? Character { get; init; }
    public string? CodePoint { get; init; }
    public string? FlagsHex { get; init; }
    public bool? Injected { get; init; }
    public bool? LowerIntegrityInjected { get; init; }
    public string? DeviceHandle { get; init; }
    public string? DeviceName { get; init; }
    public string? ExtraInfoHex { get; init; }
    public string? TextSnapshot { get; init; }
    public string? Note { get; init; }

    [JsonIgnore]
    public string DisplayTime => Timestamp.ToString("HH:mm:ss.fff");
}
