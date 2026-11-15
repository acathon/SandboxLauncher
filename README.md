# Sandbox Launcher (V2)

A Windows 11 desktop launcher for running selected files in Windows Sandbox with advanced configuration.

## Features

- **Drag & drop or browse** to select files (EXE, MSI, BAT, CMD, PS1)
- **Three built-in profiles**: Standard, Strict Isolation, Network Enabled
- **Advanced settings**: Custom memory limits and audio input toggle
- **Startup commands**: Run custom commands inside the sandbox after the file launches
- **Session history**: Track every launch with one-click output-folder access
- **Confirmation dialog**: Review all settings before launching
- **In-app notifications**: Success feedback via InfoBar (auto-dismisses)
- **Explorer context menu**: Right-click → "Run in Windows Sandbox"

## Requirements

- Windows 10/11 Pro, Enterprise, or Education with Windows Sandbox enabled
- .NET 8 SDK + Windows App SDK tooling (for building from source)
- Windows 10 SDK ≥ 10.0.19041

## Build

### ☁️ Cloud Build (GitHub Actions)

Push to GitHub and the workflow at `.github/workflows/build.yml` builds
automatically on `windows-latest`. Download the compiled artifact from the
**Actions → Artifacts** tab.

### 🖥️ Local Build

```powershell
dotnet restore .\SandboxLauncher\SandboxLauncher.csproj
dotnet build   .\SandboxLauncher\SandboxLauncher.csproj -c Release -p:Platform=x64
```

### Self-Contained Publish

```powershell
dotnet publish .\SandboxLauncher\SandboxLauncher.csproj `
  -c Release -r win-x64 --self-contained -p:Platform=x64 -o publish
```

## Explorer Integration

After building, run PowerShell (no admin needed):

```powershell
.\scripts\Install-ContextMenu.ps1 -ApplicationPath ".\publish\SandboxLauncher.exe"
```

Remove entries:

```powershell
.\scripts\Uninstall-ContextMenu.ps1
```

## Behavior

| Setting | Standard | Strict Isolation | Network Enabled |
|---------|----------|-----------------|-----------------|
| Networking | ✅ | ❌ | ✅ |
| Virtual GPU | ✅ | ❌ | ❌ |

All profiles map:
- Source folder → `C:\SandboxTarget` (read-only)
- Output folder → `Desktop\SandboxOutput\<name>-<timestamp>` (writable)

Advanced settings (apply to any profile):
- **Memory limit (MB)** — constrain the sandbox's RAM
- **Audio input** — pass microphone to the sandbox

## Session History

Launches are recorded in `%LOCALAPPDATA%\SandboxLauncher\history.json` (max 50
entries). Click any history entry to open its output folder.

## Important Limitations

- Windows Sandbox must be installed and supported by the host edition.
- The sandbox is disposable — save files to the output folder before closing.
- The app does **not** guarantee a file is safe.
- Explorer integration always uses the Standard profile.
- Review output files before opening them on the host.
