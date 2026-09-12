param(
  [Parameter(Mandatory=$true)][string]$MetaChatUrl,
  [Parameter(Mandatory=$true)][string]$MetaModel
)

$secureKey = Read-Host 'Cole sua chave Muse (não será salva em arquivo)' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try { $env:META_API_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
$env:AI_PROVIDER = 'meta'
$env:META_CHAT_URL = $MetaChatUrl
$env:META_MODEL = $MetaModel
$env:HOST = '127.0.0.1'
$env:PORT = '8787'
node --env-file-if-exists=.env src/server.mjs
