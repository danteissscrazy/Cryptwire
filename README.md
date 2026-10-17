# Cryptwire

Cliente de escritorio para Windows que viste WhatsApp Web con pieles gótico-cyberpunk, tonos de
notificación y fondos a tu elección.

> En desarrollo. Proyecto independiente, sin relación con WhatsApp ni con Meta.

## Cómo funciona

Cryptwire abre WhatsApp Web dentro de una ventana propia (WebView2, el motor de Microsoft Edge) y le
aplica una piel en CSS. No usa APIs no oficiales, no automatiza nada y no lee ni guarda tus mensajes:
el cifrado de extremo a extremo lo sigue haciendo WhatsApp Web.

## Instalar

Para Windows 10 (1809 o posterior) y Windows 11, de 64 bits. Ejecuta `Cryptwire-<versión>-instalador.exe`:

- Se instala solo para tu usuario, sin pedir permisos de administrador, en
  `%LOCALAPPDATA%\Programs\Cryptwire`. Lleva .NET dentro; si a tu PC le falta Microsoft Edge WebView2 (el
  motor que pinta WhatsApp Web), instala el de Microsoft.
- El instalador todavía no va firmado: la primera vez, Windows SmartScreen avisa de que es una app que no
  reconoce. Para seguir: **Más información › Ejecutar de todas formas**.
- Al abrir Cryptwire, vincúlala desde el móvil: en WhatsApp, **Dispositivos vinculados › Vincular un
  dispositivo**, y escanea el código QR. Cada PC se vincula por separado.
- Actualizar es instalar la versión nueva encima: cierra la que está abierta y no toca tus datos (sesión,
  ajustes, tonos y fondos, en `%LOCALAPPDATA%\Cryptwire`). Desinstalar (Configuración › Aplicaciones)
  pregunta si los borra también.

## Pieles

Una piel es una carpeta con un `skin.css` y, si quiere, sus fondos y fuentes, sus tonos (`tono.wav`
para los mensajes, `llamada.wav` para las llamadas) y un `theme.json` con el nombre que se enseña y los
colores de la ventana (la barra de título, los avisos, los ajustes y el menú de la bandeja):

```json
{
  "name": "Relicario",
  "ventana": { "fondo": "#02040A", "panel": "#070C19", "texto": "#EEF1F7", "suave": "#9AA3B8",
               "tenue": "#677189", "acento": "#DCB663", "brillo": "#45D4FF", "alerta": "#E0435A" }
}
```

Los colores van en `#RRGGBB` o `#AARRGGBB`; el que falte sale del Relicario, la piel de serie: azul
noche, plata, oro y filo de hielo.

| Para | Haz |
|---|---|
| Cambiar de piel | `F7`, los ajustes o el menú de la ventana (clic derecho en la barra de título) › Piel |
| Apagar la piel (modo seguro) | `F8`, los ajustes o el mismo menú. Se recuerda entre sesiones |
| Arrancar sin piel una sola vez | `Cryptwire.exe --modo-seguro` |
| Añadir o retocar una piel | Copia su carpeta en `%LOCALAPPDATA%\Cryptwire\themes`. Si se llama igual que una de la app, gana la tuya |
| Ensanchar o estrechar la lista de chats | Arrastra la línea que la separa de la conversación. Recuerda un ancho con la ventana apaisada y otro en vertical; doble clic en la línea vuelve al de WhatsApp |
| Cerrar el chat abierto | `Ctrl+W`, como en WhatsApp para Windows (en la web es `Esc`, y también vale) |

La ventana recuerda dónde la dejaste: tamaño, posición y si estaba maximizada. Si ese monitor ya no
está, Windows la trae a uno que sí. En Windows 11, al pasar el ratón por el botón de maximizar sale el
panel de diseños de ajuste, como en cualquier ventana.

La piel se recarga sola al guardar cualquiera de sus ficheros, sin recargar WhatsApp. Los fondos y
las fuentes viajan incrustados en el CSS (la web solo admite imágenes y fuentes propias o
incrustadas), y una piel solo puede usar imágenes y fuentes de su propia carpeta.

Para que una piel aguante las actualizaciones de WhatsApp Web, apóyate primero en sus variables de
color (`--WDS-*`) y en atributos estables (`id`, `data-testid`, `role`, `aria-*`), y deja las clases
con nombres generados para lo que no tenga otra salida. `themes/relicario/skin.css` explica las tres
capas.

## Avisos, tonos, fondo y bandeja

Todo se cambia en **Ajustes de Cryptwire**: el botón «Ajustes» de la barra de título, el menú de la
ventana o el icono de la bandeja. Se guarda en `%LOCALAPPDATA%\Cryptwire\settings.json`.

| Para | Haz |
|---|---|
| Un tono propio para un chat | Ajustes › Tonos por chat: escribe su nombre tal como sale en WhatsApp (o pulsa uno de los que te han avisado) y elige el tono. Vale para contactos y grupos, y para sus llamadas |
| Tono de los mensajes y de las llamadas | Ajustes › Avisos: el de la piel, ninguno o un archivo tuyo (WAV, MP3, WMA, M4A, AAC o FLAC; 10 MB como mucho). Se guarda una copia en `%LOCALAPPDATA%\Cryptwire\tonos` |
| No oír dos tonos | Apaga los de WhatsApp: en WhatsApp, Ajustes › Notificaciones › Sonidos y Tono de llamada |
| Tu fondo en la conversación | Ajustes › Fondo: se guarda una copia girada como la ves, reducida a 3840 px y sin los datos de la foto. «Oscurecer» para leer bien encima |
| Avisos en pantalla | Salen en la esquina con los colores de la piel y la foto del contacto (se pide solo a los servidores de WhatsApp; sin foto, su inicial); pulsar uno abre ese chat. No salen si estás en Cryptwire o con algo a pantalla completa. Se pueden apagar y dejar solo el tono |
| Seguir recibiendo avisos con la ventana cerrada | Cerrar la deja en la bandeja (se puede cambiar en Ajustes). Clic en el icono para volver; clic derecho › Salir para cerrar del todo. En la bandeja o minimizada, WhatsApp sigue despierto: Cryptwire le quita el freno que Chromium pone a las pestañas de fondo, así que los avisos llegan a su hora |
| Arrancar con Windows | Ajustes › Sistema. Arranca en la bandeja, sin ventana |
| Widgets | De serie, una barra fina bajo la conversación: enlace, Sigilo, Velo, sin leer y reloj. En Ajustes › Widgets, panel a la derecha o ninguno; `F9` rota entre los tres. Con la ventana estrecha (menos de 1100 px), el panel pasa a barra |
| Sigilo | Una hora sin tonos ni avisos de Cryptwire, llamadas incluidas: su conmutador en los widgets o la bandeja. El «No molestar» de Windows no se ve desde las apps; este es el de Cryptwire |
| Velo | Difumina los chats hasta pasar el ratón por encima. Es CSS en la página, así que con el modo seguro no está |
| Cerrar desde un script | `Cryptwire.exe --salir` |

Solo hay una Cryptwire abierta a la vez: volver a abrirla trae la que ya estaba. Con mensajes sin
leer, el icono de la bandeja lleva un punto y el botón de la barra de tareas, el número; es lo único
que Cryptwire lee de la página, de su título.

Las llamadas entrantes (y el aviso de prueba de WhatsApp) no salen como un mensaje normal: WhatsApp las
lanza desde su *service worker*, y WebView2 no deja interceptar esos avisos. Cryptwire pone en la
página un pequeño puente que los convierte en avisos normales, para que suenen con tu tono y salgan con
la piel; con el modo seguro el puente se quita y vuelven a salir como avisos de Windows. Pulsar uno de
esos avisos trae Cryptwire delante, donde WhatsApp enseña la llamada.

## Desarrollo

Requisitos: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download) y
[gitleaks](https://github.com/gitleaks/gitleaks).

```
git config core.hooksPath .githooks
dotnet build
dotnet test
dotnet run --project src/Cryptwire.App
```

El hook de pre-commit bloquea secretos y exige un email `noreply` de GitHub en los commits.

Para trabajar en una piel con la app en Debug, apunta `CRYPTWIRE_THEMES_DIR` a la carpeta `themes`
del repo: la app la lee por encima de las demás y cada vez que guardas ves el cambio.

La sesión de WhatsApp (el QR vinculado) se guarda en `%LOCALAPPDATA%\Cryptwire\WebView2`, nunca en
el repo. Para cerrar sesión del todo, borra esa carpeta o desvincula el dispositivo desde el móvil.

## Publicar

```
pwsh tools/publicar.ps1
```

Publica el commit, no la carpeta (el árbol de git tiene que estar limpio): lo clona en
`artifacts/fuente`, deja la app para Windows x64 con .NET dentro en `artifacts/publicar` y no la da por
buena hasta que:

1. el escáner del binario caza los datos que `tools/probar-escaner.ps1` planta a propósito (en UTF-8,
   Latin-1 y UTF-16, dentro de un `.dll` y un `.pdb` compilados sin `PathMap`…);
2. los paquetes coinciden con los `packages.lock.json`, ninguno tiene vulnerabilidades conocidas y los
   tests pasan en Release;
3. `tools/escanear-binario.ps1` no encuentra en lo publicado ni secretos, ni las rutas de tu máquina (el
   repo y tu perfil de usuario), ni lo que digan tus reglas personales.

Tus datos personales (nombre, correo, teléfono…) van como reglas de gitleaks en
`../.privado/gitleaks-personal.toml`, fuera del repo; los usan el pre-commit y el escáner. Con
`git config cryptwire.personalRules true`, si falta ese fichero los dos se niegan a seguir.

La compilación es reproducible: el mismo commit, compilado en otra carpeta, da los mismos bytes
(`Deterministic`, `PathMap`, finales de línea LF fijados en `.gitattributes`, el SDK fijado en
`global.json` y sin Source Link, que metería la dirección del remoto de cada clon).

## Instalador

```
pwsh tools/empaquetar.ps1
```

Necesita [Inno Setup](https://jrsoftware.org/isinfo.php) 6.7 o posterior. Ejecuta `publicar.ps1`, mete
dentro el instalador de WebView2 de Microsoft (lo descarga y comprueba que lo firma Microsoft; con
`-WebView2 <ruta>` usa uno que ya tengas) y compila `installer/Cryptwire.iss` dos veces: primero sin
comprimir nada, ni los ficheros ni la cabecera (textos, código y lista de ficheros), para que el escáner
lea el instalador entero, y después comprimido. El resultado queda en `artifacts/instalador`.

La CI de GitHub (`.github/workflows/ci.yml`) hace lo mismo en cada push a `main` y en cada pull request,
y deja el instalador como artefacto. Allí no hay reglas de datos personales: el escáner avisa y sigue con
las de secretos y las de rutas.

## Estructura

| Carpeta | Qué hay |
|---|---|
| `src/Cryptwire.App` | La ventana WPF, los avisos, la bandeja, los ajustes y los adaptadores de WebView2 |
| `src/Cryptwire.Core` | La lógica sin interfaz: navegación, avisos y tonos, pieles, fondos, ajustes, inicio. Probada con tests |
| `tests/Cryptwire.Core.Tests` | Tests unitarios de `Core` (xUnit) |
| `themes/` | Las pieles: CSS más sus fondos, fuentes y tonos. Ver `LICENCIAS.md` en cada una |
| `installer/` | El instalador de Inno Setup (`Cryptwire.iss`) |
| `tools/` | Scripts auxiliares: publicar, escanear lo publicado y empaquetar (`publicar.ps1`, `escanear-binario.ps1`, `probar-escaner.ps1`, `empaquetar.ps1`), los tonos (`generar-tono-prueba.py`, `generar-tono-llamada.py`), el icono (`generar-iconos.ps1`, desde `src/Cryptwire.App/Assets/logo.svg`), la marca de la barra de título (`generar-marca.ps1`) y la Cinzel fija de la ventana (`generar-cinzel-estatica.py`) |
