import { pcm16ToWav } from "../util/wav.js";

// LocalSTTProvider fala com o serviço Python dedicado (faster-whisper) em
// C:\QuestIA\services\stt — mesmo padrão do LocalProvider falando com Ollama.
// GroqWhisperProvider fica preparado, só funciona com GROQ_API_KEY.
export class LocalSTTProvider {
  constructor({ baseUrl, language, enabled }) {
    this.baseUrl = baseUrl.replace(/\/+$/, "");
    this.language = language;
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

  /** @param {Buffer} pcmBuffer @param {number} sampleRate */
  async transcribe(pcmBuffer, sampleRate) {
    const response = await fetch(
      `${this.baseUrl}/transcribe?sampleRate=${sampleRate}&language=${this.language}`,
      { method: "POST", headers: { "Content-Type": "application/octet-stream" }, body: pcmBuffer }
    );
    if (!response.ok) {
      throw new Error(`Serviço STT local respondeu ${response.status}.`);
    }
    return response.json(); // { text, durationMs }
  }
}

export class GroqWhisperProvider {
  constructor(groqProvider) {
    this.groqProvider = groqProvider;
  }

  async isAvailable() {
    return this.groqProvider.isAvailable();
  }

  /** @param {Buffer} pcmBuffer @param {number} sampleRate */
  async transcribe(pcmBuffer, sampleRate) {
    const wav = pcm16ToWav(pcmBuffer, sampleRate);
    return this.groqProvider.sendAudio({ audio: wav, filename: "audio.wav" });
  }
}

export class SpeechToTextRouter {
  constructor({ localConfig, groqProvider }) {
    this.local = new LocalSTTProvider(localConfig);
    this.groq = new GroqWhisperProvider(groqProvider);
  }

  /**
   * @param {"local"|"groq"} preferred
   * @param {Buffer} pcmBuffer
   * @param {number} sampleRate
   */
  async transcribe(preferred, pcmBuffer, sampleRate) {
    if (preferred === "groq" && (await this.groq.isAvailable())) {
      return { ...(await this.groq.transcribe(pcmBuffer, sampleRate)), provider: "groq" };
    }
    if (await this.local.isAvailable()) {
      return { ...(await this.local.transcribe(pcmBuffer, sampleRate)), provider: "local" };
    }
    if (await this.groq.isAvailable()) {
      return { ...(await this.groq.transcribe(pcmBuffer, sampleRate)), provider: "groq" };
    }
    throw new Error("Nenhum provider de STT disponível.");
  }
}
