# Builds dist\SteveVsZombies.exe: the launcher with the newest mod zip inside.
# Usage: powershell -File tools\build_launcher.ps1 -Version 0.4.5
param([Parameter(Mandatory = $true)][string]$Version)
$root = Split-Path $PSScriptRoot -Parent
$zip = Join-Path $root "dist\SteveVsZombies-$Version.zip"
if (-not (Test-Path $zip)) {
    # zip the built mod folder with forward slashes (PowerShell 5's Compress-Archive writes backslashes)
    Push-Location $root
    python -c "import zipfile,os,sys;z=zipfile.ZipFile(sys.argv[1],'w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(r,f).replace(os.sep,'/')) for r,_,fs in os.walk('SteveDaysToDie') for f in fs];z.close()" $zip
    Pop-Location
}
Copy-Item $zip (Join-Path $root "launcher\payload.zip") -Force
dotnet build (Join-Path $root "launcher\SteveVsZombiesLauncher.csproj") -c Release -nologo -v q "-p:Version=$Version" -o (Join-Path $env:TEMP "claude\svz_launcher")
if ($LASTEXITCODE -ne 0) { throw "launcher build failed" }
Copy-Item (Join-Path $env:TEMP "claude\svz_launcher\SteveVsZombies.exe") (Join-Path $root "dist\SteveVsZombies.exe") -Force
Get-Item (Join-Path $root "dist\SteveVsZombies.exe") | Select-Object Name, Length
