"""Quest AI - servico local de STT (faster-whisper).

Roda dedicado em processo Python separado do backend Node (mesmo padrao do
Ollama): expoe HTTP local, backend so faz fetch. Audio fica em memoria -
nada e gravado em disco.
"""

import os
import time

import numpy as np
from fastapi import FastAPI, Request, Response
from faster_whisper import WhisperModel

MODEL_SIZE = os.environ.get("LOCAL_STT_MODEL", "small")
LANGUAGE = os.environ.get("LOCAL_STT_LANGUAGE", "pt")
_requested_device = os.environ.get("LOCAL_STT_DEVICE", "cpu").strip().lower()
PORT = int(os.environ.get("LOCAL_STT_PORT", "8788"))
TARGET_SAMPLE_RATE = 16000

# "auto" nesta versao resolve para cpu: GPU exige cuDNN/cuBLAS extras
# (nvidia-cudnn-cu12/nvidia-cublas-cu12) nao instalados por padrao e
# disputaria VRAM com qwen3:8b no perfil FULL. LOCAL_STT_DEVICE=cuda fica
# disponivel para quem instalar essas libs manualmente.
DEVICE = _requested_device if _requested_device in ("cpu", "cuda") else "cpu"
compute_type = "int8" if DEVICE == "cpu" else "int8_float16"

print(f"[stt] carregando faster-whisper model={MODEL_SIZE} device={DEVICE} compute_type={compute_type}")
_load_start = time.monotonic()
model = WhisperModel(MODEL_SIZE, device=DEVICE, compute_type=compute_type)
print(f"[stt] modelo carregado em {time.monotonic() - _load_start:.2f}s")

app = FastAPI()


def resample_pcm16_to_float32(pcm_bytes: bytes, source_rate: int, target_rate: int) -> np.ndarray:
    samples = np.frombuffer(pcm_bytes, dtype="<i2").astype(np.float32) / 32768.0
    if source_rate == target_rate or samples.size == 0:
        return samples
    duration = samples.size / source_rate
    target_length = max(1, int(round(duration * target_rate)))
    source_positions = np.arange(samples.size)
    target_positions = np.linspace(0, samples.size - 1, num=target_length)
    return np.interp(target_positions, source_positions, samples).astype(np.float32)


@app.get("/health")
def health():
    return {"ok": True, "model": MODEL_SIZE, "device": DEVICE, "computeType": compute_type, "language": LANGUAGE}


@app.post("/transcribe")
async def transcribe(request: Request):
    sample_rate = int(request.query_params.get("sampleRate", "24000"))
    language = request.query_params.get("language", LANGUAGE)
    pcm_bytes = await request.body()

    if len(pcm_bytes) < 4:
        return {"text": "", "durationMs": 0}

    started = time.monotonic()
    audio = resample_pcm16_to_float32(pcm_bytes, sample_rate, TARGET_SAMPLE_RATE)
    segments, _info = model.transcribe(audio, language=language, beam_size=1, vad_filter=False)
    text = " ".join(segment.text.strip() for segment in segments).strip()
    elapsed_ms = int((time.monotonic() - started) * 1000)

    return {"text": text, "durationMs": elapsed_ms}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=PORT)
