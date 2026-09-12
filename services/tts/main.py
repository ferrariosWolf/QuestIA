"""Quest AI - servico local de TTS (Piper).

Mesmo padrao dos outros services locais: HTTP dedicado, backend Node so faz
fetch. Piper gera WAV internamente; aqui convertemos para PCM16 puro antes
de responder, para o Quest tocar direto. Nao grava audio em disco como
rotina normal (arquivo WAV fica só em memoria, via wave + BytesIO).
"""

import io
import os
import time
import wave

from fastapi import FastAPI, Query
from fastapi.responses import Response
from piper import PiperVoice

VOICE_NAME = os.environ.get("LOCAL_TTS_VOICE", "pt_BR-faber-medium")
VOICES_DIR = os.environ.get("LOCAL_TTS_VOICES_DIR", "voices")
PORT = int(os.environ.get("LOCAL_TTS_PORT", "8789"))

model_path = os.path.join(VOICES_DIR, f"{VOICE_NAME}.onnx")
print(f"[tts] carregando voz Piper: {model_path}")
_load_start = time.monotonic()
voice = PiperVoice.load(model_path)
print(f"[tts] voz carregada em {time.monotonic() - _load_start:.2f}s, sample_rate={voice.config.sample_rate}")

app = FastAPI()


@app.get("/health")
def health():
    return {"ok": True, "voice": VOICE_NAME, "sampleRate": voice.config.sample_rate}


@app.get("/synthesize")
def synthesize(text: str = Query(...)):
    started = time.monotonic()
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as wav_file:
        voice.synthesize_wav(text, wav_file)

    buffer.seek(0)
    with wave.open(buffer, "rb") as wav_file:
        pcm_bytes = wav_file.readframes(wav_file.getnframes())
        sample_rate = wav_file.getframerate()

    elapsed_ms = int((time.monotonic() - started) * 1000)
    return Response(
        content=pcm_bytes,
        media_type="application/octet-stream",
        headers={"X-Sample-Rate": str(sample_rate), "X-Duration-Ms": str(elapsed_ms)},
    )


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=PORT)
