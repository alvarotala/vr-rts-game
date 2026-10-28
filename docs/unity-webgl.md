# Cliente Unity WebGL

Esta ruta compila el cliente C# de Unity para navegador: reutiliza simulación,
IA, interfaz, tutorial, historial, gráficos y controles desktop del juego.
El proyecto three.js de `web/` queda disponible como port anterior. No se usa
su render ni su Web Worker en el cliente Unity.

## Preparar y compilar

Requiere Python 3.10 o posterior, Unity **6000.3.26f1** con **Web Build Support**
y una licencia de Unity activada en la máquina que compila.

```sh
python3 scripts/build-webgl.py --prepare-only
bash scripts/unity.sh webgl
```

El primer comando no necesita Unity ni descarga paquetes. Genera
`.cache/unity-webgl` con fuentes compartidas y versiones del lock del proyecto.
Al compilar, Unity resuelve los paquetes públicos. Si el editor está en otra
ruta, indicar su ejecutable con `UNITY_EDITOR` o `--editor`:

```sh
python3 scripts/build-webgl.py --editor /ruta/al/ejecutable/Unity
```

El resultado esperado es `Builds/WebGL/index.html` junto con los directorios
del player y `source-manifest.json`. El comando solo informa éxito si Unity
emite un recibo nuevo con la versión exacta del Editor y están el HTML, loader,
framework, datos y WASM. Los logs quedan en
`TestResults/unity-webgl-build.log`. Un proyecto preparado no es un build.

Para probar un build terminado desde la raíz del repositorio:

```sh
python3 -m http.server 4174 --bind 127.0.0.1 --directory Builds/WebGL
```

Abrir `http://localhost:4174`. El puerto 5173 sigue correspondiendo al port
three.js. Servir siempre la carpeta del build; abrir `index.html` como archivo
local no carga el player correctamente. La plantilla admite descompresión en
el navegador y sincronización automática del almacenamiento persistente.

## Proyecto generado

Editar las fuentes de `Assets/Frontier`, `Assets/Nakama` y las plantillas de
`webgl/`, luego volver a preparar. No editar las copias de `.cache/unity-webgl`:
el generador detecta cambios en su código fuente y se detiene para preservarlos.
Los `.meta` y assets de render serializados son copias regenerables: se refrescan
desde el repositorio porque Unity puede normalizarlos al importar.
También rechaza directorios ajenos, enlaces simbólicos y un editor abierto.

El símbolo `FRONTIER_BROWSER` selecciona cámara y entrada desktop durante la
importación en el Editor y en el player. El proyecto reducido omite Meta/XR,
configuración de Quest, recursos de controladores, herramientas de Editor
nativas y procesos de servidor. Conserva URP, Input System, uGUI, NGO y Nakama
en las versiones del lock original. El build retiene los shaders que el juego
carga por nombre. No se invoca `ProjectBuilder.Configure`, que configura Quest.

`QuestBridge.Browser` mantiene la API que usa la interfaz compartida; las entradas
Touch quedan inactivas y teclado/ratón siguen usando Input System. El render
y la simulación se ejecutan en Unity. El navegador usa un solo hilo C# y los
plazos del matchmaking se verifican por frames.

## Conexión online

El cliente WebGL usa el WebSocket binario de `WebSocketMatchCodec`, el mismo
gateway de partidas de esta rama. Es distinto del transporte WebSocket de UTP.
Quest y el desktop nativo conservan su cliente NGO/UDP.

- Nakama autentica mediante `UnityWebRequest`; su adaptador JS ya está incluido.
- En WebGL, `ll_search_open` declara `websocket: true` y la conexión usa `wsPort`.
- Los hosts públicos y los servicios HTTPS requieren WSS. El desarrollo local
  por HTTP puede usar WS hacia localhost o direcciones privadas.
- La conexión directa del navegador comienza con puerto 7877; el slot asignado
  usa normalmente 7878 internamente, con su puerto WSS público anunciado.

Cambiar a Unity WebGL **no agrega soporte navegador al servidor remoto**. El
endpoint debe aceptar el contrato, asignar un servidor con WS y publicar WSS
con un certificado válido para el host anunciado. Ver el
[diagnóstico y revisión con el administrador](browser-matchmaking-update.md).

## Verificación

```sh
python3 scripts/check-webgl-generator.py
.cache/dotnet-sdk/dotnet run --project tests/WebGlClientChecks/WebGlClientChecks.csproj
node --test tests/WebGlClientChecks/bridge.test.mjs
.cache/dotnet-sdk/dotnet build tests/WebGlClientChecks/WebGlClientChecks.csproj -p:RealBrowserBridge=true
```

Los checks usan el cliente C# real con un socket controlado y prueban el puente
JavaScript por separado. El modo `RealBrowserBridge` compila las declaraciones
C# del puente. No reemplazan la compilación IL2CPP ni la ejecución real Unity.

Antes de publicar se requiere compilar y probar: arranque, audio tras interacción,
menús y ratón/teclado, partida IA, tutorial, historial después de recargar,
conexión directa WS/WSS, cancelación y una partida pública con Quest. Hasta
completar esa prueba no hay una URL Unity WebGL validada para jugar.

Referencias: [limitaciones de Unity Web](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/intro/technical-overview),
[plantilla y persistencia](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/building-distribution/templates/web-templates-build-configuration).
