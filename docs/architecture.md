# Arquitectura

## Responsabilidades y flujo

`Frontier.Core` no referencia Unity. `Simulation` es la autoridad sobre recursos, ocupación, producción, combate y visión. `GameCommand` es la única entrada de órdenes. Un reloj de 20 Hz ejecuta las reglas independientemente de los cuadros del visor.

`GameSession` conecta el reloj, la IA y el jugador. La IA `Strategist` recibe `PlayerView`, el catálogo y los datos estáticos públicos del mapa. No recibe `Simulation`, listas de enemigos ocultos ni recursos de otro jugador.

`BoardView` transforma coordenadas lógicas en una maqueta, interpola posiciones y recorta la representación al área física. Cambiar escala, orientación o zoom no modifica las distancias de la simulación. Las unidades enemigas no visibles no se presentan; edificios observados previamente aparecen como recuerdos sin actualizar su información.

`CommanderInput` convierte mandos o mouse en selección y órdenes. `CommandPanel` comparte sus acciones entre escritorio y Quest. `QuestBridge` encapsula passthrough, permisos, superficies, colocación, anclas y eventos de seguimiento.

## Datos

- `MapDefinition`: terreno y contenido inicial versionado, sin referencias a Unity.
- `CatalogDefinition`: estadísticas, costos, tiempos, requisitos y roles por identificador estable.
- `GameCommand`: intención, emisor, secuencia, entidades, destino u objetivo.
- `PlayerView`: recursos propios, entidades autorizadas, recuerdos y visión. Es el contrato adecuado para clientes remotos.
- `MatchSnapshot`: diagnóstico autoritativo completo, no debe enviarse a un rival. Actualmente contiene referencias al estado; serializar inmediatamente. No implementa restauración de partidas.
- `BoardPlacement`: UUID de ancla local y ancho del tablero. Se guarda fuera del mapa en el directorio persistente de la aplicación.

El jugador y la IA pasan por el mismo validador de órdenes. La producción reserva suministro antes de empezar; una salida bloqueada conserva la unidad en cola. Las construcciones rechazadas no cobran recursos. Cancelar producción devuelve su costo; cancelar una construcción devuelve el 75 %. Destruir un edificio no devuelve recursos.

La visibilidad se actualiza a 5 Hz. Las alturas y rocas limitan visión y tránsito. La navegación usa A* sobre una cuadrícula, posiciones de destino repartidas para grupos y reserva local de celdas para evitar superposición.

La IA mueve sus refuerzos a un punto de reunión para despejar salidas de producción y rutas de trabajadores. La dificultad fácil reacciona cada tres segundos, usa una composición menos especializada, limita su decisión de producir artillería a dos unidades y omite la investigación de resistencia. La normal reacciona cada segundo, usa ambas mejoras, produce hasta seis unidades de artillería y retira unidades heridas. Son decisiones de la IA; las reglas, los límites máximos y los costos de las facciones son los mismos.

## Multijugador, siguiente etapa

Conservar la simulación en un anfitrión y reemplazar la entrega local de órdenes por transporte. Usar Netcode for GameObjects, Unity Transport y Multiplayer Services/Relay para sesiones privadas por código. El anfitrión debe asignar el emisor autenticado; nunca confiar en el `player` enviado por el cliente.

Replicar exclusivamente `PlayerView` y eventos autorizados. Comparar versión del catálogo y hash del mapa antes del inicio. El ancla, el zoom y la ubicación de cada tablero permanecen locales. Primera entrega sin reconexión, migración de anfitrión, clasificación ni servidores dedicados.

## Editor, tercera etapa

Construir una aplicación desktop que modifique `MapDefinition` y reutilice `GridMap` para validar. Incorporar pintura, rampas orientadas, recursos, posiciones iniciales, decoración y deshacer/rehacer. Exportar JSON y referencias al catálogo; añadir importación de paquetes desde almacenamiento local al juego. La versión actual carga los mapas incluidos como recursos Unity.
