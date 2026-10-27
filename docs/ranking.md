# Ranking y rivales de nivel parecido

Las partidas públicas nuevas usan Elo con **1000 puntos iniciales**, factor
**K=32** y mínimo **0**. Ganar contra un rival más fuerte suma más; perder contra
uno más débil resta más. Un empate equivale a media victoria y puede ajustar los
puntos cuando los niveles son diferentes. Se redondea el cambio al entero más
cercano. Entre jugadores de 1000 puntos, una victoria deja 1016 y 984.

El perfil muestra puntos, puesto, victorias, derrotas, empates y partidas
clasificadas. Una cuenta nueva tiene 1000 puntos y no aparece en la clasificación
hasta completar su primera partida clasificada. Los nombres públicos iniciales
son `Player ABC123`, derivados de una parte del UUID público de Nakama; nunca de
la credencial aleatoria del dispositivo.

## Emparejamiento

El backend fija el rating de la búsqueda a partir del perfil autenticado y usa
una propiedad numérica del matchmaker nativo de Nakama. La consulta siempre
conserva la compatibilidad de protocolo, reglas y contenido.

| Tiempo buscando rival | Diferencia máxima aceptada |
| --- | --- |
| 0–19 segundos | ±100 puntos |
| 20–39 segundos | ±200 puntos |
| 40–59 segundos | ±300 puntos |
| Desde 60 segundos | ±400 puntos |

Ambos jugadores deben aceptar esa diferencia. Uno que lleva un minuto esperando
no puede forzar a un recién llegado a jugar fuera de su propio rango. El límite
no se elimina aunque haya pocos jugadores. Es una ventana de nivel aceptable;
el matchmaker nativo también considera la espera y no garantiza elegir siempre
la menor diferencia exacta de todo el pool.

El reloj de PostgreSQL decide la ampliación; el cliente sólo renueva el ticket
nativo cuando cambia `queueRevision`. El before hook reemplaza el rating, rango
y consulta del cliente. El matched hook vuelve a validar ambas identidades,
ratings persistidos y ventanas antes de crear la partida. La configuración usa
`matchmaker.rev_precision=true` y `rev_threshold=0` para mantener la consulta
mutua durante toda la búsqueda.

Encontrar rival y esperar un servidor disponible son estados distintos. Las
parejas ya formadas mantienen su asignación mientras esperan capacidad del pool.

## Autoridad, persistencia y clasificación de Nakama

Sólo el evento `finished` del servidor NGO autenticado calcula puntos. Los dos
cambios de Elo, sus estadísticas, el resultado de partida y el registro inmutable
por jugador se guardan en una sola transacción PostgreSQL. Los reintentos de
`finished` conservan el mismo resultado sin volver a sumar o restar. Una rendición
o desconexión que el servidor declare derrota durante una partida sí cuenta.
Cancelaciones, no-show y partidas abortadas por fallo del servidor no puntúan.

La migración es aditiva. Las partidas existentes reciben `rank_version=0` y
siguen sin puntuación incluso si se reintenta un evento antiguo. Sólo las parejas
creadas después de incorporar ranking usan `rank_version=1`. No se recalcula el
historial anterior.

Nakama mantiene la tabla pública autoritativa `ll_ranked_1v1_v1`, descendente y
sin reinicio estacional. Los clientes pueden leerla pero no escribir ni borrar
sus puntuaciones. El backend publica valores absolutos con operador `set` desde
una cola durable `ll_rating_outbox`; no publica incrementos susceptibles de
duplicarse. Un advisory lock separado serializa los publicadores entre procesos,
y cada publicación lee el perfil canónico actual en lugar de un evento antiguo.
Si el proceso cae entre escritura y confirmación, repite el mismo valor.

El `subscore` es la versión del perfil, igual al número de partidas clasificadas.
Así, más partidas resuelven los empates de Elo y se actualizan las estadísticas
incluso cuando un empate deja los puntos intactos. Nakama aplica su orden estable
para el último empate. La publicación puede demorarse brevemente después de la
transacción; un worker reintenta cada dos segundos y la lectura del ranking
también intenta vaciar la cola. El matchmaking siempre usa el rating canónico,
independientemente de esa demora.

## RPC de jugadores

Todos requieren una sesión Nakama de jugador; no aceptan identidad ni puntos
declarados por el cliente.

- `ll_rank_profile {}` devuelve `playerId`, `displayName`, `rating`, `games`,
  `wins`, `losses`, `draws`, `rank` y `leaderboardId`. `rank=0` significa sin puesto.
- `ll_rank_top {}` devuelve `entries` con los diez primeros, las mismas
  estadísticas y `isSelf` para identificar la fila propia.
- `ll_rank_result {matchId}` sólo permite a los participantes consultar su cambio:
  `status` (`pending`, `rated` o `unrated`), `ratingBefore`, `ratingAfter`,
  `ratingDelta` y `opponentRating`.
- `ll_search_open` y `ll_search_status` agregan `rating`, `searchRadius` y
  `queueRevision`. La revisión va de 0 a 3 y no cambia el `searchId` ni reinicia
  la antigüedad de la búsqueda.

La identidad anónima sigue asociada a la instalación y al endpoint del servicio.
La vinculación de cuentas, temporadas, ligas, detección de abuso y recuperación
tras reinstalar requieren trabajo adicional; no forman parte de este ranking.

## Pruebas y referencias

El Dockerfile del plugin ejecuta `go test ./...` antes de compilarlo. Los casos
unitarios cubren Elo, empates, mínimos y límites de las ventanas mutuas.
`backend/nakama/tests/ranking_integration.py` prueba los contratos contra Nakama
y PostgreSQL reales; debe ejecutarse con el slot online detenido, como la prueba
anterior de [Nakama](nakama.md), sin detener el servidor LAN independiente.

- [Leaderboards de Nakama](https://heroiclabs.com/docs/nakama/concepts/leaderboards/)
- [API Go de nakama-common 1.48.0](https://github.com/heroiclabs/nakama-common/blob/v1.48.0/runtime/runtime.go)
- [Matchmaker 3.41.0 y precisión mutua](https://github.com/heroiclabs/nakama/blob/v3.41.0/server/matchmaker.go)
