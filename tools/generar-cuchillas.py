"""Genera el marco de los avatares del Relicario: doce cuchillas en espiral, como una sierra.

Tres estados, cada uno en su SVG dentro de themes/relicario/adornos/:
  cuchillas.svg            chat normal: cuchillas de oro
  cuchillas-sin-leer.svg   con mensajes sin leer: cuchillas carmesí
  cuchillas-activo.svg     chat abierto: cuchillas de hielo (la piel las hace girar)

Es el mismo dibujo que los avisos de la app (ToastWindow.DrawAvatar), en un cuadro de 48 unidades.
Uso: python tools/generar-cuchillas.py   El resultado es siempre el mismo, byte a byte.
"""

import math
from pathlib import Path

SIZE = 48
CENTER = 24
RING = 17.4  # aro en el que se clavan las cuchillas, justo fuera del retrato
BASE = 17.2  # donde nace cada cuchilla
LONG, SHORT = 23.6, 21.8  # puntas: largas y cortas, alternas
BACK, FRONT, TIP = -13, 5, 9  # grados: la base va de BACK a FRONT y la punta se adelanta a TIP

GOLD, ICE, CRIMSON = "#dcb663", "#45d4ff", "#e0435a"
STATES = {
    "cuchillas.svg": (GOLD, ".9", ".55"),
    "cuchillas-sin-leer.svg": (CRIMSON, ".95", ".55"),
    "cuchillas-activo.svg": (ICE, ".9", ".9"),
}


def point(radius: float, degrees: float) -> str:
    angle = math.radians(degrees)
    return f"{CENTER + radius * math.cos(angle):.2f},{CENTER + radius * math.sin(angle):.2f}"


def blades() -> str:
    paths = []
    for i in range(12):
        a = i * 30 - 90
        tip = LONG if i % 2 == 0 else SHORT
        paths.append(f"M{point(BASE, a + BACK)} L{point(tip, a + TIP)} L{point(BASE, a + FRONT)} Z")
    return " ".join(paths)


def svg(color: str, blade_opacity: str, ice_opacity: str) -> str:
    return (
        f"<svg xmlns='http://www.w3.org/2000/svg' width='{SIZE}' height='{SIZE}' viewBox='0 0 {SIZE} {SIZE}'>"
        f"<path d='{blades()}' fill='{color}' fill-opacity='{blade_opacity}'/>"
        f"<circle cx='{CENTER}' cy='{CENTER}' r='{RING}' fill='none' stroke='{color}' stroke-width='1.2'/>"
        f"<circle cx='{CENTER}' cy='{CENTER}' r='{RING - 1.3:.1f}' fill='none' stroke='{ICE}' stroke-opacity='{ice_opacity}' stroke-width='.7'/>"
        "</svg>\n"
    )


def main() -> None:
    folder = Path(__file__).resolve().parent.parent / "themes" / "relicario" / "adornos"
    for name, (color, blade_opacity, ice_opacity) in STATES.items():
        target = folder / name
        target.write_text(svg(color, blade_opacity, ice_opacity), encoding="utf-8", newline="\n")
        print(f"{name}: {target.stat().st_size} bytes")


if __name__ == "__main__":
    main()
