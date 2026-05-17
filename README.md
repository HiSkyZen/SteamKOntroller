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
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%2B-0078d4?style=for-the-badge&logo=windows&logoColor=white">
  <a href="https://github.com/HiSkyZen/SteamKOntroller/stargazers">
    <img alt="GitHub Stars" src="https://img.shields.io/github/stars/HiSkyZen/SteamKOntroller?style=for-the-badge&logo=github">
  </a>
</p>

## Overview

SteamKOntroller is a Windows desktop utility that observes Steam Keyboard input, classifies injected key events, and bridges them through a controlled reinjection path.

The application includes a compact WinUI interface for enabling the bridge, checking runtime counters, and opening diagnostic logs.

## Features

- WinUI 3 desktop app for Windows 10 and Windows 11
- Steam Keyboard candidate detection through low-level keyboard hooks
- Suppression and scancode reinjection pipeline
- Runtime status, counters, and diagnostic event view
- JSONL diagnostic logging
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
dotnet run --project .\SteamKOntroller.App\SteamKOntroller.App.csproj
```

The InputProbe utility can be started separately:

```powershell
dotnet run --project .\SteamKOntroller.InputProbe\SteamKOntroller.InputProbe.csproj
```

## Test

```powershell
dotnet run --project .\SteamKOntroller.Tests\SteamKOntroller.Tests.csproj
```

## Diagnostics

Diagnostic logs are written as JSONL files under the user's local application data folder. Use the app's log button to open the current log directory.

## License

This project is licensed under the MIT License.
