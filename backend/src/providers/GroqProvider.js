import { AIProvider, Capability } from "./AIProvider.js";

// Chamadas reais implementadas mas NÃO executadas nesta etapa sem autorização
// (seção 37 do plano). Sem GROQ_API_KEY, available=false, sem falha fatal.
export class GroqProvider extends AIProvider {
  constructor({ enabled, apiKey, textModel, sttModel }) {
    super();
    this.enabled = enabled;
    this.apiKey = apiKey;
    this.textModel = textModel;
    this.sttModel = sttModel;
  }

  get name() {
    return "groq";
  }

  async isAvailable() {
    return this.enabled && Boolean(this.apiKey);
  }

  capabilities() {
    return [Capability.TEXT, Capability.AUDIO_INPUT, Capability.CLOUD];
  }

  async sendText({ prompt, system }) {
    if (!(await this.isAvailable())) {
      throw new Error("Groq indisponível: GROQ_API_KEY não configurada.");
    }
    const messages = [];
    if (system) messages.push({ role: "system", content: system });
    messages.push({ role: "user", content: prompt });

    const response = await fetch("https://api.groq.com/openai/v1/chat/completions", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${this.apiKey}`,
      },
      body: JSON.stringify({ model: this.textModel, messages }),
    });
    if (!response.ok) {
      throw new Error(`Groq respondeu ${response.status}.`);
    }
    const data = await response.json();
    return { text: data.choices?.[0]?.message?.content ?? "", model: this.textModel };
  }

  /** @param {{audio: Buffer, filename?: string}} input */
  async sendAudio({ audio, filename = "audio.wav" }) {
    if (!(await this.isAvailable())) {
      throw new Error("Groq indisponível: GROQ_API_KEY não configurada.");
    }
    const form = new FormData();
    form.append("model", this.sttModel);
    form.append("file", new Blob([audio]), filename);

    const response = await fetch("https://api.groq.com/openai/v1/audio/transcriptions", {
      method: "POST",
      headers: { Authorization: `Bearer ${this.apiKey}` },
      body: form,
    });
    if (!response.ok) {
      throw new Error(`Groq Whisper respondeu ${response.status}.`);
    }
    const data = await response.json();
    return { text: data.text ?? "" };
  }
}
