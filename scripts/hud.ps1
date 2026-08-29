param(
    [Parameter(Mandatory)]
    [ValidateSet('Start', 'Stop', 'Status')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'
$processName = 'CodexUsage.Desktop'
$pluginRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $pluginRoot 'src\CodexUsage.Desktop\CodexUsage.Desktop.csproj'
$outputDirectory = Join-Path $pluginRoot 'src\CodexUsage.Desktop\bin\Debug\net10.0'
$executablePath = Join-Path $outputDirectory 'CodexUsage.Desktop.exe'

function Get-HudProcess {
    Get-Process -Name $processName -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.Path -eq $executablePath
        }
        catch {
            $false
        }
    }
}

if ($Action -eq 'Status') {
    $running = @(Get-HudProcess)
    if ($running.Count -eq 0) {
        Write-Output 'Codex Usage is not running.'
    }
    else {
        Write-Output "Codex Usage is running (PID $($running[0].Id))."
    }
    exit 0
}

if ($Action -eq 'Stop') {
    $running = @(Get-HudProcess)
    if ($running.Count -eq 0) {
        Write-Output 'Codex Usage is already stopped.'
        exit 0
    }

    $running | Stop-Process
    $running | Wait-Process -Timeout 5
    Write-Output 'Codex Usage stopped.'
    exit 0
}

if (-not $IsWindows) {
    throw 'The plugin architecture supports a macOS window adapter, but the POC launcher currently implements Windows only.'
}

$running = @(Get-HudProcess)
if ($running.Count -gt 0) {
    Write-Output "Codex Usage is already running (PID $($running[0].Id))."
    exit 0
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "HUD project not found at $projectPath"
}

& dotnet build $projectPath --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Codex Usage build failed.'
}

if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "HUD executable was not produced at $executablePath"
}

$startParameters = @{
    FilePath = $executablePath
    WorkingDirectory = $outputDirectory
    WindowStyle = 'Hidden'
    PassThru = $true
}
$process = Start-Process @startParameters

Write-Output "Codex Usage started (PID $($process.Id))."
