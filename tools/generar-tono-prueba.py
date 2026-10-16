"""Genera themes/prueba/tono.wav: dos campanadas sintetizadas, sin muestras de terceros.

Uso: python tools/generar-tono-prueba.py
El resultado es siempre el mismo fichero, byte a byte.
"""

import math
import struct
import wave
from pathlib import Path

RATE = 44100
LENGTH = 1.4  # segundos
PEAK = 0.7  # unos -3 dBFS

# Parciales de campana: (proporción de frecuencia, volumen, caída por segundo).
PARTIALS = [(1.0, 1.0, 3.2), (2.0, 0.55, 4.5), (2.76, 0.35, 6.0), (5.4, 0.18, 9.0)]
# Dos golpes: (inicio en segundos, frecuencia base, volumen).
STRIKES = [(0.0, 659.25, 1.0), (0.18, 987.77, 0.8)]


def sample(t: float) -> float:
    value = 0.0
    for start, base, gain in STRIKES:
        local = t - start
        if local < 0:
            continue
        attack = min(1.0, local / 0.005)  # 5 ms de entrada, sin chasquido
        for ratio, volume, decay in PARTIALS:
            value += gain * volume * attack * math.exp(-decay * local) * math.sin(2 * math.pi * base * ratio * local)
    return value


def main() -> None:
    samples = [sample(i / RATE) for i in range(int(RATE * LENGTH))]
    tail = int(RATE * 0.05)  # 50 ms de salida a cero
    for i in range(tail):
        samples[-tail + i] *= 1 - i / tail
    scale = PEAK / max(abs(s) for s in samples)

    out = Path(__file__).resolve().parent.parent / "themes" / "prueba" / "tono.wav"
    with wave.open(str(out), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(b"".join(struct.pack("<h", round(s * scale * 32767)) for s in samples))
    print(f"{out.name}: {len(samples)} muestras, {out.stat().st_size} bytes")


if __name__ == "__main__":
    main()
