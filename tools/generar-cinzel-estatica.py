"""Genera src/Cryptwire.App/Fuentes/Cinzel-SemiBold.ttf: Cinzel fijada en peso 600.

WPF no sabe usar fuentes variables, y la ventana (avisos, ajustes) quiere la Cinzel de la piel. Se saca
una instancia estática de la Cinzel variable que ya viaja con el Relicario, sin tocar nada más que el peso
y los nombres. Cinzel no tiene nombre reservado en su licencia (SIL OFL 1.1), así que la instancia puede
seguir llamándose Cinzel; la licencia va al lado, en OFL-Cinzel.txt.

Uso: python tools/generar-cinzel-estatica.py   (necesita fontTools: pip install fonttools)
El resultado es siempre el mismo fichero, byte a byte.
"""

from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

WEIGHT = 600

root = Path(__file__).resolve().parent.parent
source = root / "themes" / "relicario" / "fuentes" / "Cinzel-VariableFont_wght.ttf"
target = root / "src" / "Cryptwire.App" / "Fuentes" / "Cinzel-SemiBold.ttf"

font = TTFont(source)
modified = font["head"].modified
static = instantiateVariableFont(font, {"wght": WEIGHT})

# Nombres a la manera de siempre (familia «Cinzel SemiBold», estilo «Regular») y tipográficos
# (familia «Cinzel», estilo «SemiBold»): WPF la encuentra como Cinzel con peso SemiBold.
names = static["name"]
for platform, encoding, language in {(n.platformID, n.platEncID, n.langID) for n in names.names}:
    for name_id, text in ((1, "Cinzel SemiBold"), (2, "Regular"), (4, "Cinzel SemiBold"),
                          (6, "Cinzel-SemiBold"), (16, "Cinzel"), (17, "SemiBold")):
        names.setName(text, name_id, platform, encoding, language)
static["OS/2"].usWeightClass = WEIGHT
static["head"].modified = modified  # sin la fecha de hoy: así sale idéntica cada vez
static.recalcTimestamp = False

static.save(target)
print(f"{target.name}: {target.stat().st_size} bytes")
