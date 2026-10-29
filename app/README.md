# Little Legion móvil

App nativa para teléfonos Android e iPhone/iPad, usando el mismo juego Unity
que Quest y WebGL. Comparte simulación, gráficos, IA, mapas, historial,
localización y multijugador. La interfaz de pantalla y los gestos táctiles
también se usan al abrir la versión web desde un móvil.

## Preparar

Desde la raíz del repositorio, con Python 3.10 o posterior:

```sh
python3 app/build.py android --prepare-only
python3 app/build.py ios --prepare-only
```

Se generan proyectos Unity independientes en `app/.generated/android` y
`app/.generated/ios`. Se pueden abrir con Unity **6000.3.26f1**. El generador
no descarga paquetes ni necesita Unity para preparar los archivos. Al abrir
o compilar, Unity necesita acceso al registro de paquetes y una licencia activa.

Editar las fuentes en `Assets/Frontier` de la raíz y la configuración nativa en
`app/MobileBuilder.cs`; volver a preparar después. No editar las copias dentro
de `.generated/`: el generador rechaza sobrescribir cambios de código locales
y proyectos abiertos. `app/` forma parte de este repositorio y no contiene
otro catálogo ni otra implementación del juego.

## Android

Instalar Android Build Support, SDK/NDK y OpenJDK para la versión exacta del
Editor. Para generar explícitamente el APK de desarrollo:

```sh
python3 app/build.py android
# equivalente: bash scripts/unity.sh mobile android
```

Salida: `Builds/Mobile/Android/LittleLegion.apk`, Android 8/API 26 o posterior,
ARM64 y OpenGL ES 3. El identificador es `app.sadmonkey.littlelegion.mobile`,
separado del de Quest; datos y preferencias no se migran entre ambos. Se usa
el firmado de desarrollo de Unity. No es un artefacto de publicación en tienda.

Con un teléfono conectado y depuración USB autorizada:

```sh
adb install -r Builds/Mobile/Android/LittleLegion.apk
adb shell monkey -p app.sadmonkey.littlelegion.mobile 1
```

## iOS

Instalar iOS Build Support para Unity 6000.3.26f1 y Xcode en macOS:

```sh
python3 app/build.py ios
# equivalente: bash scripts/unity.sh mobile ios
```

Salida: proyecto Xcode en `Builds/Mobile/iOS`, con destino iPhone/iPad e iOS 15+.
Elegir equipo y perfil de firma en Xcode y ejecutar en un dispositivo. Este
comando exporta el proyecto; no firma un IPA, publica en App Store ni configura
una cuenta de Apple. El nombre e identificador coinciden con Android móvil.

Ambos destinos usan IL2CPP, URP y orientación horizontal en ambos sentidos.
`--editor` o `UNITY_EDITOR` permite indicar otra ubicación del Editor;
`--output` elige una salida dedicada (dentro del repo debe quedar bajo `Builds/`).
Logs: `TestResults/unity-mobile-android.log` y `unity-mobile-ios.log`. El comando
verifica un recibo nuevo y los archivos del destino antes de informar éxito.

## Web móvil

```sh
python3 scripts/build-webgl.py
python3 -m http.server 4174 --bind 127.0.0.1 --directory Builds/WebGL
```

Es el mismo player WebGL para escritorio y teléfonos. El HUD detecta entrada
táctil, la plantilla respeta el área segura y limita la resolución de render
en pantallas de alta densidad. **Cargar juego** inicia la descarga; **Jugar**
aporta un nuevo gesto después de inicializar Unity para habilitar el audio.
Se recomienda horizontal, con opción de continuar en vertical en la web.
La pantalla completa depende del navegador. No hay service worker ni garantía
de iniciar sin conexión; las partidas contra IA no requieren el backend una vez
cargado el juego.

Unity 6.3 documenta soporte de navegadores móviles en
[Safari iOS y Chrome Android](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/intro/browsercompatibility).
Esto no reemplaza la prueba del juego en dispositivos reales: memoria, audio,
temperatura, batería y fluidez aún deben medirse. El build conserva los
[requisitos de hosting WebGL](../docs/unity-webgl.md).

## Juego y conexión

- Toque en unidad o edificio propio: seleccionar. Doble toque: seleccionar del mismo tipo.
- Con selección activa, toque en terreno, enemigo o recurso: orden contextual.
- Mantener y soltar sobre un objetivo: orden contextual explícita; permite reparar o reanudar una construcción propia con trabajadores seleccionados.
- Arrastre de un dedo: mover cámara; dos dedos: mover y pellizcar para zoom.
- **Seleccionar área** permite arrastrar un rectángulo para seleccionar varias unidades.
- Acciones del HUD: construir, producir, atacar y habilidades del catálogo compartido.
- Los destinos de construcción/ataque/habilidad se confirman con un toque; cancelar está en el HUD.
- El minimapa mueve la cámara. Los menús y acciones se activan al soltar el dedo.

Al pasar a segundo plano se guarda el registro y se pausa la simulación local.
Una partida online sigue ejecutándose en el servidor y puede desconectarse si
el sistema suspende la aplicación; no se implementó reconexión.

Android/iOS usan el transporte nativo NGO/UDP y WebGL usa WebSocket/WSS. Ambos
usan el mismo servicio Nakama y contrato de partidas. El backend público
predeterminado es HTTPS. Los builds nativos de desarrollo permiten HTTP para
pruebas LAN; hay que usar la IP del servidor, no `localhost` del teléfono.
La compatibilidad real entre Quest, móvil y navegador requiere probar los
clientes contra el mismo despliegue y versión de protocolo.

## Validación

```sh
python3 scripts/check-mobile-generator.py
python3 scripts/check-webgl-generator.py
.cache/dotnet-sdk/dotnet run --project tests/MobileInputChecks
node --test tests/WebGlClientChecks/template.test.mjs
```

Usar `dotnet` en lugar de `.cache/dotnet-sdk/dotnet` si el SDK .NET 8 está instalado
en el sistema. Estas pruebas verifican aislamiento de proyectos, versiones
fijadas, protección de cambios locales y recibos de build, el clasificador real
de gestos y el flujo de carga web. No compilan ni ejecutan Unity. Para validar el producto se necesita compilar y probar en Android
e iPhone: selección y órdenes, construcción, producción, combate, menús, giro,
área segura, interrupciones, partida IA completa y partida online cruzada.
