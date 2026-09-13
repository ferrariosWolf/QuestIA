$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $projectRoot 'builds\voice-logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$env:HOST = '0.0.0.0'
$env:LAN_DISCOVERY = 'true'
$env:PORT = '8787'
$env:VOICE_MODE = 'local'
$env:AI_ROUTING_MODE = 'local_only'
$env:GROQ_ENABLED = 'false'
$env:GEMINI_ENABLED = 'false'
$env:MUSE_CLI_ENABLED = 'false'
$env:WAKE_WORD_ENABLED = 'false'
$env:LOCAL_AI_ENABLED = 'true'
$env:OLLAMA_LIGHT_MODEL = 'gemma3:4b'
$env:OLLAMA_TEXT_MODEL = 'gemma3:4b'
$env:LOCAL_STT_ENABLED = 'true'
$env:LOCAL_TTS_ENABLED = 'true'
$env:LOCAL_STT_LANGUAGE = 'pt'
$env:HF_HUB_OFFLINE = '1'
function Start-ReadyService([string]$Name, [string]$Directory, [string]$Executable, [string[]]$Arguments, [int]$Port) {
    if (-not (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $Directory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDir "$Name.out.log") -RedirectStandardError (Join-Path $logDir "$Name.err.log") | Out-Null
    }
    $deadline = (Get-Date).AddSeconds(90)
    do {
        try {
            $health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 3
            if ($Port -eq 8787 -and $health.service -ne 'quest-ai-backend') { throw 'Outro servidor ocupa 8787.' }
            Write-Host "$Name pronto."
            return $health
        } catch { Start-Sleep -Milliseconds 500 }
    } while ((Get-Date) -lt $deadline)
    throw "$Name não iniciou. Verifique $logDir."
}
$stt = Join-Path $projectRoot 'services\stt'
$tts = Join-Path $projectRoot 'services\tts'
Start-ReadyService 'stt' $stt (Join-Path $stt '.venv\Scripts\python.exe') @('main.py') 8788 | Out-Null
Start-ReadyService 'tts' $tts (Join-Path $tts '.venv\Scripts\python.exe') @('main.py') 8789 | Out-Null
$node = (Get-Command node -ErrorAction Stop).Source
$health = Start-ReadyService 'backend' (Join-Path $projectRoot 'backend') $node @('--env-file-if-exists=.env', 'src/server.js') 8787
if (-not $health.voice.localSTT -or -not $health.voice.localTTS -or -not $health.providers.local.available -or $health.routingMode -ne 'local_only' -or $health.providers.groq.enabled -or $health.providers.gemini.enabled -or $health.providers.muse.enabled) { throw 'Configuração local incompleta. Verifique os logs antes de ativar a voz.' }
Write-Host ''
Write-Host '============================================'
Write-Host ' QUEST AI ATIVO NA REDE WI-FI'
Write-Host ' PC: 192.168.50.220  Porta: 8787'
Write-Host ' Abra o app no Quest e toque ATIVAR VOZ.'
Write-Host ' Deixe esta janela aberta enquanto usar.'
Write-Host '============================================'
Write-Host 'PC e Quest devem estar na mesma rede doméstica. O cabo USB não é necessário.'
