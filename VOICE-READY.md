# Voz pronta no Meta Quest 3

Base oficial: C:\QuestIA. O aplicativo atualizado está instalado no Quest conectado por USB.

## Uso

1. Execute Start-QuestAI-VoiceVision.ps1 no PC se os serviços estiverem desligados ou após reconectar o USB.
2. No Quest, toque ATIVAR VOZ. Quando aparecer Ouvindo, fale normalmente.
3. Aguarde a resposta terminar antes de fazer a próxima pergunta. DESATIVAR encerra a sessão.

O botão inicia escuta contínua durante a sessão; não é preciso dizer uma palavra de ativação. Mantenha o PC ligado e o USB conectado.

## Configuração validada

- Whisper small em português, local, porta 8788.
- Gemma 3 4B no Ollama, local, porta 11434.
- Piper pt_BR-faber-medium, local, porta 8789.
- Backend local na porta 8787, conectado ao Quest por adb reverse.
- Groq, Gemini e Muse desativados pelo inicializador. A revisão automática não autorizou ativar o envio de perguntas à Groq.
- Permissão RECORD_AUDIO concedida no headset.
- Respostas de voz também aparecem no painel. Perguntas digitadas usam a mesma conexão de voz.

O modo validado é conversa local por voz e texto. A prévia de câmera não foi validada como análise visual neste modo.

## Verificação em 13/09/2026

Oito testes existentes passaram. Build Unity concluída com zero erros e sete avisos; APK instalado com sucesso. Teste sintético PCM pelo WebSocket passou por reconhecimento, modelo e síntese local, com resposta correta para dois mais dois. Tempo total incluindo envio da fala: 6,2 s; processamento após detecção de fim da fala: 1,55 s. A fala real pelo microfone do usuário ainda depende do toque em ATIVAR VOZ e do teste no headset.

Logs: builds/voice-logs. Teste reproduzível: em backend, execute node scripts/voice-local-smoke.js com os serviços iniciados.
