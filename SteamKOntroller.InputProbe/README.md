# SteamKOntroller.InputProbe

Steam Keyboard / physical keyboard / IME input path analyzer PoC.

이 프로그램은 한글 변환기가 아니라 입력 감식기입니다. Steam Keyboard가 Windows에 어떤 입력을 던지는지 확인하기 위해 아래 3개 경로를 동시에 기록합니다.

1. Raw Input (`WM_INPUT`)
2. Low-level keyboard hook (`WH_KEYBOARD_LL`)
3. 테스트 입력창의 `WndProc` 메시지 (`WM_KEYDOWN`, `WM_CHAR`, `WM_IME_COMPOSITION` 등)

## Requirements

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 또는 `dotnet` CLI

## Build

```powershell
dotnet build .\SteamKOntroller.InputProbe.csproj -c Release
```

## Run

```powershell
dotnet run --project .\SteamKOntroller.InputProbe.csproj
```

또는 Release 빌드 후:

```powershell
.\bin\Release\net8.0-windows\SteamKOntroller.InputProbe.exe
```

관리자 권한은 기본적으로 필요하지 않습니다. 다만 관리자 권한 앱을 대상으로 전역 후크 관찰이 필요하면 InputProbe도 관리자 권한으로 실행해서 비교하십시오.

## Logged layers

### 1. Raw Input

`RegisterRawInputDevices` + `WM_INPUT` + `GetRawInputData` 경로입니다.

주요 관찰값:

- `VirtualKey`
- `ScanCode`
- `Flags`
- `DeviceName`
- `DeviceHandle`

### 2. Low-level keyboard hook

`WH_KEYBOARD_LL` 경로입니다.

주요 관찰값:

- `VirtualKey`
- `ScanCode`
- `Flags`
- `Injected`
- `LowerIntegrityInjected`

`Injected = true`면 `SendInput`류 주입 입력일 가능성이 큽니다.

### 3. TextBox WndProc

테스트 입력창 자체의 메시지입니다.

주요 관찰값:

- `WM_KEYDOWN`
- `WM_KEYUP`
- `WM_CHAR`
- `WM_UNICHAR`
- `WM_IME_STARTCOMPOSITION`
- `WM_IME_COMPOSITION`
- `WM_IME_ENDCOMPOSITION`
- `WM_IME_CHAR`

이 레이어는 실제 앱 입력창에 어떤 문자가 도착했는지 확인하는 데 중요합니다.

## Log location

실행할 때마다 JSONL 로그가 아래에 생성됩니다.

```text
%LOCALAPPDATA%\SteamKOntroller\InputProbe\logs\input-probe-yyyyMMdd-HHmmss.jsonl
```

앱의 `Open log folder` 버튼으로 바로 열 수 있습니다.

## Suggested test procedure

### A. Physical keyboard baseline

InputProbe 입력창에 포커스를 두고 물리 키보드로 입력합니다.

```text
a
b
gksrmf
한글 IME ON 상태에서 gksrmf
Backspace
Enter
Space
Shift + key
```

### B. Steam Keyboard test

동일한 입력을 Steam Keyboard로 반복합니다.

```text
a
b
gksrmf
한글 IME ON 상태에서 gksrmf
자모 입력
Backspace
Enter
Space
```

### C. Compare

아래를 비교하십시오.

| 관찰 결과 | 해석 |
| --- | --- |
| Raw Input + Hook + WndProc 모두 잡힘 | 키보드 이벤트에 가까운 경로일 가능성 |
| Hook에는 잡히는데 Raw Input에는 안 잡힘 | 가상 키 입력 또는 주입 입력 가능성 |
| Hook의 `Injected`가 true | `SendInput`류 주입 가능성 |
| `WM_CHAR`만 의미 있게 잡힘 | 문자 메시지 주입 가능성 |
| `WM_IME_COMPOSITION` 발생 | IME 조합 경로 관여 |
| 한글 IME ON인데 자모가 그대로 들어옴 | Steam Keyboard가 IME 조합에 필요한 키 이벤트를 정상 제공하지 않을 가능성 |

## Next decision

이 PoC 로그를 기준으로 다음 중 하나를 선택합니다.

1. Raw Input 기반 감지
2. Low-level hook 기반 감지
3. WndProc/문자 결과 기반 감지
4. Steam Keyboard 입력을 직접 변환하지 않고 별도 입력창/오버레이 방식으로 우회

아직 이 PoC에는 한글 변환 로직을 넣지 않았습니다. 먼저 입력 경로를 확정하는 것이 목적입니다.
