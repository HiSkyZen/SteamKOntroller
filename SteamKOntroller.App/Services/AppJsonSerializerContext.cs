using System.Text.Json.Serialization;
using SteamKOntroller.Core.Diagnostics;

namespace SteamKOntroller.App.Services;

[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(InputDiagnosticRecord))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
