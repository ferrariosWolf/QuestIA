// Decide, por perfil (ou VOICE_MODE explícito), qual STT/LLM/TTS usar para
// uma conversa de voz. Fallbacks reais (local indisponível -> cloud) ficam
// nos routers específicos (SpeechToTextRouter/ProviderRouter/TextToSpeechRouter)
// — aqui só a preferência inicial é decidida.

const PROFILE_STRATEGY = {
  FULL: { stt: "local", llmComplexity: "low", llmMode: "auto", tts: "local" },
  BALANCED: { stt: "local", llmComplexity: "low", llmMode: "auto", tts: "local" },
  LOW_VRAM: { stt: "local", llmComplexity: "low", llmMode: "auto", tts: "local" },
  PCVR: { stt: "groq", llmComplexity: "high", llmMode: "auto", tts: "local" },
};

export class VoiceRouter {
  constructor({ profile, voiceMode }) {
    this.profile = profile;
    this.voiceMode = voiceMode; // auto | local | groq | gemini_live
  }

  /** @returns {{stt: "local"|"groq"|"gemini_live", llmTask: object, llmForceMode?: string, tts: "local"|"gemini_live"}} */
  decide() {
    if (this.voiceMode === "local") {
      return {
        stt: "local",
        llmTask: { modality: "text", complexity: "low", privacyPreference: "local" },
        llmForceMode: "local_only",
        tts: "local",
      };
    }

    if (this.voiceMode === "groq") {
      return {
        stt: "groq",
        llmTask: { modality: "text", complexity: "low", privacyPreference: "normal" },
        llmForceMode: "groq_only",
        tts: "local",
      };
    }

    if (this.voiceMode === "gemini_live") {
      return { stt: "gemini_live", llmTask: null, tts: "gemini_live" };
    }

    // AUTO: respeita o perfil.
    const strategy = PROFILE_STRATEGY[this.profile] ?? PROFILE_STRATEGY.FULL;
    return {
      stt: strategy.stt,
      llmTask: { modality: "text", complexity: strategy.llmComplexity, privacyPreference: "normal" },
      tts: strategy.tts,
    };
  }
}
