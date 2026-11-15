param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationPath
)

$exe = (Resolve-Path -LiteralPath $ApplicationPath -ErrorAction Stop).Path
$classes = @("exefile", "Msi.Package", "batfile", "cmdfile", "Microsoft.PowerShellScript.1")
$command = '"' + $exe + '" --launch "%1"'

foreach ($class in $classes) {
    $key = "HKCU:\Software\Classes\$class\shell\RunInSandbox"
    New-Item -Path "$key\command" -Force | Out-Null
    Set-Item -Path $key -Value "Run in Windows Sandbox"
    Set-ItemProperty -Path $key -Name "Icon" -Value "$env:SystemRoot\System32\WindowsSandbox.exe"
    Set-Item -Path "$key\command" -Value $command
}

Write-Host "Context menu installed for the current user." -ForegroundColor Green
Write-Host "Application: $exe"
