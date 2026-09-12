// Resample linear simples de PCM16 mono — usado para alinhar a saída de TTS
// local (Piper gera 22050Hz) com o sample rate fixo que o Unity espera para
// playback (24000Hz, ver AssistantVoiceController.cs).
export function resamplePcm16(pcmBuffer, sourceRate, targetRate) {
  if (sourceRate === targetRate || pcmBuffer.length < 2) return pcmBuffer;

  const sourceSamples = Math.floor(pcmBuffer.length / 2);
  const targetSamples = Math.max(1, Math.round((sourceSamples * targetRate) / sourceRate));
  const output = Buffer.alloc(targetSamples * 2);
  const ratio = sourceRate / targetRate;

  for (let i = 0; i < targetSamples; i++) {
    const position = i * ratio;
    const left = Math.min(Math.floor(position), sourceSamples - 1);
    const right = Math.min(left + 1, sourceSamples - 1);
    const fraction = position - left;
    const leftSample = pcmBuffer.readInt16LE(left * 2);
    const rightSample = pcmBuffer.readInt16LE(right * 2);
    const sample = Math.round(leftSample + (rightSample - leftSample) * fraction);
    output.writeInt16LE(Math.max(-32768, Math.min(32767, sample)), i * 2);
  }
  return output;
}
