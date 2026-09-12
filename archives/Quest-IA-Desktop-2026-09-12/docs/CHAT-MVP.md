# Chat MVP

Fluxo: painel Unity → POST /api/chat → servidor Node → adaptador Meta configurável → resposta textual.

Implementado: entrada de texto, última resposta, envio único por vez, timeout, cancelamento ao desativar, mensagens de erro, mock explícito, validação e limite de requisições. Sem novas dependências npm.

Verificação: três testes Node aprovados, cobrindo contrato mock, autenticação, entrada inválida, pedido ao adaptador e ocultação de erros do provedor. Não validam a API real.

Compilação Android tentada em Unity 6000.0.66f2: bloqueada por licença do Editor ausente (exit 198). Log: unity-chat-build.log. Não foi gerado um APK novo nem validada compilação C#.

Pendências: ativar licença Unity e compilar; validar teclado, legibilidade e controles no Quest; configurar credenciais e contrato da documentação autenticada Meta e testar chamada real.

Voz, câmera, histórico, streaming e ações espaciais ficam para etapas futuras. Reconhecer um objeto em imagem não fornece automaticamente uma posição 3D para marcadores.
