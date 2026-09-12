// Não controla CUDA diretamente — usa a API do Ollama (keep_alive, /api/ps)
// para saber o que está residente e evitar carregar modelos incompatíveis
// juntos (ex: qwen3:8b + gemma3:4b nos 8GB da 3070 Ti).
export class ModelResourceManager {
  constructor({ localProvider, profile }) {
    this.localProvider = localProvider;
    this.profile = profile;
  }

  /** @returns {Promise<Array<{name: string, sizeVram: number}>>} */
  async loadedModels() {
    try {
      const response = await fetch(`${this.localProvider.baseUrl}/api/ps`, { signal: AbortSignal.timeout(2000) });
      if (!response.ok) return [];
      const data = await response.json();
      return (data.models ?? []).map((model) => ({ name: model.name, sizeVram: model.size_vram ?? 0 }));
    } catch {
      return [];
    }
  }

  /** Modelo de texto recomendado para o perfil atual. */
  preferredTextModel() {
    if (this.profile === "LOW_VRAM" || this.profile === "PCVR") {
      return this.localProvider.lightModel;
    }
    return this.localProvider.textModel;
  }

  /** Libera modelo residente (unload) definindo keep_alive=0 numa chamada vazia. */
  async unload(modelName) {
    try {
      await fetch(`${this.localProvider.baseUrl}/api/generate`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ model: modelName, keep_alive: 0 }),
      });
    } catch {
      // best-effort — se Ollama estiver fora do ar, não há o que descarregar.
    }
  }

  /** PCVR: libera todos os modelos locais conhecidos para dar VRAM ao jogo. */
  async unloadAllLocal() {
    const models = await this.loadedModels();
    await Promise.all(models.map((model) => this.unload(model.name)));
  }
}
