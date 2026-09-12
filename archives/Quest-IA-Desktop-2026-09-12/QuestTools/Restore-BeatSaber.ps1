param(
  [string]$BackupApk = 'C:\QuestTools\Backup\BeatSaber-1.45.0-base.apk',
  [switch]$ClearAppData
)
$ErrorActionPreference = 'Stop'
$adb = Join-Path $PSScriptRoot 'platform-tools\adb.exe'
if (!(Test-Path -LiteralPath $BackupApk)) { throw "Backup APK não encontrado: $BackupApk" }
& $adb devices
if ($LASTEXITCODE -ne 0) { throw 'ADB não está disponível.' }
if ($ClearAppData) { & $adb shell pm clear com.beatgames.beatsaber }
& $adb install -r -d --no-streaming $BackupApk
if ($LASTEXITCODE -ne 0) { throw "Falha ao restaurar o APK (código $LASTEXITCODE)." }
Write-Host 'Beat Saber oficial restaurado. Conteúdo de DLCs e compras não é alterado por este script.'
