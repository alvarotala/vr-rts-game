# Multiplayer público autohospedado

Nakama OSS 3.41.0 administra cuentas, matchmaking nativo por nivel y una tabla
de clasificación autoritativa. El módulo Go en `backend/nakama` asigna partidas
a un pool explícito de servidores NGO y calcula sus cambios de Elo. La
simulación continúa en el servidor Unity dedicado; no se ejecuta en Nakama.
PostgreSQL 17 conserva cuentas, búsquedas, asignaciones, admisiones, resultados
y puntuaciones.
No se necesita una cuenta de Heroic Cloud, servicios pagos ni Docker socket.

Para el servidor Ubuntu público, usar la [guía de despliegue y operación en
producción](deploy-production.md), que conserva el override HTTPS al actualizar,
incluye backups con restauración comprobada y distingue los builds de Unity de
los del backend. Los comandos del apartado siguiente son para el stack local.

## Stack local

Primero generar la configuración local con la IP de la Mac que ven los Quest:

```sh
python3 scripts/online-stack.py init --host 192.168.100.157
```

El helper crea `.cache/online/env` y `.cache/online/config.yml`, ignorados por Git,
con permisos `0600` dentro de un directorio `0700`. Genera credenciales separadas
para PostgreSQL, sesiones, refresh, tickets, consola y RPC privados. Ejecutarlo
otra vez conserva las credenciales y los datos existentes. La clave pública que
identifica la aplicación ante el SDK es `littlelegion`.

Para construir y arrancar sólo Nakama y PostgreSQL:

```sh
python3 scripts/online-stack.py backend
python3 scripts/online-stack.py status
```

Para incluir el servidor Unity, primero producir el build Linux IL2CPP actual en
`Builds/Server/FrontierServer` mediante el flujo de build del proyecto. Después:

```sh
python3 scripts/online-stack.py up
python3 scripts/online-stack.py logs
```

El helper espera el healthcheck de Nakama. El registro del servidor NGO se verifica
en su log: `FRONTIER_ALLOCATION Registered dedicated server.`

| Servicio | Acceso local | Función |
| --- | --- | --- |
| Nakama HTTP/WebSocket | `0.0.0.0:7350/tcp` | Login, cola y RPC de jugadores |
| Consola Nakama | `127.0.0.1:7351/tcp` | Administración local; usuario `admin`, contraseña en el env privado |
| PostgreSQL | Red interna Docker, sin publicación | Persistencia |
| NGO asignado | `0.0.0.0:7778/udp` | Partidas públicas |
| NGO LAN existente | `0.0.0.0:7777/udp` en su stack independiente | Conexión directa de desarrollo |

Los clientes usan `http://192.168.100.157:7350` como backend y reciben el endpoint
NGO al encontrar rival. `0.0.0.0` sólo sirve para escuchar, nunca como dirección
de conexión. Cambiar `--host` al cambiar de red y recrear el servicio `server`.

```sh
python3 scripts/online-stack.py down
```

Ese comando conserva el volumen `little-legion-online_online-postgres`. No usar
`down -v` para actualizar el stack. El servicio `migrate` ejecuta las migraciones
oficiales de Nakama; el plugin crea sus tablas `ll_*` con operaciones aditivas.
No modifica las tablas internas de cuentas de Nakama.

### Actualizar un stack existente

Actualizar fuera de una partida activa. Primero respaldar PostgreSQL y comprobar
que el respaldo se restaura en una base o instancia temporal; inspeccionar sólo
el índice del dump no demuestra una restauración. Conservar el volumen existente
y la configuración privada de `.cache/online`. Después:

```sh
docker compose --env-file .cache/online/env -f compose.online.yml stop server
python3 scripts/online-stack.py init
python3 scripts/online-stack.py backend
python3 scripts/online-stack.py up
```

El paso `init` conserva claves y host, y regenera la configuración de Nakama con
`matchmaker.rev_precision=true` y `rev_threshold=0`. Es necesario ejecutarlo al
actualizar: `up` por sí solo no regenera un archivo de configuración existente.
Las migraciones agregan tablas de ranking y columnas de compatibilidad; no
resetean cuentas, búsquedas ni resultados. Las partidas anteriores conservan
`rank_version=0`, sin puntuación retroactiva; las parejas públicas nuevas usan
`rank_version=1`. No ejecutar `down -v` ni recrear el volumen para incorporar ranking.

## Ranking y búsqueda por nivel

Todas las partidas públicas nuevas son clasificadas. El perfil comienza en
**1000 puntos Elo**, usa **K=32** y tiene mínimo **0**. El menú muestra puntos,
posición, victorias, derrotas, empates y los diez primeros. Las cuentas sin
partidas clasificadas todavía no tienen puesto en la tabla.

La ventana inicial de búsqueda es **±100 puntos**; aumenta en 100 cada 20 segundos
hasta **±400**. PostgreSQL mide la espera y ambos jugadores deben estar dentro de
la ventana del otro. El cliente no puede declarar su propio nivel ni ampliar
el rango enviando otras propiedades o consultas al matchmaker.

Sólo el resultado `finished` del servidor autenticado aplica puntos. Se guardan
el resultado y los cambios de ambos jugadores en una sola transacción; los
reintentos no puntúan otra vez. Una derrota por rendición o desconexión en partida
cuenta. Cancelaciones, no-show y abortos no cambian Elo. La proyección a la tabla
nativa `ll_ranked_1v1_v1` usa una cola persistente y escrituras absolutas; los
clientes no pueden escribir ni borrar su puntuación. Reglas, desempates y RPC
en [Ranking y rivales de nivel parecido](ranking.md).

## Contrato y seguridad

El contrato completo está en [online-contract.md](online-contract.md). Los DTO
del módulo usan camelCase. El SDK oficial controla login, refresh y WebSocket;
el emparejamiento utiliza `RegisterMatchmakerMatched` de Nakama.

- El login device usa un identificador aleatorio por instalación, guardado en
  almacenamiento privado. Es una credencial de cuenta, no un identificador de
  hardware. Reinstalar sin conservarla pierde el acceso a esa cuenta hasta que
  se agregue vinculación con otro método de login.
- Cada búsqueda pertenece al `userId` de la sesión autenticada. El before hook
  valida la búsqueda y fija la consulta de compatibilidad y rango de nivel; el
  matched hook comprueba nuevamente las identidades, sesiones, rating y ventanas
  persistidos. La antigüedad se conserva al renovar un ticket para ampliar el rango.
- Las claves y los resultados de partida nunca se aceptan de un jugador. Los
  RPC de servidor requieren el `runtime.http_key` privado y rechazan contexto
  autenticado de jugador. El registro acepta únicamente IDs habilitados en
  `LL_SERVER_IDS`; el endpoint proviene de la configuración del proceso confiable.
- El cliente recibe un ticket HMAC opaco por cuenta y partida. En PostgreSQL se
  guarda su SHA-256. Tiene vigencia hasta el plazo de llegada de la asignación,
  y sólo se consume una vez. Un retry con el mismo `requestId` devuelve la misma
  identidad; otro ID no permite reutilizarlo. El JWT de cuenta no viaja por NGO.
- Antes de empezar deben estar admitidas las dos cuentas. El evento `started`
  y la cancelación se serializan con las admisiones: si gana la cancelación no
  puede arrancar; si ya empezó, cancelar una búsqueda no aborta la partida.
- `finished` persiste resultado y tick sin liberar el servidor. Sólo `released`
  habilita otra pareja, cuando el servidor terminó de mostrar el resultado.
  `winner` usa la convención de simulación: `0`/`1` son asientos, `2` es empate;
  eventos sin resultado pueden enviar `-1` y ese valor se ignora.

Las transacciones del pool comparten un advisory lock de PostgreSQL. Es una
decisión simple para el pool inicial pequeño: hace atómicas asignación,
cancelación, admisión, inicio y liberación entre workers del backend. El plugin
no llama al transporte ni a la simulación durante una transacción.

Una búsqueda abandonada expira tras 2 minutos sin polling. Una pareja espera
como máximo 10 minutos en la cola de capacidad. Al asignarse un servidor tiene
60 segundos para llegar. Un proceso sin heartbeat durante 20 segundos pierde su
lease y su partida se aborta; un nuevo `bootId` también invalida la anterior.
La limpieza se ejecuta con cada RPC o procesamiento del matchmaker. Un pool
ocupado mantiene las parejas compatibles en `waitingServer`, por orden de llegada.

## Pruebas

La prueba de backend usa cuentas reales, el WebSocket nativo de Nakama y un agente
de servidor simulado con la clave privada local. No requiere paquetes Python.
Detener sólo el slot online durante la prueba para evitar competir por `local-1`:

```sh
docker compose --env-file .cache/online/env -f compose.online.yml stop server
python3 scripts/online-stack.py test
python3 backend/nakama/tests/ranking_integration.py
```

Cubre login persistente y refresh, aislamiento de búsquedas, suplantación de
identidad, sesiones duplicadas, dos usuarios emparejados, claims idempotentes,
replay de tickets, cola con pool ocupado, cancelación antes/después del inicio,
resultados inmutables, dos sesiones completas, reinicio y expiración de leases y
no-show. Para probar expiraciones sin esperar minutos adelanta sólo el reloj de
filas propias del fixture; no borra ni resetea datos.

La prueba de ranking agrega Elo con resultados concurrentes e idempotentes,
re-login, empates sin cambio de puntos, proyección nativa y trabajo atrasado de
la cola persistente. También rechaza escritura y borrado desde el cliente,
comprueba que cancelaciones/no-show no puntúan y verifica las ventanas mutuas
con ratings y relojes de cuentas y búsquedas creadas por la propia prueba.

Para la prueba integrada con clientes Unity reales, el override
`compose.online.smoke.yml` activa `-frontier-smoke` en el servidor NGO:

```sh
docker compose --env-file .cache/online/env -f compose.online.yml -f compose.online.smoke.yml --profile game up -d --build
python3 scripts/check-multiplayer.py --backend http://127.0.0.1:7350 --output TestResults/online-e2e
# Retrasar al segundo cliente verifica la renovación real del ticket al ampliar el rango:
python3 scripts/check-multiplayer.py --backend http://127.0.0.1:7350 --join-delay 35 --output TestResults/ranking-e2e
```

Después de la prueba volver al comando `python3 scripts/online-stack.py up` para
recrear el servidor en tiempo real. Las pruebas de backend no validan el APK, la
conexión física de un Quest ni la fluidez del render; esas comprobaciones son
independientes.

### Validación del backend con ranking

- `TestResults/nakama-ranking-integration.log`: **266 checks** contra Nakama,
  PostgreSQL y WebSocket nativo, con el servidor NGO sustituido por un agente de
  prueba autenticado.
- `TestResults/nakama-ranking-regression.log`: **158 checks** de la integración
  anterior, repetidos después de incorporar ranking.

Estas pruebas no ejecutan la UI Unity del ranking ni el APK. La comprobación de
una partida clasificada completa con esos builds es una evidencia independiente.

### Validación integrada del ranking

- `TestResults/ranking-unity-checks.log`: 19 checks nuevos de presentación y
  respuestas tardías, más las regresiones de sesión, servidor y presentación.
  Tres capturas de menús en español con control de texto en `TestResults/Ranking`.
- Builds desktop y APK ARM64 completos (`TestResults/ranking-*-build.log`).
  Smoke offline desktop sin errores (`TestResults/ranking-offline-smoke.log`).
- `TestResults/ranking-e2e/result.json`: dos clientes Unity reales y servidor NGO
  Docker. El primer cliente esperó 35 segundos, renovó su ticket de ±100 a ±200,
  encontró rival y completó una partida con el mismo ganador, tick y reporte en
  ambos clientes. Sus consultas autenticadas confirmaron 1000→1016 y 1000→984,
  estadísticas y posiciones en la clasificación nativa. Los registros de
  PostgreSQL coincidieron. El servidor se dejó después a velocidad normal.

El build del APK no equivale a ejecutarlo en un Quest. Quedan pendientes la prueba
física de este APK y la conectividad por Internet.

### Validación del flujo anterior al ranking (26 de octubre de 2026)

- Backend real: 158 comprobaciones con Nakama, PostgreSQL y WebSocket nativo.
- Unity: búsqueda/cancelación, admisión asignada y recuperación de ACK perdido,
  más las regresiones existentes de presentación, sesión y ciclo del servidor.
- Dos partidas completas con clientes desktop y servidor Linux Docker mediante
  la cola real. La segunda pareja esperó capacidad y entró al liberarse el slot.
  Ambos clientes coincidieron en ganador, tick final y SHA-256 del reporte; los
  dos resultados persistidos en PostgreSQL también coincidieron.
- Build Linux dedicado, desktop y APK Android ARM64 completados. Smoke offline
  desktop: cero errores, movimiento, invariantes, selección y anclaje del menú.

Los resultados reproducibles están en `TestResults/online-e2e/result.json` y
`TestResults/online-e2e-requeue/result.json`; los logs de Unity usan el prefijo
`TestResults/online-`. El servidor quedó después con simulación a velocidad normal.
Esta validación no incluye ejecución del nuevo APK en un Quest físico ni WAN.

## Servidor de producción y ampliación

El mismo Dockerfile y Compose sirven de base para Linux; generar credenciales
nuevas en ese host y usar su dominio o IP pública como `--host`. Publicar la API
mediante HTTPS/WSS con un proxy TLS, conservar la consola y PostgreSQL privados,
y permitir UDP 7778 hacia el servidor de juego. El HTTP sin TLS de este ejemplo
es para desarrollo LAN. Los proxy logs no deben guardar la query `http_key` de
los RPC privados; preferir que esos RPC permanezcan en la red interna.

Antes de migrar versiones, respaldar PostgreSQL y comprobar restauración. Para
agregar capacidad, iniciar más servicios NGO con `serverId` y puerto propios,
agregar esos IDs a `LL_SERVER_IDS` y publicar sus puertos UDP. Los procesos se
registran con su hash real de contenido; sólo reciben parejas compatibles. El
plugin no crea contenedores ni escala la flota automáticamente.

La alta disponibilidad de Nakama, recuperación de una partida en curso y
ampliación dinámica del pool quedan fuera del pool local inicial. El ranking
actual no incorpora temporadas, ligas ni vinculación de cuentas después de
reinstalar; esas extensiones están separadas del cálculo de Elo y matchmaking.

## Dependencias y referencias

El cliente usa el SDK oficial Nakama Unity 3.22.1. Su procedencia, licencia y hash
del paquete importado están en [Assets/Nakama/SOURCE.md](../Assets/Nakama/SOURCE.md).

El plugin usa Go 1.27.1, `nakama-common` 1.48.0 y protobuf 1.36.12, alineados con
el [go.mod oficial de Nakama 3.41.0](https://github.com/heroiclabs/nakama/blob/v3.41.0/go.mod).
El builder y runtime Docker usan la misma versión `3.41.0`; Go plugins requieren
compatibilidad exacta de compilador y dependencias.

- [Nakama session-based multiplayer](https://heroiclabs.com/docs/nakama/concepts/multiplayer/session-based/)
- [Native matchmaking y hooks](https://heroiclabs.com/docs/nakama/concepts/multiplayer/matchmaker/)
- [Autenticación de servidores externos](https://heroiclabs.com/docs/nakama/guides/concepts/headless-server-auth/)
- [Rutas REST oficiales](https://github.com/heroiclabs/nakama/blob/v3.41.0/apigrpc/apigrpc.proto), incluyendo `/v2/account/session/refresh`
