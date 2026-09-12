param(
  [string]$MetaChatUrl = 'https://api.meta.ai/v1/chat/completions',
  [string]$MetaModel = 'muse-spark-1.3'
)

$secureKey = Read-Host 'Cole a chave Muse e pressione Enter' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try { $env:META_API_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
$env:MODEL_API_KEY = $env:META_API_KEY
$env:AI_PROVIDER = 'meta'
$env:META_CHAT_URL = $MetaChatUrl
$env:META_MODEL = $MetaModel
$env:HOST = '127.0.0.1'
$env:PORT = '8787'
node --env-file-if-exists=.env src/server.mjs
