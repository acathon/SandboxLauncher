$classes = @("exefile", "Msi.Package", "batfile", "cmdfile", "Microsoft.PowerShellScript.1")

foreach ($class in $classes) {
    $key = "HKCU:\Software\Classes\$class\shell\RunInSandbox"
    if (Test-Path -LiteralPath $key) {
        Remove-Item -LiteralPath $key -Recurse -Force
    }
}

Write-Host "Sandbox Launcher context-menu entries removed." -ForegroundColor Green
