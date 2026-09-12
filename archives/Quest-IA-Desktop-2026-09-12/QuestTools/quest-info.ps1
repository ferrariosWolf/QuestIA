$ErrorActionPreference = 'Stop'
$adb = Join-Path $PSScriptRoot 'platform-tools\adb.exe'
& $adb devices -l
Write-Host "Modelo: $(& $adb shell getprop ro.product.model)"
Write-Host "Horizon/Android: $(& $adb shell getprop ro.build.version.release)"
Write-Host "Build: $(& $adb shell getprop ro.build.display.id)"
Write-Host "Beat Saber instalado:"
& $adb shell dumpsys package com.beatgames.beatsaber | Select-String 'versionName=|versionCode='
