$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

Write-Host "1. Publishing Release binaries from src/..." -ForegroundColor Cyan
dotnet publish "$root\src\CrispMic.csproj" -c Release -r win-x64 --no-self-contained -o "$root\src\publish"

# Ensure app.ico is inside publish directory
Copy-Item "$root\src\app.ico" "$root\src\publish\app.ico" -Force

# Create dist directory
if (!(Test-Path "$root\dist")) {
    New-Item -ItemType Directory -Path "$root\dist" -Force | Out-Null
}

Write-Host "2. Creating Portable ZIP..." -ForegroundColor Cyan
$zipPath = "$root\dist\CrispMic-v1.0.1-Portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$root\src\publish\*" -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "3. Compiling Windows Installer with Inno Setup..." -ForegroundColor Cyan
$isccCmd = (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)

$isccCandidates = @(
    $isccCmd,
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Users\xande\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($isccPath) {
    & $isccPath "$root\installer\installer.iss"
    Write-Host "SUCCESS: Installer generated in $root\dist\" -ForegroundColor Green
} else {
    Write-Warning "ISCC.exe not found. Install Inno Setup 6 to compile the installer."
}

Write-Host "`nRelease artifacts available in $root\dist\:" -ForegroundColor Green
Get-ChildItem "$root\dist" | Format-Table Name, Length, LastWriteTime
