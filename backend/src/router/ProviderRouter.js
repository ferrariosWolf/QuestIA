import { Capability } from "../providers/AIProvider.js";

// TaskDescriptor conceitual (seção 14):
// { modality: "text"|"audio"|"vision"|"realtime",
//   complexity: "low"|"medium"|"high",
//   latencyPriority: "low"|"normal"|"high",
//   privacyPreference: "local"|"normal" }

export class ProviderRouter {
  constructor({ providers, mode, resourceManager }) {
    this.providers = providers; // { local, groq, gemini, openaiLocal }
    this.mode = mode; // auto | local_only | groq_only | gemini_only
    this.resourceManager = resourceManager;
  }

  /**
   * @param {object} task TaskDescriptor
   * @param {"auto"|"local_only"|"groq_only"|"gemini_only"} [forceMode] sobrepõe
   *   this.mode para esta chamada (usado pelo VoiceRouter em VOICE_MODE=local/groq).
   * @returns {Promise<import("../providers/AIProvider.js").AIProvider>}
   */
  async select(task, forceMode) {
    const mode = forceMode ?? this.mode;
    if (mode === "local_only") return this.#requireAvailable(this.providers.local);
    if (mode === "groq_only") return this.#requireAvailable(this.providers.groq);
    if (mode === "gemini_only") return this.#requireAvailable(this.providers.gemini);
    return this.#selectAuto(task);
  }

  async #selectAuto(task) {
    const { modality, complexity, privacyPreference } = task;

    // VISION: simples -> local (gemma3), complexa -> Gemini.
    if (modality === "vision") {
      if (complexity !== "high" && (await this.providers.local.isAvailable())) return this.providers.local;
      if (await this.providers.gemini.isAvailable()) return this.providers.gemini;
      return this.#requireAvailable(this.providers.local);
    }

    // REALTIME: só Gemini Live, quando explicitamente necessário.
    if (modality === "realtime") {
      return this.#requireAvailable(this.providers.gemini);
    }

    // TEXT (low/medium local-first; high -> Groq; privacidade força local).
    if (privacyPreference === "local") return this.#requireAvailable(this.providers.local);

    if (complexity === "high") {
      if (await this.providers.groq.isAvailable()) return this.providers.groq;
      if (await this.providers.local.isAvailable()) return this.providers.local;
      return this.#requireAvailable(this.providers.gemini);
    }

    if (await this.providers.local.isAvailable()) return this.providers.local;
    if (await this.providers.groq.isAvailable()) return this.providers.groq;
    return this.#requireAvailable(this.providers.gemini);
  }

  async #requireAvailable(provider) {
    if (!(await provider.isAvailable())) {
      throw new Error(`Provider "${provider.name}" indisponível.`);
    }
    return provider;
  }

  hasCapability(provider, capability) {
    return provider.capabilities().includes(capability);
  }
}

export { Capability };
