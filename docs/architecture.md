# Arquitectura

## Responsabilidades y flujo

`Frontier.Core` no referencia Unity. `Simulation` es la autoridad sobre recursos, ocupación, producción, combate y visión. `GameCommand` es la única entrada de órdenes. Un reloj de 20 Hz ejecuta las reglas independientemente de los cuadros del visor.

`GameSession` conecta el reloj, la IA y el jugador. La IA `Strategist` recibe `PlayerView`, el catálogo y los datos estáticos públicos del mapa. No recibe `Simulation`, listas de enemigos ocultos ni recursos de otro jugador.

`BoardView` transforma coordenadas lógicas en una maqueta, interpola posiciones y recorta la representación al área física. Cambiar escala, orientación o zoom no modifica las distancias de la simulación. Las unidades enemigas no visibles no se presentan; edificios observados previamente aparecen como recuerdos sin actualizar su información.

El marcador de cursor se dibuja en el material del terreno, con punto y líneas de mira al ras de suelo/rampas, sin geometría adicional. `RayToMap` devuelve también la posición mundial exacta del impacto para terminar allí el láser; sincroniza el collider cuando cambia la transformación y descarta caras verticales. El marcador se dimensiona en metros y respeta el recorte; se compone después de la niebla como información de entrada, sin revelar contenido oculto.

La selección puede extenderse fuera del tablero mediante `RayToSelectionPlane`, que proyecta sobre la continuación del plano físico cuando el rayo no toca terreno visible. `CommanderInput.PointBoard` conserva la captura del arrastre hasta soltar, pero solo permite órdenes contextuales sobre un impacto real. `TrySelectionBounds` intersecta el rectángulo con la ventana visible del mapa según zoom/desplazamiento; tanto el contorno como la selección efectiva usan esos límites. Un arrastre enteramente externo no modifica la selección y la proyección no añade colliders ni geometría fuera del tablero.

`VisibleEntity.damageTick` proyecta el `lastDamageTick` del objetivo para efectos de daño recibido. Solo las entidades propias o visibles reciben ese dato; las copias recordadas lo omiten. No se calcula a partir de diferencias de vida, por lo que reparación/regeneración no crean impactos. `BoardView` combina hover y destello mediante un bloque de propiedades, conserva selección y reutiliza un marcador por entidad. El destello dura cuatro pasos; aro y color de barra, doce. El shader `Frontier/Impact` dibuja el aro transparente sin textura, respeta los límites del tablero y está incluido por `ProjectBuilder` para las próximas compilaciones. Las entidades destruidas se retiran como antes; no se incorpora un sistema de animaciones de muerte.

`CommanderInput` convierte mandos o mouse en selección y órdenes. `CommandPanel` comparte sus acciones entre escritorio y Quest. `QuestBridge` encapsula passthrough, permisos, superficies, colocación, anclas y eventos de seguimiento.

## Menús y ciclo de partida

`CommandPanel.MenuPage` separa inicio, preparación de partida, configuración, controles, acciones, pausa, confirmación de salida y resultado. Comparten el lienzo movible y la entrada por rayos. Las pantallas de configuración y controles recuerdan el menú de origen; abrirlas en partida mantiene la pausa de menú. `CommanderInput` solo permite selección, órdenes y grupos cuando `AcceptsGameInput` está activo. Retroceder y pausar se procesan antes de interceptar el rayo sobre el panel.

`CommandPanel.MatchHud.cs` contiene la presentación de partida: recursos, ficha de selección, acciones contextuales, ocupación de depósitos, grupos y controles de partida. Usa `PlayerView` y nombres del catálogo, sin consultar estado enemigo oculto. Las acciones y los menús conservan la misma entrada por rayos; grupos y pausa son controles persistentes separados del contenedor que se reconstruye al cambiar la selección o la cola. Los costos se presentan como minerales/gas, conservando el identificador interno de catalizador.

`GameSession.LeaveMatch()` detiene y libera la simulación y la vista, vacía selección, grupos y modos de órdenes y emite `MatchClosed`. `BoardView` limpia las entidades y recursos de esa partida, `CommandPanel` vuelve al inicio y `FrameMetrics` guarda las muestras pendientes. No se elimina el ancla ni se cambia la colocación física. La pausa del menú se libera de manera independiente de seguimiento, visor y foco.

El terreno se muestra durante la preparación, colocación o partida, y se oculta en el menú principal. Cancelar la colocación restaura posición, giro, ancho, padre y estado de confirmación anteriores. No modifica la ubicación guardada del ancla.

## Datos

- `MapDefinition`: terreno y contenido inicial versionado, sin referencias a Unity.
- `CatalogDefinition`: estadísticas, costos, tiempos, requisitos y roles por identificador estable.
- `GameCommand`: intención, emisor, secuencia, entidades, destino u objetivo.
- `PlayerView`: recursos propios, entidades autorizadas, recuerdos y visión. Es el contrato adecuado para clientes remotos.
- `MatchSnapshot`: diagnóstico autoritativo completo, no debe enviarse a un rival. Actualmente contiene referencias al estado; serializar inmediatamente. No implementa restauración de partidas.
- `BoardPlacement`: UUID de ancla local y ancho del tablero. Se guarda fuera del mapa en el directorio persistente de la aplicación.

El jugador y la IA pasan por el mismo validador de órdenes. La producción reserva suministro antes de empezar; una salida bloqueada conserva la unidad en cola. Las construcciones rechazadas no cobran recursos. Cancelar producción devuelve su costo; cancelar una construcción devuelve el 75 %. Destruir un edificio no devuelve recursos.

La visibilidad se actualiza a 5 Hz. Las alturas y rocas limitan visión y tránsito. La navegación combina A* sobre la cuadrícula con cuerpos circulares en coordenadas continuas. El catálogo define `collisionRadius` por unidad (hasta 0,49 celdas, para conservar el paso por corredores de una celda); el cuerpo visual cabe dentro de esa huella.

Las órdenes de grupo reservan destinos distintos con espacio entre ellos, incluso cerca del borde del mapa. Durante el recorrido, las unidades evitan el tráfico cercano, recalculan rutas con un costo de congestión y pueden ceder el paso temporalmente conservando su orden. Los aliados se empujan de forma gradual: cada desplazamiento comprueba cuerpos, edificios, límites, acantilados y rampas, sin exceder la velocidad de la unidad en ese paso. Si una cadena de empujes no cabe, se revierte completa. Los enemigos y las unidades en **Mantener posición** no se empujan.

La prioridad de actualización rota cada paso para repartir el avance entre unidades. Las salidas de producción y los nuevos edificios comprueban la huella completa de los cuerpos, evitando generar unidades superpuestas o encerrar una unidad en una construcción. Las pruebas de navegación cubren cruces, pasillos, rampas, formaciones de 50 unidades y recolección compartida.

`Harvesting` centraliza los objetivos de reparto: dos trabajadores por mineral y tres por catalizador. `AssignGather` planifica el grupo completo antes de aplicar cambios, descuenta sus asignaciones anteriores y reserva cada destino provisional. Prefiere el depósito indicado mientras tenga puestos libres; después busca otros del mismo tipo a un máximo de 14 celdas del destino. Comprueba exploración, cantidad conocida, extractor propio terminado y ruta con el radio del trabajador. Si alguno no tiene un destino accesible, rechaza la orden sin modificar el grupo.

La reserva permanece durante viajes y entregas. Los excedentes siguen recolectando y se redistribuyen tras entregar cuando hay un puesto libre cercano; no se modifica la velocidad de extracción. La producción de trabajadores, el regreso de construcción/reparación y el agotamiento reutilizan el mismo reparto. La búsqueda automática, si no encuentra recursos locales, puede volver a minerales explorados más lejanos. Cambiar entre mineral y catalizador entrega primero la carga anterior.

En `PlayerView`, cada `ResourceState` incorpora `assignedWorkers`, `optimalWorkers`, `baseId` y `extractorId`. Son datos derivados exclusivamente de las asignaciones y edificios del jugador que recibe la vista; los campos no se rellenan en los recursos autoritativos de `MatchSnapshot`. La capacidad es cero para depósitos conocidos como agotados y gas sin extractor propio terminado. Cada depósito pertenece, a efectos del resumen de interfaz, al centro propio terminado más cercano dentro de 18 celdas, evitando contarlo en dos bases. La cantidad oculta se conserva según la última observación, también al buscar destinos.

La IA mueve sus refuerzos a un punto de reunión para despejar salidas de producción y rutas de trabajadores. La dificultad fácil reacciona cada tres segundos, usa una composición menos especializada, limita su decisión de producir artillería a dos unidades y omite la investigación de resistencia. La normal reacciona cada segundo, usa ambas mejoras, produce hasta seis unidades de artillería y retira unidades heridas. Son decisiones de la IA; las reglas, los límites máximos y los costos de las facciones son los mismos.

## Multijugador, siguiente etapa

Conservar la simulación en un anfitrión y reemplazar la entrega local de órdenes por transporte. Usar Netcode for GameObjects, Unity Transport y Multiplayer Services/Relay para sesiones privadas por código. El anfitrión debe asignar el emisor autenticado; nunca confiar en el `player` enviado por el cliente.

Replicar exclusivamente `PlayerView` y eventos autorizados. Comparar versión del catálogo y hash del mapa antes del inicio. El ancla, el zoom y la ubicación de cada tablero permanecen locales. Primera entrega sin reconexión, migración de anfitrión, clasificación ni servidores dedicados.

## Editor, tercera etapa

Construir una aplicación desktop que modifique `MapDefinition` y reutilice `GridMap` para validar. Incorporar pintura, rampas orientadas, recursos, posiciones iniciales, decoración y deshacer/rehacer. Exportar JSON y referencias al catálogo; añadir importación de paquetes desde almacenamiento local al juego. La versión actual carga los mapas incluidos como recursos Unity.
