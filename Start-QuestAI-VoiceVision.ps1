$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendDir = Join-Path $projectRoot 'backend'
$sttDir = Join-Path $projectRoot 'services\stt'
$ttsDir = Join-Path $projectRoot 'services\tts'

function Start-LocalService([string]$Name, [string]$WorkingDirectory, [int]$Port) {
    $listening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($listening) {
        Write-Host "$Name já está disponível na porta $Port."
        return
    }
    $python = Join-Path $WorkingDirectory '.venv\Scripts\python.exe'
    if (-not (Test-Path -LiteralPath $python)) { throw "Python do serviço $Name não encontrado: $python" }
    Start-Process -FilePath $python -ArgumentList 'main.py' -WorkingDirectory $WorkingDirectory -WindowStyle Hidden
    Write-Host "Iniciando $Name na porta $Port..."
}

Start-LocalService 'reconhecimento de voz' $sttDir 8788
Start-LocalService 'voz sintetizada' $ttsDir 8789

$adb = 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
if (Test-Path -LiteralPath $adb) {
    & $adb start-server | Out-Null
    $questConnected = (& $adb devices) -match '\tdevice$'
    if ($questConnected) {
        & $adb reverse tcp:8787 tcp:8787 | Out-Null
        Write-Host 'Meta Quest conectado: túnel USB configurado.'
    } else {
        Write-Warning 'Meta Quest não detectado por USB. Conecte-o e execute este script novamente.'
    }
}

$env:MUSE_CLI_ENABLED = 'true'
$env:VOICE_MODE = 'auto'
$env:WAKE_WORD_ENABLED = 'false'
$env:PORT = '8787'
Set-Location $backendDir
Write-Host 'Quest AI voz + visão: ws://127.0.0.1:8787'
& node --env-file-if-exists=.env src/server.js
