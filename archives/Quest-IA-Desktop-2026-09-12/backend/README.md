# Backend — chat MVP

Implementado com Node.js 22+, sem dependências externas. Execute nesta pasta:

```powershell
npm.cmd test
npm.cmd start
```

Padrão: demonstração explícita em `127.0.0.1:8787`. GET `/health` informa o provedor. POST `/api/chat` recebe `message` e `sessionId` opcional, retorna `answer`, `sessionId` e `provider`. Limites: 2000 caracteres, 16 KiB por corpo e 20 pedidos/minuto por IP. Sem memória entre pedidos; sessionId apenas correlaciona chamadas.

Para Meta, copie `.env.example` para `.env` e configure AI_PROVIDER=meta, META_CHAT_URL (endpoint HTTPS completo), META_MODEL e META_API_KEY conforme a documentação autenticada da sua conta. O adaptador usa chat-completions; esse contrato precisa de validação com a conta Meta. Não existe fallback silencioso para demonstração.

Como a chave já está disponível, uma forma mais segura de testar sem salvá-la é executar `.\Start-QuestAI.ps1 -MetaChatUrl "ENDPOINT_DA_META" -MetaModel "NOME_DO_MODELO"`; o script solicita a chave de forma protegida e a mantém apenas no processo atual. A documentação pública da Meta confirma acesso OpenAI-compatible e indica que o endpoint e o modelo são obtidos no portal autenticado.

A chave Meta fica somente no servidor. APP_TOKEN é um token separado para desenvolvimento, exigido ao escutar fora de localhost. Produção requer HTTPS e autenticação por usuário. Mensagens não são persistidas nem registradas pelo servidor; no modo Meta são enviadas ao provedor.

## Teste no Quest via USB

1. Inicie o backend.
2. Com depuração USB autorizada, execute `adb reverse tcp:8787 tcp:8787`.
3. Instale o APK de desenvolvimento e ative o assistente.
4. Digite e envie uma pergunta; valide o teclado e interação no aparelho.

Configure URL e token de desenvolvimento em `quest-app/Assets/Resources/QuestChatConfig.json` antes do build. Nunca coloque a chave Meta nesse arquivo. Para uso sem USB, configure HTTPS acessível ao headset.

Build Unity: método `QuestAIChatBuild.Build`; saída `builds/QuestAI-chat-dev.apk`. Usa cenas existentes. HTTP é permitido somente em desenvolvimento e o cliente limita HTTP a loopback.
