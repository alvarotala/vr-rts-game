# Actualización del servidor para jugadores de navegador

## Diagnóstico histórico del 28 de octubre de 2026

En `https://server-us-1.littlelegion.app`, el healthcheck, el preflight CORS y la
autenticación respondieron HTTP 200. `ll_search_open` con `websocket: true`
respondió HTTP 400 (`code: 3`, `message: "Invalid request"`); la misma solicitud
sin ese campo fue aceptada y se canceló inmediatamente. No se entró en una
partida. Esto identifica un contrato publicado que rechaza la búsqueda web;
no demuestra qué SHA está desplegado.

La rama `feature/desktop-web-crossplay` ya contiene el soporte necesario en
`backend/nakama/{search.go,server.go,matchmaker.go,pool.go,schema.sql}` y en el
servidor dedicado. No retirar `websocket: true` del cliente: permite excluir
servidores que solo admiten UDP. El cliente Unity WebGL usa WSS para partidas
de un servicio HTTPS incluso desde `http://localhost:4174`. El diagnóstico
anterior se obtuvo con el cliente retirado; debe repetirse para confirmar el
estado del servicio desplegado, sin interpretar un healthcheck como matchmaking.

## Confirmar qué versión funciona antes de actualizar

El cliente de navegador actual es una build Unity WebGL de las fuentes C# compartidas. El repositorio también conserva el cliente Unity desktop nativo para desarrollo, que puede funcionar por UDP sin demostrar compatibilidad del navegador. Confirmar con el administrador el tipo de cliente, backend, revisión desplegada y transporte de la versión que funciona.

La prueba HTTP histórica no inspeccionó el binario Unity instalado: no demuestra que deba recompilarse. El servidor dedicado Linux necesita una nueva build únicamente si le falta el transporte compatible o cambian protocolo, reglas o contenido. Compilar el cliente WebGL mediante `scripts/build-webgl.py` genera el player del navegador y no actualiza ese servidor. El hosting de `Dockerfile.web` sirve el `Builds/WebGL` ya compilado; ver [cliente y hosting](web-client.md).

Preparar las fuentes y artefactos de una revisión identificada, revisar su diff contra la versión desplegada y conservar el SHA y cualquier corrección local. Los bundles y validaciones del port anterior son evidencia histórica y no sustituyen esa revisión.

## Pasos del administrador

1. Usar el procedimiento de mantenimiento y backup verificado de
   [deploy-production.md](deploy-production.md#7-actualizar-una-instalación-existente).
   Conservar configuración privada, overrides e imágenes anteriores.
2. Si el binario dedicado instalado no incluye el soporte WebSocket compatible,
   compilar el mismo commit con Unity 6000.3.26f1, Linux Dedicated Server Build
   Support y Unity CLI mediante `python3 scripts/build-server.py`. Verificar
   `FRONTIER_DEDICATED_BUILD passed` y `FRONTIER_SERVER_BUILT` con `backend=IL2CPP`.
   Registrar el SHA en `Builds/Server/source-commit.txt` y transferir todos los
   artefactos como indica la guía. `Dockerfile.server` no compila C#. Si el binario
   ya lo incluye y coincide el protocolo/contenido, verificar su listener y
   registro antes de decidir si hace falta reemplazarlo.
3. Publicar WSS para la partida, con proxy hacia `server:7878` en la red Compose.
   Es una conexión a `/`, distinta del WebSocket de Nakama `/ws`. Si se usa un
   puerto TLS dedicado, por ejemplo 8443, publicarlo en el proxy y firewall y
   configurar `LL_PUBLIC_WS_PORT=8443`. Mantener el puerto UDP de Quest.
4. Verificar que el certificado cubra el `host` anunciado por el servidor. El
   contrato actual comparte ese host entre UDP y WS: si se cambia una IP por un
   dominio, validar también su conexión desde Quest. Un certificado del dominio
   no basta si la conexión WSS sigue anunciando una IP distinta.
5. Con las fuentes, artefactos y configuración preparados, usar la función `dc`
   de la guía (incluye ambos Compose y el perfil `game`):

   ```sh
   dc config --quiet
   dc build migrate server
   dc run --rm --no-deps migrate
   dc up -d --no-build --no-deps --force-recreate --wait --wait-timeout 120 nakama
   dc up -d --no-build --no-deps --force-recreate server caddy
   ```

   Ejecutar cada paso únicamente si el anterior termina correctamente. Al cargar
   el módulo, Nakama agrega de forma idempotente `ll_servers.ws_port`,
   `ll_searches.websocket` y `ll_matches.websocket`. Recrear el servidor dedicado
   para que registre su endpoint con un nuevo proceso/boot ID; un simple restart
   de imágenes antiguas no incorpora los cambios.
6. Comprobar aceptación de `ll_search_open` con `websocket: true`, registro y
   asignación de un `wsPort` público, conexión WSS válida, una partida controlada
   navegador/Quest y cancelación de búsqueda. Registrar el SHA desplegado y los
   resultados; un healthcheck HTTP 200 no valida estos recorridos.

## Límites de la evidencia

El diagnóstico HTTP y las partidas del cliente anterior se conservan como registro histórico en [validation.md](validation.md). Sus pruebas unitarias, typecheck y build no validan Unity WebGL. El player actual necesita su propia compilación IL2CPP y prueba en navegador; los requisitos están en [unity-webgl.md](unity-webgl.md).

La disponibilidad HTTPS, CORS y un build local exitoso no verifican la aceptación de `websocket: true`, el `wsPort` asignado, el proxy WSS ni una partida pública contra Quest. Registrar cada comprobación y el SHA desplegado por separado.
