# DRUM SABER STATUS

Projeto: `C:\Users\Carlos Guido\Desktop\Quest IA\DrumSaber`
Milestone: v0.1 — protótipo jogável em desenvolvimento
Unity detectado: 6000.0.66f2 e 6000.5.10f1
Raiz oficial do projeto: `C:\QuestIA`
OpenXR/Quest: dependências ainda não configuradas neste projeto novo
Quest detectado: SIM — ADB autorizado, serial `2G97C5ZH6301Z5`
APK: `C:\QuestIA\DrumSaber\Builds\DrumSaber-v0.1.apk` (25.9 MB)
Instalado: SIM — `adb install -r` retornou `Success`
Execução: iniciou no Quest via `monkey`, sem `FATAL EXCEPTION`/`AndroidRuntime` no logcat coletado

## Verificação local

- Unity batch import/compilação: OK, sem `error CS`, `Compilation failed` ou exceções do projeto.
- Log: `unity-v01-check-2.log`
- APK/install/FPS real: ainda não verificados.

## Implementado nesta etapa

- Bootstrap de cena e bateria procedural usando primitivas.
- Mapa interno de teste a 120 BPM.
- Relógio baseado em `AudioSettings.dspTime`.
- Geração antecipada de notas, pooling simples e hit zone.
- Baquetas virtuais, velocidade/intensidade e validação de golpe.
- Leitura de posição/rotação dos nós XR LeftHand/RightHand quando o OpenXR estiver ativo, com fallback de teclado no Editor.
- Perfect/Good/Miss, combo, multiplicador e score.
- Auto Kick ON por padrão, sem exigir hit nem quebrar combo.
- Calibração persistida em `PlayerPrefs`.

## Próximos passos

1. Confirmar visualmente no headset bateria, baquetas e notas; medir FPS real.
2. Adicionar áudio interno e menu de seleção.
3. Adicionar parser modular Info.dat/dificuldade v2/v3.
4. Adicionar biblioteca local e áudio real de teste.
