# Relicario: de dónde sale cada fichero

| Fichero | Origen | Licencia |
|---|---|---|
| `skin.css`, `theme.json` | Escritos para Cryptwire | La del repo |
| `adornos/*.svg` (rosetón, radar, arcos, anillo, filigrana) | Dibujados para Cryptwire con un script propio | La del repo |
| `tono.wav` | El mismo de la piel de prueba: sintetizado con `tools/generar-tono-prueba.py`, sin muestras de terceros | La del repo |
| `fuentes/Cinzel-VariableFont_wght.ttf` | [Cinzel](https://github.com/NDISCOVER/Cinzel), de The Cinzel Project Authors, sin modificar | SIL Open Font License 1.1, en `fuentes/OFL-Cinzel.txt` |
| `fuentes/ShareTechMono-Regular.ttf` | [Share Tech Mono](https://github.com/google/fonts/tree/main/ofl/sharetechmono), de Carrois Type Design, sin modificar | SIL Open Font License 1.1, en `fuentes/OFL-ShareTechMono.txt`. Nombre reservado: «Share» |

Las fuentes van enteras y sin tocar: recortarlas a un subconjunto contaría como modificación y,
en Share Tech Mono, obligaría a cambiarle el nombre.

El texto de los mensajes usa Saira si está instalada y, si no, Bahnschrift, que viene con Windows.
Ninguna de las dos viaja con la piel.
