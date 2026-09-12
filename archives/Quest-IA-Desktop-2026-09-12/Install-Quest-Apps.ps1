[CmdletBinding()]
param(
    [string]$QuestTools = 'C:\QuestTools',
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$adbCandidates = @(
    (Get-Command adb.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $QuestTools 'platform-tools\adb.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

if (-not $adbCandidates) { throw 'adb.exe não encontrado.' }
$adb = [string]@($adbCandidates)[0]
$deviceLines = @(& $adb devices | Select-Object -Skip 1 | Where-Object { $_.Trim() })
if ($deviceLines.Count -ne 1 -or $deviceLines[0] -notmatch '\sdevice$') {
    throw "É necessário exatamente um Quest autorizado com status device. Resultado: $($deviceLines -join '; ')"
}

$model = (& $adb shell getprop ro.product.model).Trim()
$abi = (& $adb shell getprop ro.product.cpu.abi).Trim()
if ($model -ne 'Quest 3' -or $abi -ne 'arm64-v8a') {
    throw "Dispositivo inesperado: modelo=$model ABI=$abi"
}

$apkRoot = Join-Path $QuestTools 'APK'
$logRoot = Join-Path $QuestTools 'Logs'
New-Item -ItemType Directory -Path $apkRoot, $logRoot -Force | Out-Null
$log = Join-Path $logRoot ("Quest-Apps-Repeat-{0}.txt" -f (Get-Date -Format 'yyyy-MM-dd-HHmmss'))

$apps = @(
    @{ Name='Obtainium'; Package='dev.imranr.obtainium'; Pattern='Obtainium\app-arm64-v8a-release.apk' },
    @{ Name='Amaze'; Package='com.amaze.filemanager'; Pattern='Amaze\app-fdroid-release.apk' },
    @{ Name='Moonlight'; Package='com.limelight'; Pattern='Moonlight\app-nonRoot-release.apk' },
    @{ Name='RetroArch'; Package='com.retroarch.aarch64'; Pattern='RetroArch\RetroArch_aarch64-*.apk' },
    @{ Name='PPSSPP VR'; Package='org.ppsspp.ppssppvr'; Pattern='PPSSPP\ppsspp_vr-*.apk' },
    @{ Name='Dolphin'; Package='org.dolphinemu.dolphinemu'; Pattern='Dolphin\dolphin-*.apk' },
    @{ Name='QuestZDoom'; Package='com.drbeef.questzdoom'; Pattern='QuestZDoom\questzdoom-*.apk' },
    @{ Name='QuestZDoom Launcher'; Package='com.Baggyg.QuestZDoom_Launcher'; Pattern='QuestZDoom\questzdoom_launcher_*.apk' },
    @{ Name='QuestCraft'; Package='com.qcxr.qcxr'; Pattern='QuestCraft\QCXR-*.apk' },
    @{ Name='VLC'; Package='org.videolan.vlc'; Pattern='VLC\VLC-Android-*-arm64-v8a.apk' }
)

"Run: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')`r`nADB: $adb`r`nModel: $model`r`nABI: $abi" |
    Set-Content -LiteralPath $log -Encoding utf8

foreach ($app in $apps) {
    $files = @(Get-ChildItem -Path (Join-Path $apkRoot $app.Pattern) -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)
    $installed = [bool](& $adb shell pm list packages $app.Package)
    $installedVersion = if ($installed) {
        ((& $adb shell dumpsys package $app.Package | Select-String 'versionName=' | Select-Object -First 1).Line -replace '.*versionName=', '').Trim()
    } else { 'não instalado' }

    if (-not $files) {
        Add-Content -LiteralPath $log -Encoding utf8 -Value "`r`n$($app.Name): APK ausente; instalado=$installedVersion"
        continue
    }

    $apk = $files[0]
    $hash = (Get-FileHash -LiteralPath $apk.FullName -Algorithm SHA256).Hash
    $result = 'somente verificado'
    if ($Install) {
        if ($installed) {
            $result = 'já instalado; reinstalação ignorada para preservar dados'
        } else {
            $output = (& $adb install -r $apk.FullName 2>&1) -join ' '
            $result = $output
        }
    }
    Add-Content -LiteralPath $log -Encoding utf8 -Value (
        "`r`n{0}`r`nPackage={1}`r`nInstalledVersion={2}`r`nAPK={3}`r`nSHA256={4}`r`nResult={5}" -f
        $app.Name, $app.Package, $installedVersion, $apk.FullName, $hash, $result
    )
}

& $adb devices -l | Add-Content -LiteralPath $log -Encoding utf8
Write-Output "Auditoria concluída: $log"
