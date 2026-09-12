$env:AI_PROVIDER = 'muse-cli'
$env:HOST = '127.0.0.1'
$env:PORT = '8787'
$env:WSL_DISTRO = 'Ubuntu'
$env:MUSE_CLI_PATH = '/home/ferrarioswolf/.local/bin/muse'
$env:MUSE_CLI_MODEL = 'muse-spark-1.3-contributor'
node --env-file-if-exists=.env src/server.mjs
