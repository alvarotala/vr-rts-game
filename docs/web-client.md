# Cliente de escritorio (three.js) y juego cruzado con Quest

La versión de escritorio se reemplazó por un cliente web con three.js que corre en cualquier navegador de escritorio actual. Juega en los mismos servidores dedicados que el Quest: una partida puede tener un jugador en Quest y otro en el navegador. También puede jugar contra la IA sin conexión, con la misma simulación y la misma IA que Unity.

El código vive en `web/`. El build Unity de escritorio (`bash scripts/unity.sh desktop`) sigue disponible como herramienta de desarrollo, pero ya no es la versión de escritorio para jugadores.

## Cómo funciona el juego cruzado

El servidor dedicado (`NgoMatchServer`) sigue siendo la única autoridad. Ahora atiende dos transportes a la vez:

| Jugador | Transporte | Puerto por defecto |
| --- | --- | --- |
| Quest y build Unity | NGO / Unity Transport (UDP) | 7777 directo · 7778 asignado |
| Navegador | WebSocket (TCP) | 7877 directo · 7878 asignado |

Los dos asientos son neutrales respecto del transporte. La validación del saludo (`ConnectionHello`), la admisión con ticket del matchmaking público, la espera de "listo", las órdenes con su límite de tráfico, las instantáneas filtradas (`PlayerView`), los ACK idempotentes, el resultado final y la concesión por desconexión usan exactamente el mismo código para ambos.

`WebSocketGateway` es un servidor RFC 6455 sin dependencias de Unity, con hilos propios y límites de memoria por conexión. Los eventos se procesan en el hilo principal desde `Update`, así que el estado de la partida nunca se toca en paralelo. Cada mensaje binario lleva un byte de tipo y el mismo contenido que el mensaje NGO equivalente (`WebSocketMatchCodec`). Las instantáneas y el resultado conservan su compresión deflate y su fragmentación de 900 bytes. El navegador las reensambla con un puerto de `FrameAssembler`.

Diferencias del transporte WebSocket:

- El navegador envía primero el saludo. El servidor responde `Admitted` cuando el asiento queda reservado, lo que reemplaza el callback de conexión de NGO.
- La bienvenida incluye `countdownSeconds`, porque el navegador no tiene el reloj sincronizado de NGO. Los clientes NGO ignoran el campo.
- Un socket que no envía su saludo en 5 segundos se cierra. El motivo de cada rechazo viaja en el cierre del WebSocket.

La versión de protocolo sigue en 3. Un APK de Quest ya instalado puede jugar contra navegadores en cuanto el servidor se recompila con este cambio.

### Compatibilidad de contenido

El cliente web calcula al compilar el mismo hash de catálogo y mapas que `MatchProtocol.ContentHash`, y lee la versión de protocolo y de reglas de las fuentes C#. Si cambia el catálogo, un mapa o las reglas, hay que recompilar el cliente web junto con el servidor y el APK. Un cliente desactualizado recibe el mismo rechazo que un APK desactualizado.

## Matchmaking público

El navegador usa el mismo flujo de Nakama que Unity: sesión por dispositivo, `ll_search_open`, matchmaker nativo y consulta de `ll_search_status`. La búsqueda declara `websocket: true`. El backend registra el puerto WebSocket de cada servidor (`wsPort`) y solo asigna una pareja con un jugador de navegador a un servidor que lo tenga. Las parejas solo de Quest pueden usar cualquier servidor. El contrato completo está en [online-contract.md](online-contract.md).

## Partidas sin conexión

`web/core-wasm` compila las fuentes de `Assets/Frontier/Core` a WebAssembly con .NET 8, sin copiar ni reescribir reglas. La simulación y el `Strategist` corren en un Web Worker a 20 ticks por segundo. Las decisiones de la IA pueden tardar más de 100 ms en algunos ticks, y el worker evita que esos picos congelen el render. La página recibe el mismo `PlayerView` que en línea.

## Ejecutar y compilar

Requiere Node 22 o superior y el SDK de .NET 8 (la variable `DOTNET` permite indicar otra ruta).

```sh
cd web
npm install
npm run build:core      # publica la simulación WebAssembly en public/core
npm run dev             # servidor de desarrollo en el puerto 5173
npm run build           # verifica tipos y genera web/dist
```

`web/dist` es un sitio estático. Para servirlo junto al stack en línea:

```sh
python3 scripts/online-stack.py web    # imagen nginx en el puerto 8080
```

Para probar el juego en línea sin Unity, el arnés .NET levanta un servidor de desarrollo con el código real del servidor. Puede sumar un rival IA que entra por NGO, como un Quest:

```sh
.cache/dotnet-sdk/dotnet run --project tests/ServerHarness/ServerHarness.csproj -c Release -- \
  serve --port 7777 --ws-port 7877 --map rift --bot biological --bot-transport ngo
```

Luego elegir **Conexión directa** con la dirección `127.0.0.1:7877`. El arnés no reemplaza al servidor Unity: su transporte NGO es interno al proceso, así que un Quest real no puede conectarse a él.

### Despliegue público

Una página servida por HTTPS solo puede abrir `wss://`. En producción hay que poner un proxy TLS delante del puerto WebSocket de cada servidor y declarar el puerto público con `FRONTIER_ADVERTISE_WS_PORT` (`LL_PUBLIC_WS_PORT` en `compose.online.yml`). El servicio Nakama también debe publicarse por HTTPS, como ya exigía el cliente Quest.

## Controles

| Acción | Control |
| --- | --- |
| Seleccionar · seleccionar un área | Clic izquierdo · arrastrar |
| Agregar o quitar de la selección | Shift + clic |
| Mismo tipo cercano · mismo tipo en pantalla | Doble clic · Ctrl + clic |
| Orden contextual (mover, atacar, recolectar, reparar, punto de reunión) | Clic derecho |
| Encolar la orden | Shift + clic derecho |
| Avanzar al ataque | Doble clic derecho · A |
| Detener · mantener posición | S · D |
| Panel de órdenes (construir, entrenar, investigar, habilidades) | Q W E R T · Z X C V B |
| Punto de reunión · cancelar | F · G |
| Grupos: recuperar · guardar · agregar | 1–0 · Ctrl+1–0 · Shift+1–0 (doble pulsación centra la cámara) |
| Trabajadores inactivos · ejército | F1 · F2 |
| Cámara | Flechas, bordes de la pantalla, botón central, minimapa |
| Zoom | Rueda del mouse |
| Centrar en la selección · en la base | Espacio · Inicio |
| Mostrar todas las barras de vida | Alt |
| Cancelar · menú | Esc |

La selección por área prefiere unidades: si el rectángulo incluye unidades, no suma edificios. Con Shift, colocar un edificio mantiene activa la construcción para colocar otro. Estas comodidades son locales del cliente y no cambian el protocolo.

## Alcance actual y pendientes

Incluido: partidas contra la IA (3 dificultades, 3 mapas, 2 facciones), conexión directa, matchmaking público, interfaz en inglés y español con el catálogo de traducciones del juego, informe final y música de cada mapa.

No incluido todavía:

- Tutorial guiado.
- Historial de partidas y replays.
- Reasignación de teclas.
- Cola visible de órdenes por unidad con cancelación individual (la cola funciona; falta su panel).

El servidor dedicado debe recompilarse con Unity 6000.3.26f1 para incluir el transporte WebSocket. El APK no cambia de comportamiento, pero conviene compilarlo para confirmar que el proyecto compila con los archivos nuevos de `Frontier.Networking`. Esta máquina no tiene Unity. Los cambios del servidor se validaron con el arnés .NET y con las pruebas de Editor existentes ejecutadas en ese arnés (ver [validación](validation.md)).
