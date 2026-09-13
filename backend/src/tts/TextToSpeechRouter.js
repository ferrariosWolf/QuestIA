import { resamplePcm16 } from "../util/resamplePcm16.js";

// LocalTTSProvider fala com o serviço Python dedicado (Piper) em
// C:\QuestIA\services\tts. GeminiLiveTTS depende de Gemini Live (não
// implementado — ver GeminiLiveSession). CloudTTS fica reservado.
export class LocalTTSProvider {
  constructor({ baseUrl, voice, enabled }) {
    this.baseUrl = baseUrl.replace(/\/+$/, "");
    this.voice = voice;
    this.enabled = enabled;
  }

  async isAvailable() {
    if (!this.enabled) return false;
    try {
      const response = await fetch(`${this.baseUrl}/health`, { signal: AbortSignal.timeout(1500) });
      return response.ok;
    } catch {
      return false;
    }
  }

  /** @param {string} text @returns {Promise<{pcm: Buffer, sampleRate: number}>} */
  async synthesize(text) {
    const response = await fetch(`${this.baseUrl}/synthesize?text=${encodeURIComponent(text)}`, { signal: AbortSignal.timeout(30000) });
    if (!response.ok) {
      throw new Error(`Serviço TTS local respondeu ${response.status}.`);
    }
    const sampleRate = Number.parseInt(response.headers.get("X-Sample-Rate") ?? "22050", 10);
    const pcm = Buffer.from(await response.arrayBuffer());
    return { pcm, sampleRate };
  }
}

// O Unity toca o áudio recebido num AudioClip criado a este sample rate fixo
// (ver transmissionSampleRate em AssistantVoiceController.cs). Cada provider
// de voz declara seu próprio sampleRate; aqui tudo é normalizado antes de
// sair do backend, para o Unity nunca precisar conhecer o provider.
const QUEST_OUTPUT_SAMPLE_RATE = 24000;

export class TextToSpeechRouter {
  constructor({ localConfig }) {
    this.local = new LocalTTSProvider(localConfig);
  }

  /** @param {"local"|"gemini_live"|"none"} preferred @param {string} text */
  async synthesize(preferred, text) {
    if (await this.local.isAvailable()) {
      const { pcm, sampleRate } = await this.local.synthesize(text);
      return { pcm: resamplePcm16(pcm, sampleRate, QUEST_OUTPUT_SAMPLE_RATE), provider: "local" };
    }
    // Gemini Live / cloud TTS ainda não implementados nesta versão.
    return null; // sem TTS disponível — resposta permanece só em texto.
  }
}
