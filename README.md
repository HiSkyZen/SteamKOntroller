<p align="center">
  <img src="docs/icon.png" alt="SteamKOntroller 아이콘" width="96" height="96">
</p>

<h1 align="center">SteamKOntroller</h1>

<p align="center">
  Windows용 Steam 키보드 입력 브리지입니다.
</p>

<p align="center">
  <a href="https://github.com/HiSkyZen/SteamKOntroller/releases">
    <img alt="GitHub 릴리스" src="https://img.shields.io/github/v/release/HiSkyZen/SteamKOntroller?style=for-the-badge&logo=github&label=Release">
  </a>
  <a href="https://github.com/HiSkyZen/SteamKOntroller/blob/main/LICENSE">
    <img alt="라이선스" src="https://img.shields.io/github/license/HiSkyZen/SteamKOntroller?style=for-the-badge">
  </a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512bd4?style=for-the-badge&logo=dotnet&logoColor=white">
  <a href="https://github.com/HiSkyZen/SteamKOntroller/stargazers">
    <img alt="GitHub Stars" src="https://img.shields.io/github/stars/HiSkyZen/SteamKOntroller?style=for-the-badge&logo=github">
  </a>
</p>

## 개요

SteamKOntroller는 Steam 키보드 입력을 관찰하고, 주입된 키 이벤트를 분류한 뒤, 제어된 재주입 경로로 전달하는 Windows 데스크톱 유틸리티입니다.

앱은 Fluent 기반 WinUI 설정 화면을 제공합니다. 여기에서 브리지 활성화, Windows 로그인 시 시작, 런타임 카운터 확인, 진단 로그 기록 여부를 관리할 수 있습니다.

## 주요 기능

- WinUI 3 데스크톱 앱
- 저수준 키보드 훅 기반 Steam 키보드 후보 감지
- 입력 억제 및 스캔 코드 재주입 파이프라인
- 알림 영역 우선 시작 및 선택적 Windows 로그인 시작
- 런타임 상태/카운터와 상세 진단 이벤트 분리
- 압축 및 보존 기간 정리를 지원하는 선택적 JSONL 진단 로그
- Raw Input, 훅, 창 메시지 경로를 검사하는 InputProbe 유틸리티

## 저장소 구조

```text
SteamKOntroller.App/        WinUI 3 데스크톱 애플리케이션
SteamKOntroller.Bundle/     라이선스 UI가 포함된 WiX Burn 부트스트래퍼
SteamKOntroller.Core/       입력 브리지, 분류, 정책, 진단
SteamKOntroller.Installer/  WiX MSI 설치 패키지
SteamKOntroller.InstallerAgent/
                            외부 설치 단계를 처리하는 MSI 설치 도우미
SteamKOntroller.InputProbe/ 입력 경로 분석 유틸리티
SteamKOntroller.Tests/      핵심 동작 경량 테스트
docs/                       프로젝트 에셋
```

## 요구 사항

- Windows 10 버전 1809 이상
- .NET 10 SDK
- Visual Studio 2022 또는 `dotnet` CLI

## 빌드

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet restore .\SteamKOntroller.slnx
dotnet build .\SteamKOntroller.App\SteamKOntroller.App.csproj -c Release -p:Platform=$Platform
dotnet build .\SteamKOntroller.InputProbe\SteamKOntroller.InputProbe.csproj -c Release
dotnet build .\SteamKOntroller.Tests\SteamKOntroller.Tests.csproj -c Release
```

## 설치 프로그램

WiX Burn 설치 프로그램은 앱을 게시한 뒤 부트스트래퍼 EXE와 MSI를 함께 빌드합니다. 설치 프로그램 검증은 빌드 단계까지만 진행하세요. SteamKOntroller를 실제로 설치하고 Steam을 재시작하려는 경우가 아니라면 생성된 설치 프로그램을 실행하지 마세요.

실제 설치에는 기본 self-contained 설치 파일인 `SteamKOntroller.Setup.exe`를 사용합니다. Burn은 한국어 라이선스 UI를 표시하고, 사전 설치 작업, 숨김 앱 MSI, 설치 완료 작업을 순서대로 실행합니다. MSI는 필수 구성 요소용 custom action을 포함하지 않으며 프로그램 및 기능에 노출되지 않습니다. 따라서 Burn 부트스트래퍼만 단일 등록 설치 항목으로 남습니다.

Bundle 빌드는 `SteamKOntroller.Setup.exe`와 함께 framework-dependent 설치 파일인 `SteamKOntroller.Setup.FrameworkDependent.exe`도 생성합니다. Framework-dependent 설치 파일은 설치 사전 단계에서 .NET Desktop Runtime 10과 Windows App SDK Runtime 2.1.3 설치 여부를 확인하고, 누락된 경우 사용자 동의를 받은 뒤 Microsoft 공식 설치 관리자로 설치합니다.

설치 도중 Steam 경로 선택, Millennium 설치 확인, Steam 재시작 확인처럼 별도 창이 필요한 단계는 InstallerAgent가 전면 포커스를 확보하도록 처리합니다.

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet build .\SteamKOntroller.Bundle\SteamKOntroller.Bundle.wixproj -c Release -p:Platform=$Platform
```

## 실행

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet run --project .\SteamKOntroller.App\SteamKOntroller.App.csproj -p:Platform=$Platform
```

앱은 기본적으로 알림 영역에서 시작합니다. 개발 중 설정 창을 바로 열려면 `-- --show`를 추가하세요.

InputProbe 유틸리티는 별도로 실행할 수 있습니다.

```powershell
dotnet run --project .\SteamKOntroller.InputProbe\SteamKOntroller.InputProbe.csproj
```

## 테스트

```powershell
$arch = $env:PROCESSOR_ARCHITECTURE
$Platform = if ($arch -eq 'AMD64') { 'x64' } else { $arch }
dotnet run --project .\SteamKOntroller.Tests\SteamKOntroller.Tests.csproj -p:Platform=$Platform
```

## 진단

영구 진단 로그는 기본적으로 비활성화되어 있습니다. 비활성화 상태에서는 키별 상세 진단 레코드가 UI나 디스크에 기록되지 않으며, 런타임 카운터도 마지막 키 값을 보존하지 않습니다.

앱에서 진단 로그를 활성화하면 키 값은 마스킹된 상태로 기록됩니다. Debug 빌드는 원시 키 값을 기록하는 고위험 민감 입력 로그 옵션을 별도로 제공하며, 활성화 전에 경고를 표시합니다. 완료된 JSONL 로그는 압축되고, 설정된 보존 기간에 따라 오래된 로그가 삭제됩니다. 로그 폴더는 앱의 진단 페이지에서 열 수 있습니다.

## 라이선스

이 프로젝트는 MIT 라이선스로 배포됩니다.
