// VAD local por energia RMS — leve, sem dependências, roda no PC.
// Detecta início/fim de fala em blocos PCM16 mono.
export class VoiceActivityDetector {
  constructor({ speechThreshold = 0.02, silenceHoldMs = 700 } = {}) {
    this.speechThreshold = speechThreshold;
    this.silenceHoldMs = silenceHoldMs;
    this.speaking = false;
    this.lastVoiceAt = 0;
  }

  /**
   * @param {Buffer} pcm16Frame frame PCM16 little-endian mono
   * @param {number} now epoch ms
   * @returns {"speech_started"|"speech_continues"|"speech_stopped"|"silence"}
   */
  process(pcm16Frame, now) {
    const rms = this.#rms(pcm16Frame);
    const hasVoice = rms >= this.speechThreshold;

    if (hasVoice) {
      this.lastVoiceAt = now;
      if (!this.speaking) {
        this.speaking = true;
        return "speech_started";
      }
      return "speech_continues";
    }

    if (this.speaking && now - this.lastVoiceAt >= this.silenceHoldMs) {
      this.speaking = false;
      return "speech_stopped";
    }
    return this.speaking ? "speech_continues" : "silence";
  }

  #rms(pcm16Frame) {
    if (!pcm16Frame || pcm16Frame.length < 2) return 0;
    let sumSquares = 0;
    const sampleCount = Math.floor(pcm16Frame.length / 2);
    for (let i = 0; i < sampleCount; i++) {
      const sample = pcm16Frame.readInt16LE(i * 2) / 32768;
      sumSquares += sample * sample;
    }
    return Math.sqrt(sumSquares / sampleCount);
  }
}
