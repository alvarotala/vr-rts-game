# Little Legion — RTS para Quest 3

**Pequeños ejércitos. Grandes batallas.**

Prototipo nativo de estrategia en realidad mixta: un tablero sobre una mesa o el piso, dos facciones, economía, construcción, producción y combate terrestre. Incluye partidas offline contra IA, conexión LAN directa y búsqueda pública 1v1 con servidores dedicados NGO. El servicio online es autohospedado con Nakama y PostgreSQL en Docker; offline y LAN no necesitan ese backend. El arte geométrico es provisional.

La versión de escritorio es un cliente web con three.js que juega en los mismos servidores que el Quest: una partida puede enfrentar a un jugador de Quest con uno de escritorio. Ver [cliente de escritorio y juego cruzado](docs/web-client.md).

La dirección de producto es un juego gratis con venta de skins cosméticas para personalizar los ejércitos. Las primeras razas conceptuales son **Pharaoh Cats** (gatos egipcios), **Gummy Brigade** (ositos de goma) y **Cosmic Crew** (aliens). La monetización y estas colecciones todavía no están implementadas; ver [identidad, skins y primeras razas](docs/identity.md).

## Requisitos y configuración

Requiere **Unity 6000.3.26f1**, Android Build Support, SDK/NDK y OpenJDK instalados desde Unity Hub. Los paquetes están fijados en `Packages/manifest.json` y `Packages/packages-lock.json`.

1. Agregar esta carpeta a Unity Hub y abrirla con 6000.3.26f1.
2. Ejecutar **Frontier → Configure project** una vez, o `bash scripts/unity.sh configure`.
3. Abrir `Assets/Frontier/Scenes/Frontier.unity` y pulsar Play.

Se puede definir `UNITY_EDITOR` si el editor está instalado en otra ruta.

## Compilar e instalar

Para generar el APK, usar **Frontier → Build Quest APK** o:

```sh
bash scripts/unity.sh quest
```

El archivo se genera en `Builds/Quest/LittleLegion.apk`. Para instalarlo en un Quest con modo desarrollador y depuración USB autorizada:

```sh
adb devices
adb install -r Builds/Quest/LittleLegion.apk
adb shell monkey -p app.sadmonkey.littlelegion 1
```

El APK usa el firmado de desarrollo local, el identificador `app.sadmonkey.littlelegion` y el nombre visible **Little Legion**. Android lo instala como una aplicación nueva respecto al prototipo anterior; no migra sus datos y ambas instalaciones pueden coexistir. La publicación en una tienda, sus cuentas y su firmado de distribución no están configurados.

La versión de prueba para macOS con Apple Silicon se genera con `bash scripts/unity.sh desktop`, en `Builds/Desktop/LittleLegion.app`. Se conserva como herramienta de desarrollo.

## Versión de escritorio (navegador)

Requiere Node 22 o superior y el SDK de .NET 8, que compila la simulación a WebAssembly para jugar sin conexión:

```sh
cd web
npm install
npm run build:core
npm run dev
```

`npm run build` genera el sitio estático en `web/dist`, y `python3 scripts/online-stack.py web` lo sirve en el puerto 8080. Desde el menú se juega contra la IA, por conexión directa a un servidor dedicado (puerto TCP 7877) o por búsqueda pública. Controles, despliegue con HTTPS y pendientes en [docs/web-client.md](docs/web-client.md).

Para aceptar jugadores de escritorio, el servidor dedicado debe compilarse con esta versión del código. El APK actual sigue siendo compatible mientras catálogo, mapas y reglas no cambien.

## Multiplayer y servidor Docker

### Búsqueda pública 1v1

Con Linux Dedicated Server Build Support instalado para el mismo Editor, generar el servidor y levantar el stack online. Reemplazar la IP del ejemplo por la dirección LAN de esta Mac:

```sh
python3 scripts/build-server.py
python3 scripts/online-stack.py init --host 192.168.100.157
python3 scripts/online-stack.py up
```

Abrir **Multijugador → Buscar partida** en ambos clientes. El tablero se coloca antes de iniciar la búsqueda. Nakama conserva una identidad por instalación, encuentra dos jugadores compatibles y entrega a cada uno un ticket para el mismo servidor NGO. La configuración del servicio online se guarda separada de la conexión LAN. Desde Quest, usar `http://192.168.100.157:7350` como servicio; el endpoint de juego se recibe automáticamente.

Para abrir Desktop con búsqueda automática, después de compilarlo:

```sh
open -n "/Users/alvaro/Documents/vr-rts-game/Builds/Desktop/LittleLegion.app" --args \
  -frontier-backend http://127.0.0.1:7350 -frontier-matchmake \
  -logFile /tmp/little-legion-online.log
```

El stack inicial tiene **un slot de partida** en UDP 7778 (TCP 7878 para navegadores); las parejas siguientes esperan capacidad. PostgreSQL mantiene los datos en un volumen persistente. No hay escalado automático, ranking ni reconexión a una partida en curso. La configuración, seguridad y ampliación manual del pool están en [Multiplayer público autohospedado](docs/nakama.md) y su [contrato de asignación](docs/online-contract.md).

La API HTTP del ejemplo es para pruebas locales. Una instalación pública necesita HTTPS/WSS y acceso al puerto UDP de juego. Esta documentación no implica un despliegue WAN ni una validación del flujo público en Quest físico.

### Conexión LAN directa

El servidor directo sigue siendo independiente del stack online:

```sh
python3 scripts/build-server.py
docker compose -f compose.multiplayer.yml up -d --build
```

Abrir **Multijugador → Conexión directa** en dos clientes y conectar a `127.0.0.1:7777` desde esta Mac. La partida comienza cuando ambos están conectados. Un jugador de escritorio se une al mismo servidor con **Conexión directa** y la dirección `127.0.0.1:7877`. Compose publica UDP 7777 y TCP 7877 en todas las interfaces (`0.0.0.0`) por defecto. Desde Quest u otra computadora en la misma red, usar la dirección LAN de la Mac; `0.0.0.0` es la dirección de escucha, no la dirección de conexión. El menú directo permite editar dirección y puerto. Para limitar el servidor a esta Mac, iniciar Compose con `FRONTIER_BIND_ADDRESS=127.0.0.1`.

La imagen contiene solamente el servidor Linux y se puede trasladar a un host compatible. Cada proceso directo atiende una partida a la vez; cuando ambos jugadores salen, vuelve a esperar otros dos sin reiniciar el contenedor. Instrucciones, límites y pruebas reproducibles en [Multiplayer y Docker](docs/multiplayer.md).

## Uso básico

Para aprender desde cero, elegir **Tutorial guiado** en el menú principal. La partida pausa cada explicación y enseña selección, economía, construcción, menú, rueda Touch, producción, grupos y combate hasta la victoria. Cada lección exige completar su objetivo antes de avanzar; ver [tutorial guiado](docs/tutorial.md).

Elegir facción, rival, dificultad y mapa. En Quest, usar **Ubicar tablero** y confirmar con el gatillo; si no hay datos de la habitación, usar **Mesa** o **Piso** para colocarlo manualmente. Iniciar la partida, seleccionar unidades con el gatillo y dar órdenes contextuales con A. Inclinar el joystick abre la rueda unificada: arriba grupos, derecha acciones de la selección e izquierda órdenes; el gatillo entra a cada opción. **Hotkeys** permite personalizar los controles antes de jugar o desde pausa, con guardado local y restauración de valores predeterminados.

Los trabajadores recolectan y construyen; los edificios producen unidades e investigan mejoras. Se gana destruyendo todos los edificios enemigos. Consultar la [guía de juego](docs/gameplay.md) para los controles completos, el movimiento del menú y la identificación de unidades.

Por ahora las dos facciones comparten todas las reglas y estadísticas; se diferencian en nombres y apariencia. Ambas requieren un trabajador durante toda la construcción y pueden reparar con trabajadores. Las futuras skins mantendrán las mismas reglas y estadísticas: la compra personaliza la apariencia del ejército.

## Documentación

- [Identidad, skins y primeras razas](docs/identity.md)
- [Desarrollo y comprobaciones](docs/development.md)
- [Arquitectura y próximas etapas](docs/architecture.md)
- [Cliente de escritorio (three.js) y juego cruzado con Quest](docs/web-client.md)
- [Multiplayer, servidor dedicado y Docker](docs/multiplayer.md)
- [Búsqueda pública con Nakama y PostgreSQL](docs/nakama.md)
- [Formato de mapas](docs/maps.md)
- [Registro de validación y pendientes](docs/validation.md)
