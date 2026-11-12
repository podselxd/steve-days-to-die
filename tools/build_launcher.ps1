# Builds EVERYTHING for one version, in order, so nothing old can slip in:
#   1. the mod DLL (bin\Release) -> copied into SteveDaysToDie\   (forgetting this shipped old code in 0.4.7-1.0.0)
#   2. checks ModInfo.xml says this version and the DLL carries it
#   3. dist\SteveVsZombies-<ver>.zip (always rebuilt, forward slashes)
#   4. the launcher with that zip inside -> dist\SteveVsZombies.exe and dist\SteveDaysToDie-Launcher.exe
# Usage: powershell -File tools\build_launcher.ps1 -Version 1.0.1
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

# 1. the mod
dotnet build (Join-Path $root "SteveDaysToDie.csproj") -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "mod build failed" }
$dll = Join-Path $root "SteveDaysToDie\SteveDaysToDie.dll"
Copy-Item (Join-Path $root "bin\Release\SteveDaysToDie.dll") $dll -Force

# 2. the version, in ModInfo.xml and inside the DLL (Plugin.cs log line "v<ver> loading")
$info = [IO.File]::ReadAllText((Join-Path $root "SteveDaysToDie\ModInfo.xml"))
if ($info -notmatch ('Version value="' + [regex]::Escape($Version) + '"')) { throw "ModInfo.xml does not say version $Version" }
$text = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($dll))
if (-not $text.Contains("v$Version loading")) { throw "the DLL in SteveDaysToDie\ is not version $Version (Plugin.cs log line?)" }

# 3. the zip
$zip = Join-Path $root "dist\SteveVsZombies-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Push-Location $root
python -c "import zipfile,os,sys;z=zipfile.ZipFile(sys.argv[1],'w',zipfile.ZIP_DEFLATED);[z.write(os.path.join(r,f),os.path.join(r,f).replace(os.sep,'/')) for r,_,fs in os.walk('SteveDaysToDie') for f in fs];z.close()" $zip
Pop-Location

# 4. the launcher
Copy-Item $zip (Join-Path $root "launcher\payload.zip") -Force
dotnet build (Join-Path $root "launcher\SteveVsZombiesLauncher.csproj") -c Release -nologo -v q "-p:Version=$Version" -o (Join-Path $env:TEMP "claude\svz_launcher")
if ($LASTEXITCODE -ne 0) { throw "launcher build failed" }
Copy-Item (Join-Path $env:TEMP "claude\svz_launcher\SteveVsZombies.exe") (Join-Path $root "dist\SteveVsZombies.exe") -Force
Copy-Item (Join-Path $root "dist\SteveVsZombies.exe") (Join-Path $root "dist\SteveDaysToDie-Launcher.exe") -Force   # the name players see on GitHub
Get-Item $dll, $zip, (Join-Path $root "dist\SteveDaysToDie-Launcher.exe") | Select-Object Name, Length, LastWriteTime
