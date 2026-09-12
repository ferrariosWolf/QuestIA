import WebSocket from "ws";

const LIVE_WS_URL =
  "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

// Sessão de voz cloud premium (áudio->áudio) via Gemini Live. Usada como
// rota alternativa quando local não for adequado ou PCVR precisar liberar
// VRAM. Nesta versão: uma sessão por utterance (conecta, envia áudio da
// pergunta, recebe áudio/texto de resposta, fecha) — não é o duplex
// persistente completo ainda, mas já é uma chamada real funcional.
export class GeminiLiveSession {
  constructor({ apiKey, model, voice, instructions, onAudio, onText, onStatus, onError }) {
    this.apiKey = apiKey;
    this.model = model;
    this.voice = voice;
    this.instructions = instructions;
    this.onAudio = onAudio ?? (() => {});
    this.onText = onText ?? (() => {});
    this.onStatus = onStatus ?? (() => {});
    this.onError = onError ?? (() => {});
    this.socket = null;
    this.ready = false;
  }

  connect() {
    return new Promise((resolve, reject) => {
      const socket = new WebSocket(`${LIVE_WS_URL}?key=${this.apiKey}`);
      this.socket = socket;

      socket.once("open", () => {
        socket.send(
          JSON.stringify({
            setup: {
              model: `models/${this.model}`,
              generationConfig: {
                responseModalities: ["AUDIO"],
                speechConfig: {
                  voiceConfig: { prebuiltVoiceConfig: { voiceName: this.voice } },
                },
              },
              systemInstruction: this.instructions
                ? { parts: [{ text: this.instructions }] }
                : undefined,
            },
          })
        );
      });

      socket.on("message", (data) => {
        // A API Live envia JSON como frame binário (não texto) — sempre
        // parseia o payload como string independentemente do tipo de frame.
        let event;
        try {
          event = JSON.parse(data.toString("utf8"));
        } catch {
          return;
        }

        if (process.env.GEMINI_LIVE_DEBUG) console.log("[gemini-live] raw:", JSON.stringify(event).slice(0, 300));

        if (event.setupComplete) {
          this.ready = true;
          this.onStatus("connected");
          resolve();
          return;
        }

        const parts = event.serverContent?.modelTurn?.parts ?? [];
        for (const part of parts) {
          if (part.inlineData?.data) {
            this.onAudio(Buffer.from(part.inlineData.data, "base64"), part.inlineData.mimeType);
          }
          if (part.text) this.onText(part.text);
        }
        if (event.serverContent?.turnComplete) this.onStatus("turnComplete");
      });

      socket.on("error", (error) => {
        if (!this.ready) reject(error);
        this.onError(error);
      });

      socket.on("close", (code, reason) => {
        this.ready = false;
        this.onStatus("disconnected");
        if (code !== 1000) this.onError(new Error(`Gemini Live fechou: ${code} ${reason}`));
      });
    });
  }

  /** @param {Buffer} pcmBuffer @param {number} sampleRate */
  sendAudioChunk(pcmBuffer, sampleRate = 16000) {
    if (!this.ready) return;
    this.socket.send(
      JSON.stringify({
        realtimeInput: {
          audio: { mimeType: `audio/pcm;rate=${sampleRate}`, data: pcmBuffer.toString("base64") },
        },
      })
    );
  }

  /** Sinaliza fim do áudio da pergunta (a Live API detecta silêncio via VAD
   * de servidor, mas o fim explícito acelera a resposta em testes curtos). */
  endAudioStream() {
    if (!this.ready) return;
    this.socket.send(JSON.stringify({ realtimeInput: { audioStreamEnd: true } }));
  }

  /** Barge-in: interrompe a resposta em andamento. */
  interrupt() {
    if (!this.ready) return;
    this.socket.send(JSON.stringify({ clientContent: { turnComplete: false } }));
  }

  close() {
    this.ready = false;
    if (this.socket && this.socket.readyState < WebSocket.CLOSING) {
      this.socket.close(1000, "Sessão encerrada");
    }
    this.socket = null;
  }
}
