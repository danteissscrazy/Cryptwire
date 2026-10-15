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
```

El hook de pre-commit bloquea secretos y exige un email `noreply` de GitHub en los commits.
