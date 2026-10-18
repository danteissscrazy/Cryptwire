# Avisos de terceros

Lo que Cryptwire lleva dentro o usa y no es suyo, con su licencia. Las fuentes y los recursos de cada piel
tienen además su propio `LICENCIAS.md` (`themes/<piel>/LICENCIAS.md` y `src/Cryptwire.App/Fuentes/LICENCIAS.md`).

| Componente | De quién | Licencia | Cómo va |
|---|---|---|---|
| .NET 10 (runtime) y Windows Desktop (WPF y Windows Forms) | .NET Foundation y Microsoft | MIT | Dentro del instalador: la app se publica con .NET dentro. Avisos de sus propios componentes: [dotnet/runtime](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT), [dotnet/wpf](https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT) y [dotnet/winforms](https://github.com/dotnet/winforms/blob/main/THIRD-PARTY-NOTICES.TXT) |
| Microsoft.Web.WebView2 (SDK: `Microsoft.Web.WebView2.*.dll`, `WebView2Loader.dll`) | Microsoft | BSD de tres cláusulas, abajo | Dentro de la app |
| Microsoft Edge WebView2 Runtime | Microsoft | La de Microsoft para WebView2 | No va dentro. Si a tu PC le falta, el instalador ejecuta el instalador pequeño de Microsoft (`MicrosoftEdgeWebview2Setup.exe`, firmado por Microsoft), que lo descarga de Microsoft |
| Cinzel | The Cinzel Project Authors | SIL Open Font License 1.1 | Fuente de la ventana y de las pieles |
| Saira | The Saira Project Authors | SIL Open Font License 1.1 | Fuente del Relicario |
| Share Tech Mono | Carrois Type Design | SIL Open Font License 1.1 (nombre reservado: «Share») | Fuente de la ventana y del Relicario |
| Grenze Gotisch | The Grenze Gotisch Project Authors | SIL Open Font License 1.1 | Solo para dibujar la marca de la barra de título (`tools/generar-marca.ps1`) |
| Inno Setup | Jordan Russell y Martijn Laan | Inno Setup License | Compila el instalador |

WhatsApp es una marca de WhatsApp LLC. Cryptwire no tiene relación con WhatsApp ni con Meta, y no lleva
nada de WhatsApp dentro: abre WhatsApp Web, que es de WhatsApp, como lo abriría un navegador.

## Microsoft.Web.WebView2

```
Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * The name of Microsoft Corporation, or the names of its contributors
may not be used to endorse or promote products derived from this
software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## .NET

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
