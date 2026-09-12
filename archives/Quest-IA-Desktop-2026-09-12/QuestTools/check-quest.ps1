$ErrorActionPreference = 'Stop'
$adb = Join-Path $PSScriptRoot 'platform-tools\adb.exe'
& $adb kill-server | Out-Null
& $adb start-server | Out-Null
& $adb devices -l
& $adb shell getprop ro.product.model
& $adb shell getprop ro.build.version.release
