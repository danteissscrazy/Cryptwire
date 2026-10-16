# Cryptwire

Cliente de escritorio para Windows que viste WhatsApp Web con pieles gótico-cyberpunk, tonos de
notificación y fondos a tu elección.

> En desarrollo. Proyecto independiente, sin relación con WhatsApp ni con Meta.

## Cómo funciona

Cryptwire abre WhatsApp Web dentro de una ventana propia (WebView2, el motor de Microsoft Edge) y le
aplica una piel en CSS. No usa APIs no oficiales, no automatiza nada y no lee ni guarda tus mensajes:
el cifrado de extremo a extremo lo sigue haciendo WhatsApp Web.

## Pieles

Una piel es una carpeta con un `skin.css` y, si quiere, sus fondos y fuentes, un `tono.wav` para los
avisos y un `theme.json` con el nombre que se enseña (`{ "name": "Relicario" }`). La de serie es
**Relicario**: azul noche, plata, oro y filo de hielo.

| Para | Haz |
|---|---|
| Cambiar de piel | `F7`, o el menú de la ventana (clic derecho en la barra de título) › Piel |
| Apagar la piel (modo seguro) | `F8`, o el mismo menú. Se recuerda entre sesiones |
| Arrancar sin piel una sola vez | `Cryptwire.exe --modo-seguro` |
| Añadir o retocar una piel | Copia su carpeta en `%LOCALAPPDATA%\Cryptwire\themes`. Si se llama igual que una de la app, gana la tuya |

La piel se recarga sola al guardar cualquiera de sus ficheros, sin recargar WhatsApp. Los fondos y
las fuentes viajan incrustados en el CSS (la web solo admite imágenes y fuentes propias o
incrustadas), y una piel solo puede usar imágenes y fuentes de su propia carpeta.

Para que una piel aguante las actualizaciones de WhatsApp Web, apóyate primero en sus variables de
color (`--WDS-*`) y en atributos estables (`id`, `data-testid`, `role`, `aria-*`), y deja las clases
con nombres generados para lo que no tenga otra salida. `themes/relicario/skin.css` explica las tres
capas.

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

## Estructura

| Carpeta | Qué hay |
|---|---|
| `src/Cryptwire.App` | La ventana WPF y los adaptadores de WebView2: solo traducen eventos |
| `src/Cryptwire.Core` | La lógica sin interfaz: navegación, avisos, pieles, ajustes. Probada con tests |
| `tests/Cryptwire.Core.Tests` | Tests unitarios de `Core` (xUnit) |
| `themes/` | Las pieles: CSS más sus fondos, fuentes y tono. Ver `LICENCIAS.md` en cada una |
| `tools/` | Scripts auxiliares, como el que sintetiza el tono de prueba |
