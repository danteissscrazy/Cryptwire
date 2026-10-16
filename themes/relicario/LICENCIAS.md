# Relicario: de dónde sale cada fichero

| Fichero | Origen | Licencia |
|---|---|---|
| `skin.css`, `theme.json` | Escritos para Cryptwire | La del repo |
| `adornos/*.svg` (radar, arcos, anillo, filigrana) | Dibujados para Cryptwire con un script propio | La del repo |
| `adornos/cuchillas*.svg` (el marco de los avatares) | Dibujados para Cryptwire con `tools/generar-cuchillas.py` | La del repo |
| `tono.wav` | El mismo de la piel de prueba: sintetizado con `tools/generar-tono-prueba.py`, sin muestras de terceros | La del repo |
| `llamada.wav` | Dos campanas graves para las llamadas, sintetizadas con `tools/generar-tono-llamada.py`, sin muestras de terceros | La del repo |
| `fuentes/Cinzel-VariableFont_wght.ttf` | [Cinzel](https://github.com/NDISCOVER/Cinzel), de The Cinzel Project Authors, sin modificar | SIL Open Font License 1.1, en `fuentes/OFL-Cinzel.txt` |
| `fuentes/ShareTechMono-Regular.ttf` | [Share Tech Mono](https://github.com/google/fonts/tree/main/ofl/sharetechmono), de Carrois Type Design, sin modificar | SIL Open Font License 1.1, en `fuentes/OFL-ShareTechMono.txt`. Nombre reservado: «Share» |
| `fuentes/Saira-Variable.ttf` | [Saira](https://github.com/google/fonts/tree/main/ofl/saira), de The Saira Project Authors (`Saira[wdth,wght].ttf`, solo renombrada), sin modificar | SIL Open Font License 1.1, en `fuentes/OFL-Saira.txt` |

Las fuentes van enteras y sin tocar: recortarlas a un subconjunto contaría como modificación y,
en Share Tech Mono, obligaría a cambiarle el nombre.
