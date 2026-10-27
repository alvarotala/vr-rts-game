# Multiplayer público autohospedado

Nakama OSS 3.41.0 administra cuentas y matchmaking nativo. El módulo Go en
`backend/nakama` asigna partidas a un pool explícito de servidores NGO. La
simulación continúa en el servidor Unity dedicado; no se ejecuta en Nakama.
PostgreSQL 17 conserva cuentas, búsquedas, asignaciones, admisiones y resultados.
No se necesita una cuenta de Heroic Cloud, servicios pagos ni Docker socket.

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
| WebSocket asignado | `0.0.0.0:7878/tcp` | El mismo slot para jugadores de escritorio (navegador) |
| Cliente de escritorio | `0.0.0.0:8080/tcp` (opcional) | `online-stack.py web`: sitio estático three.js |
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

## Contrato y seguridad

El contrato completo está en [online-contract.md](online-contract.md). Los DTO
del módulo usan camelCase. El SDK oficial controla login, refresh y WebSocket;
el emparejamiento utiliza `RegisterMatchmakerMatched` de Nakama.

- El login device usa un identificador aleatorio por instalación, guardado en
  almacenamiento privado. Es una credencial de cuenta, no un identificador de
  hardware. Reinstalar sin conservarla pierde el acceso a esa cuenta hasta que
  se agregue vinculación con otro método de login.
- Cada búsqueda pertenece al `userId` de la sesión autenticada. El before hook
  valida la búsqueda y fija la consulta de compatibilidad; el matched hook
  comprueba nuevamente las identidades, sesiones y configuración persistida.
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
```

Cubre login persistente y refresh, aislamiento de búsquedas, suplantación de
identidad, sesiones duplicadas, dos usuarios emparejados, claims idempotentes,
replay de tickets, cola con pool ocupado, cancelación antes/después del inicio,
resultados inmutables, dos sesiones completas, reinicio y expiración de leases y
no-show. Para probar expiraciones sin esperar minutos adelanta sólo el reloj de
filas propias del fixture; no borra ni resetea datos.

Para la prueba integrada con clientes Unity reales, el override
`compose.online.smoke.yml` activa `-frontier-smoke` en el servidor NGO:

```sh
docker compose --env-file .cache/online/env -f compose.online.yml -f compose.online.smoke.yml --profile game up -d --build
python3 scripts/check-multiplayer.py --backend http://127.0.0.1:7350 --output TestResults/online-e2e
```

Después de la prueba volver al comando `python3 scripts/online-stack.py up` para
recrear el servidor en tiempo real. Las pruebas de backend no validan el APK, la
conexión física de un Quest ni la fluidez del render; esas comprobaciones son
independientes.

### Validación local del 26 de octubre de 2026

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

### Juego cruzado con escritorio — 26 de octubre de 2026

Los servidores registran `wsPort` y las búsquedas del navegador declaran `websocket`. Una pareja con un jugador de navegador solo se asigna a un slot con WebSocket; el estado `ready` entrega `port` (UDP) y `wsPort` (TCP). Si un proxy TLS publica el WebSocket en otro puerto, `LL_PUBLIC_WS_PORT` lo anuncia. Las columnas nuevas (`ws_port`, `websocket`) se agregan con `ADD COLUMN IF NOT EXISTS`, sin modificar filas existentes.

Validación local: la prueba de integración pasó con 190 comprobaciones contra Nakama 3.41.0 y PostgreSQL 17 en Docker, incluidas las nuevas de emparejamiento cruzado. Además, un jugador tipo Quest (NGO) y el cliente web (WebSocket) fueron emparejados por el matchmaker nativo, asignados por el agente real al mismo servidor y terminaron una partida con el mismo resultado persistido. El servidor de esa prueba fue el arnés .NET, no el build Unity.

Este entregable guarda resultados para una próxima etapa de ranking. Todavía no
calcula MMR ni publica una tabla de clasificación. La alta disponibilidad de
Nakama, recuperación de una partida en curso y ampliación dinámica del pool
quedan fuera del pool local inicial.

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
