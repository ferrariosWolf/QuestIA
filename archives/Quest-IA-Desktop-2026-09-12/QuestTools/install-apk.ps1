param([Parameter(Mandatory=$true)][string]$ApkPath)
$ErrorActionPreference = 'Stop'
$adb = Join-Path $PSScriptRoot 'platform-tools\adb.exe'
if (!(Test-Path -LiteralPath $ApkPath)) { throw "APK não encontrado: $ApkPath" }
& $adb install -r -d --no-streaming $ApkPath
if ($LASTEXITCODE -ne 0) { throw "Falha no sideload (código $LASTEXITCODE)." }
