# Actualización del servidor para jugadores de navegador

## Diagnóstico del 28 de octubre de 2026

En `https://server-us-1.littlelegion.app`, el healthcheck, el preflight CORS y la
autenticación respondieron HTTP 200. `ll_search_open` con `websocket: true`
respondió HTTP 400 (`code: 3`, `message: "Invalid request"`); la misma solicitud
sin ese campo fue aceptada y se canceló inmediatamente. No se entró en una
partida. Esto identifica un contrato publicado que rechaza la búsqueda web;
no demuestra qué SHA está desplegado.

La rama `feature/desktop-web-crossplay` ya contiene el soporte necesario en
`backend/nakama/{search.go,server.go,matchmaker.go,pool.go,schema.sql}` y en el
servidor dedicado. No retirar `websocket: true` del cliente: permite excluir
servidores que solo admiten UDP. El cliente ahora explica ese rechazo y usa WSS
para partidas de un servicio HTTPS incluso desde `http://localhost:5173`.

## Confirmar qué versión funciona antes de actualizar

El cliente de esta rama es three.js/TypeScript; no es un build Unity WebGL. El
repositorio también tiene un cliente Unity desktop nativo, que puede funcionar
por UDP sin demostrar compatibilidad con este navegador. Pedir al administrador
la URL o tipo de aplicación que funciona, su backend, la revisión desplegada y
el transporte que usa. Si ya existe otro endpoint compatible, verificarlo y
configurarlo en el port antes de concluir que hace falta un despliegue.

La prueba HTTP no inspeccionó el binario Unity instalado: no demuestra que ese
binario deba recompilarse. La compilación que puede hacer falta es la del
servidor dedicado Linux, únicamente si todavía no contiene el transporte de
esta rama. Compilar Unity para WebGL sería otro cliente y otro flujo de build.

## Entrega de fuentes

El archivo `little-legion-browser-fix.bundle` contiene la rama integrada, no
binarios Unity ni configuración privada. Requiere que el repositorio receptor
contenga el commit `4e6d8e296e74ab130d637f74209d0bfa1c0370bc`. Esa es una base
conocida del repositorio; comparar con la instalación real antes de integrar.

En un checkout de revisión del mismo repositorio, adaptar la ruta del bundle:

```sh
git bundle verify /ruta/little-legion-browser-fix.bundle
git bundle list-heads /ruta/little-legion-browser-fix.bundle
git fetch /ruta/little-legion-browser-fix.bundle refs/heads/feature/desktop-web-crossplay:refs/heads/review/browser-support
git diff --stat HEAD..review/browser-support
```

Revisar el diff completo: la entrega incluye el cliente web y el transporte del
servidor, además del backend. Si la instalación está limpia y su HEAD es ancestro
de la entrega, puede integrarse con `git merge --ff-only review/browser-support`.
Si diverge, revisar e integrar sus cambios propios antes del mantenimiento.

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

## Estado de la entrega local

Pasaron 116 pruebas unitarias, typecheck y la compilación web. También se comprobó
en un navegador sin interfaz el mensaje español, simulando la respuesta del
backend sin crear otra búsqueda pública. Las pruebas previas
de backend, transporte y partida cruzada local constan en
[validation.md](validation.md). Esta máquina no tiene Unity instalado ni acceso
administrativo al servidor público. El bundle es una entrega de fuentes para
revisión: no se verificó el binario instalado ni se hizo un despliegue o una
partida pública. La necesidad de recompilar Unity depende de esa revisión.
