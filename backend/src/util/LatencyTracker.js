// Instrumentação de latência do pipeline de voz (seção 18). Não expõe
// segredos — só timestamps e nomes de etapa.
export class LatencyTracker {
  constructor() {
    this.marks = new Map();
  }

  mark(name) {
    this.marks.set(name, performance.now());
  }

  deltaMs(fromMark, toMark) {
    const from = this.marks.get(fromMark);
    const to = this.marks.get(toMark);
    if (from === undefined || to === undefined) return null;
    return Math.round(to - from);
  }

  summary() {
    return {
      vadEndToSttFinalMs: this.deltaMs("vadEnd", "sttFinal"),
      sttFinalToLlmDoneMs: this.deltaMs("sttFinal", "llmDone"),
      llmDoneToTtsFirstAudioMs: this.deltaMs("llmDone", "ttsFirstAudio"),
      totalMs: this.deltaMs("vadEnd", "ttsFirstAudio"),
    };
  }
}
