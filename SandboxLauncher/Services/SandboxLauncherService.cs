using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace SandboxLauncher.Services;

public sealed class SandboxLauncherService
{
    private const string SandboxSource = @"C:\SandboxTarget";
    private const string SandboxOutput = @"C:\Users\WDAGUtilityAccount\Desktop\HostOutput";
    private static readonly HashSet<string> SupportedExtensions =
        [".exe", ".msi", ".bat", ".cmd", ".ps1"];

    public async Task<string> LaunchAsync(string filePath, SandboxProfile profile, string? startupCommand = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Select a file to launch.");

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected file does not exist.", fullPath);

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
            throw new NotSupportedException($"The file type '{extension}' is not supported.");

        if (!IsWindowsSandboxAvailable())
            throw new InvalidOperationException(
                "Windows Sandbox is not available. Enable the Windows Sandbox optional feature and restart Windows.");

        var sourceDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Could not determine the file's directory.");

        var outputRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "SandboxOutput");
        var outputDirectory = Path.Combine(
            outputRoot,
            $"{Path.GetFileNameWithoutExtension(fullPath)}-{DateTime.Now:yyyyMMdd-HHmmss-fff}");

        Directory.CreateDirectory(outputDirectory);

        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SandboxLauncher", "Sessions");
        Directory.CreateDirectory(configDirectory);

        var configPath = Path.Combine(configDirectory, $"Sandbox-{Guid.NewGuid():N}.wsb");
        var fileName = Path.GetFileName(fullPath);
        var command = BuildCommand(extension, fileName);

        // If a startup command is provided, create a launcher script in the
        // writable output directory so it is accessible inside the sandbox.
        if (!string.IsNullOrWhiteSpace(startupCommand))
        {
            var asyncCmd = BuildAsyncLaunchCommand(extension, fileName);
            var launcherPath = Path.Combine(outputDirectory, "_launcher.cmd");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine(asyncCmd);
            sb.AppendLine("timeout /t 3 /nobreak >nul");
            sb.AppendLine(startupCommand);
            File.WriteAllText(launcherPath, sb.ToString());
            command = $"cmd.exe /k \"{SandboxOutput}\\_launcher.cmd\"";
        }

        // ── Build WSB configuration ──────────────────────────────────────
        var configElements = new List<XElement>();

        if (profile.MemoryInMB.HasValue)
            configElements.Add(new XElement("MemoryInMB", profile.MemoryInMB.Value));

        configElements.Add(new XElement("VGpu", profile.VGpu ? "Enable" : "Disable"));
        configElements.Add(new XElement("Networking", profile.Networking ? "Default" : "Disable"));

        if (profile.AudioInput)
            configElements.Add(new XElement("AudioInput", "Enable"));

        configElements.Add(new XElement("MappedFolders",
            new XElement("MappedFolder",
                new XElement("HostFolder", sourceDirectory),
                new XElement("SandboxFolder", SandboxSource),
                new XElement("ReadOnly", "true")),
            new XElement("MappedFolder",
                new XElement("HostFolder", outputDirectory),
                new XElement("SandboxFolder", SandboxOutput),
                new XElement("ReadOnly", "false"))));

        configElements.Add(new XElement("LogonCommand",
            new XElement("Command", command)));

        var configuration = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Configuration", configElements));

        configuration.Save(configPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "WindowsSandbox.exe",
            Arguments = Quote(configPath),
            UseShellExecute = true
        };

        Process.Start(startInfo);
        await Task.CompletedTask;
        return outputDirectory;
    }

    // ── Command builders ─────────────────────────────────────────────────

    /// <summary>Direct logon command (may block for interactive file types).</summary>
    private static string BuildCommand(string extension, string fileName)
    {
        var sandboxFile = $@"{SandboxSource}\{fileName}";
        var workingDirectory = Quote(SandboxSource);
        var quotedFile = Quote(sandboxFile);

        return extension switch
        {
            ".msi" => $"msiexec.exe /i {quotedFile}",
            ".ps1" => $"powershell.exe -NoProfile -ExecutionPolicy Bypass -NoExit -File {quotedFile}",
            ".bat" or ".cmd" => $"cmd.exe /k cd /d {workingDirectory} ^&^& call {Quote(fileName)}",
            _ => $"cmd.exe /c start \"\" /D {workingDirectory} {quotedFile}"
        };
    }

    /// <summary>Non-blocking launch command for use inside a launcher script.</summary>
    private static string BuildAsyncLaunchCommand(string extension, string fileName)
    {
        var sandboxFile = $@"{SandboxSource}\{fileName}";
        var quotedFile = Quote(sandboxFile);

        return extension switch
        {
            ".msi" => $"start \"Installer\" msiexec.exe /i {quotedFile}",
            ".ps1" => $"start \"PowerShell\" powershell.exe -NoProfile -ExecutionPolicy Bypass -File {quotedFile}",
            ".bat" or ".cmd" => $"start \"Script\" cmd.exe /k \"cd /d {SandboxSource} && call {fileName}\"",
            _ => $"start \"Application\" /D {Quote(SandboxSource)} {quotedFile}"
        };
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static bool IsWindowsSandboxAvailable()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return File.Exists(Path.Combine(windows, "System32", "WindowsSandbox.exe"));
    }
}
