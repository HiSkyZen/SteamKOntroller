<p align="center">
  <img src="docs/icon.png" alt="SteamKOntroller icon" width="96" height="96">
</p>

<h1 align="center">SteamKOntroller</h1>

<p align="center">
  Steam Keyboard input bridge for Windows.
</p>

<p align="center">
  <a href="https://github.com/HiSkyZen/SteamKOntroller/releases">
    <img alt="GitHub Release" src="https://img.shields.io/github/v/release/HiSkyZen/SteamKOntroller?style=for-the-badge&logo=github&label=Release">
  </a>
  <a href="https://github.com/HiSkyZen/SteamKOntroller/blob/main/LICENSE">
    <img alt="License" src="https://img.shields.io/github/license/HiSkyZen/SteamKOntroller?style=for-the-badge">
  </a>
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512bd4?style=for-the-badge&logo=dotnet&logoColor=white">
  <a href="https://github.com/HiSkyZen/SteamKOntroller/stargazers">
    <img alt="GitHub Stars" src="https://img.shields.io/github/stars/HiSkyZen/SteamKOntroller?style=for-the-badge&logo=github">
  </a>
</p>

## Overview

SteamKOntroller is a Windows desktop utility that observes Steam Keyboard input, classifies injected key events, and bridges them through a controlled reinjection path.

The application includes a Fluent WinUI settings interface for enabling the bridge, managing startup behavior, checking runtime counters, and opting into diagnostic logs.

## Features

- WinUI 3 desktop app
- Steam Keyboard candidate detection through low-level keyboard hooks
- Suppression and scancode reinjection pipeline
- Tray-first startup and optional Windows sign-in launch
- Runtime status and counters separated from diagnostic event details
- Optional JSONL diagnostic logging with compression and retention cleanup
- InputProbe utility for inspecting Raw Input, hook, and window message paths

## Repository Layout

```text
SteamKOntroller.App/        WinUI 3 desktop application
SteamKOntroller.Core/       Input bridge, classification, policy, diagnostics
SteamKOntroller.InputProbe/ Input path analyzer utility
SteamKOntroller.Tests/      Lightweight core behavior tests
docs/                       Project assets
```

## Requirements

- Windows 10 version 1809 or later
- .NET 8 SDK
- Visual Studio 2022 or the `dotnet` CLI

## Build

```powershell
dotnet restore .\SteamKOntroller.slnx
dotnet build .\SteamKOntroller.slnx -c Release
```

## Run

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet run --project .\SteamKOntroller.App\SteamKOntroller.App.csproj -p:Platform=$Platform
```

The app starts in the notification area by default. Add `-- --show` to open the settings window during development.

The InputProbe utility can be started separately:

```powershell
dotnet run --project .\SteamKOntroller.InputProbe\SteamKOntroller.InputProbe.csproj
```

## Test

```powershell
dotnet run --project .\SteamKOntroller.Tests\SteamKOntroller.Tests.csproj
```

## Diagnostics

Persistent diagnostic logging is disabled by default. When enabled in the app, completed JSONL logs are compressed and old logs are deleted according to the configured retention period. Use the app's diagnostics page to open the log directory.

## License

This project is licensed under the MIT License.
