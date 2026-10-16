# Cryptwire

Cliente de escritorio para Windows que viste WhatsApp Web con pieles gótico-cyberpunk, tonos de
notificación y fondos a tu elección.

> En desarrollo. Proyecto independiente, sin relación con WhatsApp ni con Meta.

## Cómo funciona

Cryptwire abre WhatsApp Web dentro de una ventana propia (WebView2, el motor de Microsoft Edge) y le
aplica una piel en CSS. No usa APIs no oficiales, no automatiza nada y no lee ni guarda tus mensajes:
el cifrado de extremo a extremo lo sigue haciendo WhatsApp Web.

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

La sesión de WhatsApp (el QR vinculado) se guarda en `%LOCALAPPDATA%\Cryptwire\WebView2`, nunca en
el repo. Para cerrar sesión del todo, borra esa carpeta o desvincula el dispositivo desde el móvil.

## Estructura

| Carpeta | Qué hay |
|---|---|
| `src/Cryptwire.App` | La ventana WPF y los adaptadores de WebView2: solo traducen eventos |
| `src/Cryptwire.Core` | La lógica sin interfaz: navegación, avisos, pieles. Probada con tests |
| `tests/Cryptwire.Core.Tests` | Tests unitarios de `Core` (xUnit) |
| `themes/` | Las pieles: CSS más sus fondos, fuentes y tono. Ver `LICENCIAS.md` en cada una |
| `tools/` | Scripts auxiliares, como el que sintetiza el tono de prueba |
