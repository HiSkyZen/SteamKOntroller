using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Win32;

namespace SteamKOntroller.InstallerAgent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var status = new StatusDialog();

        try
        {
            var options = CommandLineOptions.Parse(args);
            var installDirectory = options.InstallDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InstallerConstants.AppName);

            status.Text = options.Mode == InstallerMode.Uninstall
                ? "SteamKOntroller 제거"
                : "SteamKOntroller 설치";
            status.ShowForeground();
            switch (options.Mode)
            {
                case InstallerMode.PreInstall:
                    InstallerState.Save(RunPreInstall(status, options.InstallRuntimePrerequisites));
                    break;
                case InstallerMode.PostInstall:
                    var steamDirectory = InstallerState.TryLoad()?.SteamDirectory
                        ?? SteamInstallation.ResolveOrPrompt(status);
                    RunPostInstall(installDirectory, steamDirectory, status);
                    break;
                case InstallerMode.Uninstall:
                    RunUninstall(status);
                    break;
            }


            InstallerLog.Info("Installer agent completed.");
            return 0;
        }
        catch (InstallerCanceledException ex)
        {
            status.Close();
            InstallerLog.Info(ex.Message);
            MessageBox.Show(ex.Message, InstallerConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1602;
        }
        catch (Exception ex)
        {
            status.Close();
            InstallerLog.Error(ex);
            MessageBox.Show(
                "설치를 완료하지 못했습니다.\n\n" + ex.Message + "\n\n자세한 로그: " + InstallerLog.LogPath,
                InstallerConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1603;
        }
    }

    private static InstallerState RunPreInstall(StatusDialog status, bool installRuntimePrerequisites)
    {
        if (installRuntimePrerequisites)
        {
            status.SetStatus(".NET Desktop Runtime 10 설치 여부를 확인하고 있습니다.");
            DotNetDesktopRuntime.EnsureInstalled(status);

            status.SetStatus("Windows App SDK Runtime 2.1.3 설치 여부를 확인하고 있습니다.");
            WindowsAppSdkRuntime.EnsureInstalled(status);
        }

        status.SetStatus("Steam 설치 경로를 확인하고 있습니다.");
        var steamDirectory = SteamInstallation.ResolveOrPrompt(status);

        status.SetStatus("Millennium 설치 여부를 확인하고 있습니다.");
        var millennium = MillenniumInstallation.ResolveOrInstall(steamDirectory, status);

        status.SetStatus("SteamKOntroller-KBD 플러그인을 설치하고 있습니다.");
        PluginInstaller.Install(millennium);

        return new InstallerState(steamDirectory);
    }

    private static void RunPostInstall(string installDirectory, string steamDirectory, StatusDialog status)
    {
        var millennium = MillenniumInstallation.ResolveOrInstall(steamDirectory, status);

        status.SetStatus("Millennium 플러그인 설정을 업데이트하고 있습니다.");
        MillenniumConfiguration.EnablePlugin(millennium.ConfigPath, InstallerConstants.PluginId);

        status.SetStatus("Steam을 재시작하고 있습니다.");
        SteamRestart.Restart(steamDirectory, status);

        InstallerState.Delete();
    }

    private static void RunUninstall(StatusDialog status)
    {
        status.SetStatus("Steam 설치 경로를 확인하고 있습니다.");
        var steamDirectory = InstallerState.TryLoad()?.SteamDirectory
            ?? SteamInstallation.ResolveOrPrompt(status);

        var millennium = MillenniumInstallation.TryDetectForCleanup(steamDirectory);
        if (millennium is null)
        {
            InstallerLog.Info("Millennium installation was not detected. Skipping Millennium cleanup.");
            InstallerState.Delete();
            return;
        }

        var removeMillennium = InstallerUi.ShowMessage(
            status,
            "SteamKOntroller 제거와 함께 Millennium도 제거하시겠습니까?\n\n" +
            "예: Millennium 전체를 제거합니다.\n" +
            "아니요: SteamKOntroller-KBD 플러그인만 제거하고 Millennium은 유지합니다.",
            "Millennium 제거",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        status.SetStatus("Steam을 종료하고 Millennium 파일을 정리하고 있습니다.");
        SteamRestart.Stop(status);

        if (removeMillennium)
        {
            MillenniumUninstaller.RemoveMillennium(steamDirectory, millennium);
        }
        else
        {
            MillenniumUninstaller.RemovePluginOnly(millennium);
        }

        InstallerState.Delete();
    }
}

internal enum InstallerMode
{
    PreInstall,
    PostInstall,
    Uninstall,
}

internal static class InstallerConstants
{
    public const string AppExecutableName = "SteamKOntroller.App.exe";
    public const string AppName = "SteamKOntroller";
    public const string DesktopRuntimePackageId = "Microsoft.DotNet.DesktopRuntime.10";
    public const string MillenniumInstallerUri = "https://github.com/SteamClientHomebrew/Installer/releases/latest/download/MillenniumInstaller-Windows.exe";
    public const string PluginId = "steam-korean-keyboard-injection";
    public const string PluginUri = "https://github.com/HiSkyZen/SteamKOntroller-KBD/releases/download/v1.0.0/steam-korean-keyboard-injection.zip";
    public const string WindowsAppSdkRuntimeInstallerVersion = "2.1.3";
    public const string WindowsAppSdkRuntimePackageName = "Microsoft.WindowsAppRuntime.2";
    public static readonly Version WindowsAppSdkRequiredRuntimeVersion = new(2, 1, 3, 0);
}

internal sealed record CommandLineOptions(string? InstallDirectory, InstallerMode Mode, bool InstallRuntimePrerequisites)
{
    public static CommandLineOptions Parse(string[] args)
    {
        string? installDirectory = null;
        InstallerMode? mode = null;
        var installRuntimePrerequisites = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--install-dir", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length)
            {
                installDirectory = args[++index];
            }
            else if (string.Equals(args[index], "--mode", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length)
            {
                mode = ParseMode(args[++index]);
            }
            else if (string.Equals(args[index], "--install-runtime-prerequisites", StringComparison.OrdinalIgnoreCase))
            {
                installRuntimePrerequisites = true;
            }
        }

        if (mode is null)
        {
            throw new ArgumentException("설치 작업 모드가 지정되지 않았습니다. WiX Burn bootstrapper를 통해 실행해야 합니다.");
        }

        return new CommandLineOptions(installDirectory, mode.Value, installRuntimePrerequisites);
    }

    private static InstallerMode ParseMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "preinstall" => InstallerMode.PreInstall,
            "postinstall" => InstallerMode.PostInstall,
            "uninstall" => InstallerMode.Uninstall,
            _ => throw new ArgumentException("알 수 없는 설치 도우미 모드입니다: " + value),
        };
    }
}

internal sealed record InstallerState(string SteamDirectory)
{
    private static string StatePath => Path.Combine(Path.GetTempPath(), "SteamKOntroller.InstallerAgent.state.json");

    public static void Save(InstallerState state)
    {
        var json = JsonSerializer.Serialize(state);
        File.WriteAllText(StatePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static InstallerState? TryLoad()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<InstallerState>(File.ReadAllText(StatePath))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed class InstallerCanceledException(string message) : Exception(message);

internal sealed class StatusDialog : Form
{
    private readonly Label _statusLabel;

    public StatusDialog()
    {
        Text = "SteamKOntroller 설치";
        Width = 460;
        Height = 150;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        _statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        Controls.Add(_statusLabel);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        BringToForeground();
    }

    public void ShowForeground()
    {
        Show();
        BringToForeground();
    }

    public void BringToForeground()
    {
        if (IsDisposed)
        {
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        TopMost = false;
        TopMost = true;
        BringToFront();
        Activate();
        NativeMethods.SetForegroundWindow(Handle);
    }

    public void SetStatus(string message)
    {
        InstallerLog.Info(message);
        _statusLabel.Text = message;
        BringToForeground();
        Refresh();
        Application.DoEvents();
    }
}

internal static class InstallerUi
{
    public static DialogResult ShowMessage(
        IWin32Window owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        BringOwnerToForeground(owner);
        return MessageBox.Show(owner, text, caption, buttons, icon);
    }

    public static void BringOwnerToForeground(IWin32Window owner)
    {
        if (owner is StatusDialog status)
        {
            status.BringToForeground();
            return;
        }

        if (owner is Form form && !form.IsDisposed)
        {
            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            form.TopMost = false;
            form.TopMost = true;
            form.BringToFront();
            form.Activate();
            NativeMethods.SetForegroundWindow(form.Handle);
        }
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}

internal static class DotNetDesktopRuntime
{
    public static void EnsureInstalled(IWin32Window owner)
    {
        if (IsInstalled())
        {
            InstallerLog.Info(".NET Desktop Runtime 10 is already installed.");
            return;
        }

        var consent = InstallerUi.ShowMessage(
            owner,
            "SteamKOntroller를 실행하려면 .NET Desktop Runtime 10이 필요합니다.\n\nwinget을 먼저 사용하고, 실패하면 Microsoft 공식 설치 관리자를 다운로드해 설치합니다. 지금 설치하시겠습니까?",
            "필수 구성 요소 설치",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (consent != DialogResult.Yes)
        {
            throw new InstallerCanceledException(".NET Desktop Runtime 10 설치가 취소되었습니다.");
        }

        if (!TryInstallWithWinget())
        {
            InstallWithMicrosoftInstaller();
        }

        if (!IsInstalled())
        {
            throw new InvalidOperationException(".NET Desktop Runtime 10 설치를 확인하지 못했습니다.");
        }
    }

    private static bool IsInstalled()
    {
        return DirectoryContainsMajorVersion(GetSharedFrameworkDirectory(), 10)
            || RegistryContainsMajorVersion(RegistryView.Registry64, "x64", 10)
            || RegistryContainsMajorVersion(RegistryView.Registry32, "x64", 10)
            || RegistryContainsMajorVersion(RegistryView.Registry32, "x86", 10);
    }

    private static string GetSharedFrameworkDirectory()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
    }

    private static bool DirectoryContainsMajorVersion(string directory, int majorVersion)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        return Directory.EnumerateDirectories(directory)
            .Select(Path.GetFileName)
            .Any(version => Version.TryParse(version, out var parsed) && parsed.Major == majorVersion);
    }

    private static bool RegistryContainsMajorVersion(RegistryView view, string architecture, int majorVersion)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey($@"SOFTWARE\dotnet\Setup\InstalledVersions\{architecture}\sharedfx\Microsoft.WindowsDesktop.App");
            if (key is null)
            {
                return false;
            }

            return key.GetValueNames()
                .Any(version => Version.TryParse(version, out var parsed) && parsed.Major == majorVersion);
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryInstallWithWinget()
    {
        var wingetPath = ProcessLauncher.FindExecutable("winget.exe");
        if (wingetPath is null)
        {
            InstallerLog.Info("winget.exe was not found.");
            return false;
        }

        InstallerLog.Info("Installing .NET Desktop Runtime 10 with winget.");
        try
        {
            var exitCode = ProcessLauncher.Run(
                wingetPath,
                $"install --id {InstallerConstants.DesktopRuntimePackageId} --exact --silent --accept-package-agreements --accept-source-agreements",
                allowShellExecute: false);

            return exitCode is 0 or 3010;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            InstallerLog.Info("winget installation failed to start: " + ex.Message);
            return false;
        }
    }

    private static void InstallWithMicrosoftInstaller()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        var installerUri = $"https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-{architecture}.exe";
        var installerPath = Path.Combine(Path.GetTempPath(), $"windowsdesktop-runtime-10-{architecture}.exe");

        InstallerLog.Info("Downloading .NET Desktop Runtime 10 installer from Microsoft.");
        Downloader.Download(installerUri, installerPath);

        InstallerLog.Info("Running .NET Desktop Runtime 10 installer.");
        var exitCode = ProcessLauncher.Run(installerPath, "/install /quiet /norestart", allowShellExecute: true);
        if (exitCode is not (0 or 3010))
        {
            throw new InvalidOperationException($".NET Desktop Runtime 10 설치 관리자가 종료 코드 {exitCode}로 실패했습니다.");
        }
    }
}

internal static class WindowsAppSdkRuntime
{
    public static void EnsureInstalled(IWin32Window owner)
    {
        if (IsInstalled())
        {
            InstallerLog.Info("Windows App SDK Runtime 2.1.3 or later is already installed.");
            return;
        }

        var consent = InstallerUi.ShowMessage(
            owner,
            "SteamKOntroller framework-dependent 설치 파일을 실행하려면 Windows App SDK Runtime 2.1.3이 필요합니다.\n\nMicrosoft 공식 설치 관리자를 다운로드해 설치합니다. 지금 설치하시겠습니까?",
            "필수 구성 요소 설치",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (consent != DialogResult.Yes)
        {
            throw new InstallerCanceledException("Windows App SDK Runtime 2.1.3 설치가 취소되었습니다.");
        }

        InstallWithMicrosoftInstaller();

        if (!IsInstalled())
        {
            throw new InvalidOperationException("Windows App SDK Runtime 2.1.3 설치를 확인하지 못했습니다.");
        }
    }

    private static bool IsInstalled()
    {
        return GetInstalledVersions()
            .Any(version => version.CompareTo(InstallerConstants.WindowsAppSdkRequiredRuntimeVersion) >= 0);
    }

    private static IEnumerable<Version> GetInstalledVersions()
    {
        foreach (var version in GetInstalledVersions(includeAllUsers: true))
        {
            yield return version;
        }

        foreach (var version in GetInstalledVersions(includeAllUsers: false))
        {
            yield return version;
        }
    }

    private static IEnumerable<Version> GetInstalledVersions(bool includeAllUsers)
    {
        var powershellPath = ProcessLauncher.FindExecutable("powershell.exe");
        if (powershellPath is null)
        {
            InstallerLog.Info("powershell.exe was not found. Cannot query Windows App SDK Runtime packages.");
            return [];
        }

        var allUsersArgument = includeAllUsers ? " -AllUsers" : string.Empty;
        var command =
            "$ErrorActionPreference='Stop'; " +
            $"Get-AppxPackage -Name '{InstallerConstants.WindowsAppSdkRuntimePackageName}'{allUsersArgument} | " +
            "ForEach-Object { $_.Version.ToString() }";

        try
        {
            var result = ProcessLauncher.CaptureOutput(
                powershellPath,
                "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"");

            if (result.ExitCode != 0)
            {
                InstallerLog.Info("Windows App SDK Runtime package query failed: " + result.StandardError.Trim());
                return [];
            }

            return result.StandardOutput
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => Version.TryParse(line, out _))
                .Select(Version.Parse)
                .ToArray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            InstallerLog.Info("Windows App SDK Runtime package query failed to start: " + ex.Message);
            return [];
        }
    }

    private static void InstallWithMicrosoftInstaller()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        var installerUri = $"https://aka.ms/windowsappsdk/2.1/{InstallerConstants.WindowsAppSdkRuntimeInstallerVersion}/windowsappruntimeinstall-{architecture}.exe";
        var installerPath = Path.Combine(Path.GetTempPath(), $"windowsappruntimeinstall-{InstallerConstants.WindowsAppSdkRuntimeInstallerVersion}-{architecture}.exe");

        InstallerLog.Info("Downloading Windows App SDK Runtime 2.1.3 installer from Microsoft.");
        Downloader.Download(installerUri, installerPath);

        InstallerLog.Info("Running Windows App SDK Runtime 2.1.3 installer.");
        var exitCode = ProcessLauncher.Run(installerPath, "--quiet", allowShellExecute: true);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Windows App SDK Runtime 2.1.3 설치 관리자가 종료 코드 {exitCode}로 실패했습니다.");
        }
    }
}

internal static class SteamInstallation
{
    public static string ResolveOrPrompt(IWin32Window owner)
    {
        var detected = GetCandidateDirectories().FirstOrDefault(IsSteamDirectory);
        if (detected is not null)
        {
            InstallerLog.Info("Detected Steam directory: " + detected);
            return detected;
        }

        InstallerUi.ShowMessage(
            owner,
            "Steam 설치 경로를 찾지 못했습니다.\n\nsteam.exe가 있는 Steam 설치 폴더를 선택해 주세요.",
            "Steam 경로 선택",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        while (true)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "steam.exe가 있는 Steam 설치 폴더를 선택하세요.",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true,
            };

            InstallerUi.BringOwnerToForeground(owner);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
            {
                throw new InstallerCanceledException("Steam 설치 경로 선택이 취소되었습니다.");
            }

            if (IsSteamDirectory(dialog.SelectedPath))
            {
                InstallerLog.Info("Selected Steam directory: " + dialog.SelectedPath);
                return dialog.SelectedPath;
            }

            InstallerUi.ShowMessage(
                owner,
                "선택한 폴더에서 steam.exe를 찾을 수 없습니다. Steam 설치 폴더를 다시 선택해 주세요.",
                "Steam 경로 선택",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static bool IsSteamDirectory(string directory)
    {
        return Directory.Exists(directory) && File.Exists(Path.Combine(directory, "steam.exe"));
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        foreach (var candidate in GetRegistryCandidates())
        {
            yield return candidate;
        }

        foreach (var candidate in GetWellKnownCandidates())
        {
            yield return candidate;
        }
    }

    private static IEnumerable<string> GetRegistryCandidates()
    {
        var values = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Valve\Steam", "InstallPath"),
            (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath"),
            (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamExe"),
        };

        foreach (var (hive, view, subKey, valueName) in values)
        {
            var value = ReadRegistryString(hive, view, subKey, valueName);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Replace('/', Path.DirectorySeparatorChar);
            yield return File.Exists(normalized) ? Path.GetDirectoryName(normalized)! : normalized;
        }
    }

    private static IEnumerable<string> GetWellKnownCandidates()
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(programFilesX86, "Steam");
        yield return Path.Combine(programFiles, "Steam");
        yield return Path.Combine(localAppData, "Steam");
    }

    private static string? ReadRegistryString(RegistryHive hive, RegistryView view, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch (SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

internal sealed record MillenniumInstallation(string RootPath, string PluginsDirectory, string ConfigPath)
{
    public static MillenniumInstallation? TryDetectForCleanup(string steamDirectory)
    {
        var rootPath = Path.Combine(steamDirectory, "millennium");
        var wsockShimPath = Path.Combine(steamDirectory, "wsock32.dll");
        var millenniumLibraryPath = Path.Combine(rootPath, "lib", "millennium.dll");

        if (File.Exists(wsockShimPath) || File.Exists(millenniumLibraryPath) || Directory.Exists(rootPath))
        {
            return Create(rootPath);
        }

        return null;
    }

    public static MillenniumInstallation ResolveOrInstall(string steamDirectory, IWin32Window owner)
    {
        var existing = TryDetect(steamDirectory);
        if (existing is not null)
        {
            InstallerLog.Info("Detected Millennium root: " + existing.RootPath);
            return existing;
        }

        var consent = InstallerUi.ShowMessage(
            owner,
            "Millennium이 설치되어 있지 않습니다.\n\nSteamKOntroller-KBD 플러그인을 설치하려면 Millennium 공식 설치 프로그램을 실행해야 합니다. 지금 설치하시겠습니까?",
            "Millennium 설치",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (consent != DialogResult.Yes)
        {
            throw new InstallerCanceledException("Millennium 설치가 취소되었습니다.");
        }

        InstallWithOfficialInstaller();

        existing = TryDetect(steamDirectory);
        if (existing is not null)
        {
            InstallerLog.Info("Detected Millennium root after installation: " + existing.RootPath);
            return existing;
        }

        return PromptForRoot(owner);
    }

    private static MillenniumInstallation? TryDetect(string steamDirectory)
    {
        var rootPath = Path.Combine(steamDirectory, "millennium");
        var wsockShimPath = Path.Combine(steamDirectory, "wsock32.dll");
        var millenniumLibraryPath = Path.Combine(rootPath, "lib", "millennium.dll");

        return File.Exists(wsockShimPath) && File.Exists(millenniumLibraryPath)
            ? Create(rootPath)
            : null;
    }

    private static MillenniumInstallation PromptForRoot(IWin32Window owner)
    {
        InstallerUi.ShowMessage(
            owner,
            "Millennium 설치는 완료되었지만 설치 경로를 자동으로 확인하지 못했습니다.\n\nMillennium 루트 폴더를 선택해 주세요.",
            "Millennium 경로 선택",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        while (true)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Millennium 루트 폴더를 선택하세요.",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true,
            };

            InstallerUi.BringOwnerToForeground(owner);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
            {
                throw new InstallerCanceledException("Millennium 경로 선택이 취소되었습니다.");
            }

            if (IsMillenniumRoot(dialog.SelectedPath))
            {
                InstallerLog.Info("Selected Millennium root: " + dialog.SelectedPath);
                return Create(dialog.SelectedPath);
            }

            InstallerUi.ShowMessage(
                owner,
                "선택한 폴더에서 lib\\millennium.dll을 찾을 수 없습니다. Millennium 루트 폴더를 다시 선택해 주세요.",
                "Millennium 경로 선택",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static bool IsMillenniumRoot(string rootPath)
    {
        return Directory.Exists(rootPath)
            && File.Exists(Path.Combine(rootPath, "lib", "millennium.dll"));
    }

    private static MillenniumInstallation Create(string rootPath)
    {
        return new MillenniumInstallation(
            rootPath,
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config", "config.json"));
    }

    private static void InstallWithOfficialInstaller()
    {
        var installerPath = Path.Combine(Path.GetTempPath(), "MillenniumInstaller-Windows.exe");

        InstallerLog.Info("Downloading Millennium official installer.");
        Downloader.Download(InstallerConstants.MillenniumInstallerUri, installerPath);

        InstallerLog.Info("Running Millennium official installer.");
        var exitCode = ProcessLauncher.Run(installerPath, string.Empty, allowShellExecute: true);
        if (exitCode != 0)
        {
            InstallerLog.Info("Millennium installer exited with code " + exitCode + ".");
        }
    }
}

internal static class PluginInstaller
{
    public static void Install(MillenniumInstallation millennium)
    {
        Directory.CreateDirectory(millennium.PluginsDirectory);

        var zipPath = Path.Combine(Path.GetTempPath(), InstallerConstants.PluginId + ".zip");
        var stagingDirectory = Path.Combine(Path.GetTempPath(), InstallerConstants.PluginId + "-" + Guid.NewGuid().ToString("N"));
        var destinationDirectory = Path.Combine(millennium.PluginsDirectory, InstallerConstants.PluginId);

        try
        {
            Downloader.Download(InstallerConstants.PluginUri, zipPath);
            Directory.CreateDirectory(stagingDirectory);
            ZipFile.ExtractToDirectory(zipPath, stagingDirectory, overwriteFiles: true);

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, destinationDirectory);
            InstallerLog.Info("Installed plugin to " + destinationDirectory + ".");
        }
        finally
        {
            TryDeleteFile(zipPath);
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal static class MillenniumUninstaller
{
    public static void RemoveMillennium(string steamDirectory, MillenniumInstallation millennium)
    {
        var wsockShimPath = Path.Combine(steamDirectory, "wsock32.dll");
        var migrationTempDirectory = Path.Combine(steamDirectory, "millennium-migration-temp");

        FileSystemDeletion.DeleteFileIfExists(wsockShimPath);
        FileSystemDeletion.DeleteDirectoryIfExists(millennium.RootPath);
        FileSystemDeletion.DeleteDirectoryIfExists(migrationTempDirectory);

        InstallerLog.Info("Removed Millennium from " + millennium.RootPath + ".");
    }

    public static void RemovePluginOnly(MillenniumInstallation millennium)
    {
        var pluginDirectory = Path.Combine(millennium.PluginsDirectory, InstallerConstants.PluginId);
        FileSystemDeletion.DeleteDirectoryIfExists(pluginDirectory);
        MillenniumConfiguration.DisablePlugin(millennium.ConfigPath, InstallerConstants.PluginId);

        InstallerLog.Info("Removed plugin from " + pluginDirectory + ".");
    }
}

internal static class FileSystemDeletion
{
    public static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
        InstallerLog.Info("Deleted file " + path + ".");
    }

    public static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        ClearAttributes(path);
        Directory.Delete(path, recursive: true);
        InstallerLog.Info("Deleted directory " + path + ".");
    }

    private static void ClearAttributes(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(childDirectory, FileAttributes.Normal);
        }

        File.SetAttributes(directory, FileAttributes.Normal);
    }
}

internal static class MillenniumConfiguration
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static void EnablePlugin(string configPath, string pluginId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        var root = ReadConfig(configPath);
        var plugins = EnsureObject(root, "plugins");
        var enabledPlugins = EnsureArray(plugins, "enabledPlugins");

        if (!enabledPlugins.Any(node => StringValueEquals(node, pluginId)))
        {
            enabledPlugins.Add(pluginId);
        }

        var json = root.ToJsonString(SerializerOptions);
        File.WriteAllText(configPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        InstallerLog.Info("Updated Millennium config at " + configPath + ".");
    }

    public static void DisablePlugin(string configPath, string pluginId)
    {
        if (!File.Exists(configPath))
        {
            InstallerLog.Info("Millennium config was not found at " + configPath + ".");
            return;
        }

        var root = ReadConfig(configPath);
        if (root["plugins"] is not JsonObject plugins
            || plugins["enabledPlugins"] is not JsonArray enabledPlugins)
        {
            InstallerLog.Info("Millennium config has no enabledPlugins array at " + configPath + ".");
            return;
        }

        for (var index = enabledPlugins.Count - 1; index >= 0; index--)
        {
            if (StringValueEquals(enabledPlugins[index], pluginId))
            {
                enabledPlugins.RemoveAt(index);
            }
        }

        var json = root.ToJsonString(SerializerOptions);
        File.WriteAllText(configPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        InstallerLog.Info("Updated Millennium config at " + configPath + ".");
    }

    private static JsonObject ReadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new JsonObject();
        }

        var json = File.ReadAllText(configPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        var node = JsonNode.Parse(json);
        return node as JsonObject ?? throw new InvalidOperationException("Millennium config.json 루트가 JSON 객체가 아닙니다.");
    }

    private static JsonObject EnsureObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static JsonArray EnsureArray(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonArray existing)
        {
            return existing;
        }

        var created = new JsonArray();
        parent[propertyName] = created;
        return created;
    }

    private static bool StringValueEquals(JsonNode? node, string value)
    {
        return node is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var existing)
            && string.Equals(existing, value, StringComparison.Ordinal);
    }
}

internal static class SteamRestart
{
    public static void Restart(string steamDirectory, IWin32Window owner)
    {
        var steamExe = Path.Combine(steamDirectory, "steam.exe");
        if (!File.Exists(steamExe))
        {
            throw new FileNotFoundException("Steam 실행 파일을 찾을 수 없습니다.", steamExe);
        }

        var message = InstallerUi.ShowMessage(
            owner,
            "Millennium 플러그인 적용을 위해 Steam을 재시작합니다.\n\n진행 중인 Steam 작업을 저장한 뒤 확인을 눌러 주세요.",
            "Steam 재시작",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        if (message != DialogResult.OK)
        {
            throw new InstallerCanceledException("Steam 재시작이 취소되었습니다.");
        }

        foreach (var process in Process.GetProcessesByName("steam"))
        {
            CloseSteamProcess(process, owner);
        }

        using var _ = Process.Start(new ProcessStartInfo
        {
            FileName = steamExe,
            WorkingDirectory = steamDirectory,
            UseShellExecute = true,
        });
    }

    public static void Stop(IWin32Window owner)
    {
        var message = InstallerUi.ShowMessage(
            owner,
            "Millennium 파일 정리를 위해 Steam을 종료합니다.\n\n진행 중인 Steam 작업을 저장한 뒤 확인을 눌러 주세요.",
            "Steam 종료",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        if (message != DialogResult.OK)
        {
            throw new InstallerCanceledException("Steam 종료가 취소되었습니다.");
        }

        foreach (var process in Process.GetProcessesByName("steam"))
        {
            CloseSteamProcess(process, owner);
        }
    }

    private static void CloseSteamProcess(Process process, IWin32Window owner)
    {
        try
        {
            /*if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
            {
                process.CloseMainWindow();
            }

            if (!process.WaitForExit(milliseconds: 15000))
            {
                var consent = InstallerUi.ShowMessage(
                    owner,
                    "Steam이 자동으로 종료되지 않았습니다. 강제 종료 후 재시작하시겠습니까?",
                    "Steam 재시작",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (consent != DialogResult.Yes)
                {
                    throw new InstallerCanceledException("Steam 강제 종료가 취소되었습니다.");
                }*/

                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 10000);
            /*}*/
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal static class Downloader
{
    public static void Download(string uri, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var httpClient = new HttpClient();
        using var response = httpClient.GetAsync(uri).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var fileStream = File.Create(destinationPath);
        responseStream.CopyTo(fileStream);
    }
}

internal static class ProcessLauncher
{
    public static int Run(string fileName, string arguments, bool allowShellExecute)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = allowShellExecute,
            WindowStyle = ProcessWindowStyle.Normal,
        }) ?? throw new InvalidOperationException("프로세스를 시작하지 못했습니다: " + fileName);

        process.WaitForExit();
        InstallerLog.Info($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}.");
        return process.ExitCode;
    }

    public static (int ExitCode, string StandardOutput, string StandardError) CaptureOutput(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("프로세스를 시작하지 못했습니다: " + fileName);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        InstallerLog.Info($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}.");
        return (process.ExitCode, standardOutput, standardError);
    }

    public static string? FindExecutable(string executableName)
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pathEntry in pathEntries)
        {
            var candidate = Path.Combine(pathEntry, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var windowsAppsCandidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            executableName);

        return File.Exists(windowsAppsCandidate) ? windowsAppsCandidate : null;
    }
}

internal static class InstallerLog
{
    public static string LogPath { get; } = Path.Combine(Path.GetTempPath(), "SteamKOntroller.InstallerAgent.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(Exception exception)
    {
        Write("ERROR", exception.ToString());
    }

    private static void Write(string level, string message)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.Now:u} [{level}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
