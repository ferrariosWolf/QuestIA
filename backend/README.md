# Backend — chat MVP

Implementado com Node.js 22+, sem dependências externas. Execute nesta pasta:

```powershell
npm.cmd test
npm.cmd start
```

Padrão: demonstração explícita em `127.0.0.1:8787`. GET `/health` informa o provedor. POST `/api/chat` recebe `message` e `sessionId` opcional, retorna `answer`, `sessionId` e `provider`. Limites: 2000 caracteres, 16 KiB por corpo e 20 pedidos/minuto por IP. Sem memória entre pedidos; sessionId apenas correlaciona chamadas.

Para usar a assinatura autenticada do Muse Code no WSL, execute `.\Start-QuestAI-MuseCode.ps1`. Esse modo chama `muse exec` sem ferramentas, escrita, shell, web ou contexto pessoal e devolve ao Quest somente o texto final. Cada mensagem inicia uma execução independente e pode levar mais tempo que a API direta.

Para Meta, o quickstart fornecido confirma `https://api.meta.ai/v1` como base URL e `muse-spark-1.3` como model ID. O backend usa Chat Completions em `https://api.meta.ai/v1/chat/completions`, formato compatível indicado pelo quickstart. Configure AI_PROVIDER=meta, META_CHAT_URL, META_MODEL e META_API_KEY. Não existe fallback silencioso para demonstração.

Como a chave já está disponível, uma forma mais segura de testar sem salvá-la é executar `.\Start-QuestAI.ps1`; o script usa os valores oficiais acima, solicita a chave de forma protegida e a mantém apenas no processo atual. Também exporta a mesma chave como `MODEL_API_KEY`, nome usado no quickstart.

A chave Meta fica somente no servidor. APP_TOKEN é um token separado para desenvolvimento, exigido ao escutar fora de localhost. Produção requer HTTPS e autenticação por usuário. Mensagens não são persistidas nem registradas pelo servidor; no modo Meta são enviadas ao provedor.

## Teste no Quest via USB

1. Inicie o backend.
2. Com depuração USB autorizada, execute `adb reverse tcp:8787 tcp:8787`.
3. Instale o APK de desenvolvimento e ative o assistente.
4. Digite e envie uma pergunta; valide o teclado e interação no aparelho.

Configure URL e token de desenvolvimento em `quest-app/Assets/Resources/QuestChatConfig.json` antes do build. Nunca coloque a chave Meta nesse arquivo. Para uso sem USB, configure HTTPS acessível ao headset.

Build Unity: método `QuestAIChatBuild.Build`; saída `builds/QuestAI-chat-dev.apk`. Usa cenas existentes. HTTP é permitido somente em desenvolvimento e o cliente limita HTTP a loopback.
