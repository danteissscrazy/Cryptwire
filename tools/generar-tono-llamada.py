"""Genera themes/relicario/llamada.wav: el toque de llamada del Relicario, dos campanas graves.

Sintetizado aquí mismo, sin muestras de terceros. Dura lo justo para repetirse en bucle mientras te
llaman: dos golpes y un silencio que deja respirar antes de la siguiente vuelta.

Uso: python tools/generar-tono-llamada.py
El resultado es siempre el mismo fichero, byte a byte.
"""

import math
import struct
import wave
from pathlib import Path

RATE = 22050
LENGTH = 2.6  # segundos de cada vuelta
PEAK = 0.7  # unos -3 dBFS

# Parciales de campana de iglesia: (proporción de frecuencia, volumen, caída por segundo).
# Zumbido, fundamental, tercera menor, quinta y nominal.
PARTIALS = [(0.5, 0.45, 1.6), (1.0, 1.0, 2.2), (1.2, 0.5, 3.0), (1.5, 0.35, 3.6), (2.0, 0.6, 4.2)]
# Dos golpes: (inicio en segundos, frecuencia de la fundamental, volumen).
STRIKES = [(0.0, 392.0, 1.0), (0.55, 329.63, 0.85)]


def sample(t: float) -> float:
    value = 0.0
    for start, base, gain in STRIKES:
        local = t - start
        if local < 0:
            continue
        attack = min(1.0, local / 0.004)  # 4 ms de entrada, sin chasquido
        for ratio, volume, decay in PARTIALS:
            value += gain * volume * attack * math.exp(-decay * local) * math.sin(2 * math.pi * base * ratio * local)
    return value


def main() -> None:
    samples = [sample(i / RATE) for i in range(int(RATE * LENGTH))]
    tail = int(RATE * 0.4)  # 400 ms de salida a cero: la vuelta siguiente empieza sin chasquido
    for i in range(tail):
        samples[-tail + i] *= 1 - i / tail
    scale = PEAK / max(abs(s) for s in samples)

    out = Path(__file__).resolve().parent.parent / "themes" / "relicario" / "llamada.wav"
    with wave.open(str(out), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(b"".join(struct.pack("<h", round(s * scale * 32767)) for s in samples))
    print(f"{out.name}: {len(samples)} muestras, {out.stat().st_size} bytes")


if __name__ == "__main__":
    main()
