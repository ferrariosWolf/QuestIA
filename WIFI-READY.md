# Quest AI pela rede de casa

## Usar sem USB

1. No PC, dê dois cliques no atalho **Iniciar Quest AI** do Desktop. Aguarde a mensagem de servidor Wi-Fi pronto.
2. Deixe o PC ligado e conectado ao roteador. Pode continuar pela Ethernet.
3. Conecte o Quest ao Wi-Fi da mesma rede e abra o aplicativo Quest AI.
4. Toque **ATIVAR VOZ**. Quando aparecer **Ouvindo**, fale normalmente.

O USB foi usado apenas para instalar esta atualização. Não é necessário durante o uso. O PC faz o reconhecimento, gera a resposta e sintetiza a voz; por isso precisa permanecer ligado, sem suspensão. Fechar a janela do inicializador não encerra os serviços já iniciados.

## Rede configurada

PC: 192.168.50.220. Quest observado: 192.168.50.176. Rede: 192.168.50.0/24.

O aplicativo procura o servidor automaticamente por UDP 8786; se não houver resposta, tenta o endereço do PC acima. Mudanças de IP funcionam pela descoberta enquanto ambos permanecerem na mesma rede. Evite rede de convidados com isolamento de dispositivos.

O firewall autoriza somente o executável Node, TCP 8787 e UDP 8786, perfil Privado, interface Ethernet e origem na sub-rede local. Não foi configurado acesso pela internet nem encaminhamento de portas no roteador. Whisper, Piper e Ollama continuam em localhost.

Se mudar o PC para Wi-Fi ou outra interface de rede, as regras precisam ser ajustadas para a nova interface. Não desative o firewall.

## Verificação

Em 13/09/2026: 10 testes passaram; build Android concluída sem erros; APK instalado. Do próprio Quest, sem adb reverse, o servidor respondeu HTTP 200 pela rede e respondeu à descoberta UDP. O teste de fala com o aplicativo depende do toque em ATIVAR VOZ no headset.

Logs do PC: C:\QuestIA\builds\voice-logs. Inicializador: C:\QuestIA\Start-QuestAI-VoiceVision.ps1.
