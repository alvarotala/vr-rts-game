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

Para exportar una versión release identificada, lista para el paso de archivo
y firma en Xcode:

```sh
python3 app/build.py ios --release --version 0.1.0 --build-number 1
```

`--release` desactiva las opciones de desarrollo y las conexiones HTTP del
cliente; usar un backend HTTPS. `--version` acepta `X.Y.Z` y `--build-number`
un entero positivo. Los valores predeterminados siguen siendo `0.1.0` y `1`;
incrementar el número para cada subida a TestFlight. Estos argumentos también
funcionan en Android, donde no cambian el firmado de desarrollo del APK.
El recibo y el manifiesto de fuentes registran plataforma, configuración,
versión y número; la validación rechaza un recibo que no coincida.

La exportación iOS utiliza Metal y declara pantalla completa para el diseño
horizontal. Agrega a `Info.plist` la explicación del acceso a servidores de
partidas en la red local que requiere
[Apple para conexiones locales, incluidas UDP](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy).
El sistema pide permiso cuando se usa esa conexión; no habilita HTTP en release
ni cambia el equipo o perfil de firma.

Ambos destinos usan IL2CPP, URP y orientación horizontal en ambos sentidos.
`--editor` o `UNITY_EDITOR` permite indicar otra ubicación del Editor;
`--output` elige una salida dedicada (dentro del repo debe quedar bajo `Builds/`).
Logs: `TestResults/unity-mobile-android.log` y `unity-mobile-ios.log`. El comando
verifica un recibo nuevo y los archivos del destino antes de informar éxito.

## Firma, dispositivo y TestFlight

`app/ios.py` coordina la exportación Unity y la firma automática de Xcode.
Necesita macOS, Unity 6000.3.26f1 con iOS Build Support y licencia activa,
Xcode seleccionado, espacio libre para exportar/compilar y la cuenta del equipo
iniciada en Xcode. El equipo se elige siempre con `--team-id`; el script no lee
credenciales ni instala herramientas. La firma permite que Xcode actualice los
perfiles del equipo indicado. Para instalar, conectar y autorizar el iPhone,
activar Developer Mode y usar su **UDID físico**, visible en Xcode → Devices
and Simulators o `xcrun xctrace list devices`. El UUID de CoreDevice que muestra
`devicectl list devices` no sirve como destino de `xcodebuild`; no usarlo aquí.

Revisar primero los comandos sin ejecutar ninguna herramienta ni crear archivos:

```sh
python3 app/ios.py device --team-id TEAM_ID --device DEVICE_ID --version 0.1.0 --build-number 1 --plan
python3 app/ios.py testflight --team-id TEAM_ID --version 0.1.0 --build-number 1 --plan
```

`TEAM_ID` y `DEVICE_ID` son marcadores que deben reemplazarse; `DEVICE_ID` es el
UDID físico del iPhone. El plan no comprueba
la disponibilidad del Editor, la licencia, el espacio, la firma ni la cuenta.
Quitar `--plan` para ejecutar. `device` compila Release, verifica identificador,
versión, número de build y firma del equipo; luego instala y abre exclusivamente
en el dispositivo indicado. `--editor /ruta/al/Unity` permite elegir el Editor.

`testflight` genera y valida un archivo Xcode Release y exporta un **IPA local por
defecto**. La subida requiere `--upload` explícito:

```sh
python3 app/ios.py testflight --team-id TEAM_ID --version 0.1.0 --build-number 1 --upload --plan
# Revisar el plan y quitar --plan para archivar, firmar y subir con la cuenta de Xcode.
```

Cada ejecución usa una carpeta nueva bajo `Builds/Mobile/iOS-delivery/`; conserva
el proyecto exportado, DerivedData, el archivo y las opciones de exportación.
No sobrescribe entregas anteriores. El identificador continúa siendo
`app.sadmonkey.littlelegion.mobile`. El número de build debe ser positivo y
nuevo para cada entrega a App Store Connect; Xcode no lo incrementa silenciosamente.
El script valida el archivo antes de exportarlo o subirlo y verifica el IPA local
antes de informar éxito. Una subida finalizada aún requiere comprobar el
procesamiento y disponibilidad en TestFlight, los metadatos y el acceso de testers.

Las pruebas del flujo usan procesos simulados y artefactos de prueba; no firman,
instalan ni publican:

```sh
python3 scripts/check-ios-delivery.py
```

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
- Durante la partida, rodear unidades con un dedo dibuja un lazo visible; al soltar se cierra y selecciona los aliados cuyos centros quedan dentro. No hace falta activar un modo ni dibujar un círculo perfecto. Un trazo demasiado pequeño o una línea no cambia la selección.
- Dos dedos desplazan la cámara; pellizcar acerca o aleja. En preparación y replay también se puede mover la cámara con un dedo.
- Acciones del HUD: construir, producir, atacar y habilidades del catálogo compartido.
- Los destinos de construcción/ataque/habilidad se confirman con un toque; cancelar está en el HUD.
- El minimapa mueve la cámara. Los menús y acciones se activan al soltar el dedo.

Después de seleccionar, **Agrupar** guarda las unidades en el primer espacio
libre. Su icono, número y cantidad quedan visibles en el HUD; un toque vuelve
a seleccionar al grupo. Los cuatro grupos son los mismos del juego compartido,
descuentan las bajas y duran hasta terminar la partida. Guardar de nuevo la
misma selección reutiliza su icono. Si están llenos, **Agrupar** permite elegir
explícitamente cuál reemplazar o cancelar, sin descartar otro grupo automáticamente.

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
.cache/dotnet-sdk/dotnet run --project tests/MobileGroupChecks
node --test tests/WebGlClientChecks/template.test.mjs
```

Usar `dotnet` en lugar de `.cache/dotnet-sdk/dotnet` si el SDK .NET 8 está instalado
en el sistema. Estas pruebas verifican aislamiento de proyectos, versiones
fijadas, protección de cambios locales y recibos de build, el clasificador real
de gestos, la geometría del lazo, la gestión de grupos y el flujo de carga web.
No compilan ni ejecutan Unity. Para validar el producto se necesita compilar y probar en Android
e iPhone: selección y órdenes, construcción, producción, combate, menús, giro,
área segura, interrupciones, partida IA completa y partida online cruzada.
