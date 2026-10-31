# Cliente Unity WebGL, hosting y juego cruzado

La única versión de navegador de Little Legion se compila con Unity WebGL. Usa las fuentes C# originales de simulación, IA, catálogo, mapas, render, tutorial e historial, con interfaz, cámara y entrada adaptadas a desktop. El cliente anterior y su pipeline de compilación fueron retirados. El build Unity nativo de escritorio sigue disponible como herramienta de desarrollo.

## Compilar y ejecutar

Requiere Unity 6000.3.26f1 con Web Build Support, una licencia activada y Python 3.10+. No cambiar el destino del proyecto raíz de Quest; el script genera `.cache/unity-webgl` y compila ese proyecto:

```sh
python3 scripts/build-webgl.py
python3 -m http.server 4174 --bind 127.0.0.1 --directory Builds/WebGL
```

Abrir `http://localhost:4174`. Las correcciones se hacen en las fuentes originales y luego se regenera el proyecto. Requisitos, recibos de build, persistencia y verificaciones en [Unity WebGL](unity-webgl.md).

## Hosting estático con Docker

Después de completar la compilación:

```sh
python3 scripts/online-stack.py web
```

Abrir `http://localhost:8080` o el puerto elegido mediante `LL_WEB_PORT`. El helper comprueba que estén el HTML, manifiesto, recibo, loader, framework, datos y WASM antes de iniciar Docker. `Dockerfile.web` solo copia `Builds/WebGL` y sirve esos archivos mediante nginx: no instala Node, no compila otra simulación y no ejecuta Unity. La configuración está en `hosting/webgl/nginx.conf`.

También se puede empaquetar y ejecutar únicamente el hosting, sin levantar el stack online:

```sh
docker build -f Dockerfile.web -t little-legion-web:local .
docker run --rm -p 127.0.0.1:8080:8080 little-legion-web:local
```

El build usa nombres de assets con hash y descompresión de respaldo del player. nginx entrega `.unityweb` como datos binarios sin `Content-Encoding`; el loader de Unity los descomprime. HTML, loader y manifiestos se revalidan, mientras que los assets con hash pueden conservarse en caché. Una ruta inexistente devuelve 404 para que un WASM o paquete de datos faltante no se confunda con HTML.

Para publicar, transferir la carpeta completa `Builds/WebGL` o esa imagen a un hosting HTTPS. Mantener juntos los archivos de cada build y registrar el SHA de las fuentes, el diff local si existe y `source-manifest.json`. Cambiar de host, protocolo o puerto cambia el origen del navegador y puede separar los datos locales de historial y preferencias. Empaquetar o servir el cliente no actualiza el backend ni el servidor de partidas.

## Cómo funciona el juego cruzado

El servidor dedicado `NgoMatchServer` mantiene la autoridad y atiende dos transportes:

| Jugador | Transporte | Puerto por defecto |
| --- | --- | --- |
| Quest y Unity desktop nativo | NGO / Unity Transport (UDP) | 7777 directo · 7778 asignado |
| Unity WebGL | WebSocket (TCP) | 7877 directo · 7878 asignado |

Los dos asientos comparten validación del saludo, admisión mediante tickets, preparación de partida, órdenes, vistas filtradas, confirmaciones y resultado final. `BrowserMatchClient` usa `WebSocketMatchCodec`, `FrameAssembler` y el mismo protocolo C#; el pequeño puente JavaScript abre el socket del navegador. No ejecuta reglas del juego.

El cliente WebGL declara `websocket: true` al buscar una partida y utiliza `wsPort` para conectarse. El backend solo asigna parejas con navegador a servidores que anuncien ese puerto. Catálogo, mapas, reglas y protocolo deben coincidir entre todos los clientes y el servidor; cambiar el contenido requiere recompilar las plataformas afectadas. Contrato completo en [online-contract.md](online-contract.md).

### Despliegue público

El endpoint predeterminado es `https://server-us-1.littlelegion.app`. Su disponibilidad HTTPS no demuestra compatibilidad del servidor de partidas.

Una página HTTPS y las partidas asignadas por un servicio HTTPS requieren WSS, también al abrir el cliente desde `http://localhost:4174`. Publicar un proxy TLS hacia el listener de partidas, anunciar su puerto mediante `FRONTIER_ADVERTISE_WS_PORT` (`LL_PUBLIC_WS_PORT` en Compose) y usar un certificado válido para el `host` anunciado. El WebSocket de partida en `/` es independiente del WebSocket de Nakama en `/ws`. Conservar el puerto UDP usado por Quest.

El desarrollo local desde HTTP puede usar WS hacia localhost o una IP privada. Los pasos para revisar y actualizar el backend están en [soporte de jugadores de navegador](browser-matchmaking-update.md). Una build WebGL exitosa no confirma una partida pública ni crossplay con Quest.

## Controles

La interfaz desktop presenta los controles y acciones disponibles para la selección actual. Clic izquierdo selecciona, arrastrar selecciona un área, clic derecho da una orden contextual y la rueda controla el zoom. Consultar la guía y la configuración de hotkeys del propio juego para los atajos activos. Las acciones llegan al mismo `GameSession` y al mismo validador de órdenes que Quest.

## Validación

Los tests de `tests/WebGlClientChecks` comprueban el cliente C# y los puentes JavaScript. `tests/NetworkChecks`, `tests/ServerHarness` y las pruebas de Nakama conservan la cobertura del transporte y la autoridad compartidos. Las pruebas históricas del cliente retirado no acreditan este player.

Antes de distribuir una build nueva, probar su arranque, interfaz y controles, audio tras interacción, IA, tutorial, historial después de recargar y replays. Validar WS/WSS y una partida pública contra Quest por separado, usando un backend compatible y registrando la revisión desplegada.
