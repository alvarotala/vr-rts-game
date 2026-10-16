# Registro de validación

## Alcance de la evidencia

La simulación, la compilación Unity, la ejecución gráfica desktop y el funcionamiento en Quest son comprobaciones diferentes. Este archivo registra cada una; no considera una compilación como prueba física de realidad mixta.

## Cancelación individual de producción con hover rojo — fuentes, sin APK — 15 de octubre de 2026

Cada tarjeta ocupada de la cola, incluida la producción activa, admite cancelación directa con A/gatillo/mouse, sin confirmación. Al apuntarla se vuelve roja y muestra **Cancel + refund**. Devuelve el costo completo, libera suministro reservado y mantiene los demás pedidos en orden. Los pedidos tienen identidad estable por jugador; terminar o sustituir un elemento mientras se pulsa no cancela otra unidad. Los lugares vacíos permanecen inactivos y el botón general **Cancel / refund** conserva su comportamiento de cancelar el último pedido.

- Núcleo: **243 comprobaciones** aprobadas, incluidas las nuevas de cancelación para ambas facciones: cinco trabajadores idénticos, elemento intermedio/activo/último, reembolsos, suministro, avance, duplicados/IDs inválidos, productor incorrecto, propiedad, pausa, investigaciones y ausencia de información rival en la secuencia. Registro: `TestResults/queue-cancel-core.log`.
- `ProductionCancelPreview.Render`: **168 comprobaciones y 14 vistas**; colas de cinco trabajadores y cinco soldados, hover y limpieza de rojo, A/gatillo, cancelación de investigación, liberación fuera del botón, selección diferente, pausa/salida, reemplazo con la misma longitud y finalización antes de reconstruir el panel.
- Regresiones: ayuda (**134 comprobaciones / 36 vistas**), producción (**48 / 16**), HUD y recolección (**18 vistas**), flujo de menús (**46 / 12**) y rueda (**148 / 18**). Total Editor: **544 comprobaciones enumeradas y 114 vistas estáticas**. Sin desbordes de texto. Inspeccionadas cola llena de trabajadores, soldados biológicos, elemento activo e investigación con hover rojo.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones de pruebas. Registro: `TestResults/unity-queue-cancel.log`; vistas: `TestResults/ProductionCancel`. APK, `ProjectSettings` y `Assets/XR` conservan los **52 hashes** previos.

No se generó APK. Pendiente: validar el clic y la legibilidad del hover con mandos físicos en Quest; las pruebas de entrada usan rayos sintéticos en el Editor.

## Ayuda de acciones, categorías y botón A en la consola — fuentes, sin APK — 15 de octubre de 2026

Hover continuo de 1,5 segundos sobre una acción muestra una descripción en la tarjeta lateral de selección. Incluye los once roles de cada facción, mejoras, órdenes y cancelación; costos, tiempos y requisitos usan el catálogo vigente. La ayuda no tapa acciones, recursos ni colas y se limpia al cambiar de objetivo, pulsar, pausar, abrir la rueda, perder foco o cambiar de selección. Construcción, producción, investigación, órdenes y cancelación se separan por filas, encabezados y colores. La zona de selección/acciones gana 62 unidades de alto para conservar el tamaño de los botones.

A y gatillo comparten la interacción con todos los botones y la barra de movimiento de la consola. El botón que inicia una pulsación conserva su liberación, evitando duplicados al combinar ambos; las pulsaciones y liberaciones capturadas no pasan al terreno. A conserva las órdenes contextuales sobre el mapa y los atajos previos de la rueda.

- `ActionHelpPreview.Render`: **134 comprobaciones y 36 vistas**. Ayuda de ambas facciones, demora, cambio/salida del hover, pausa/selección/foco, colores y secciones sin superposición; A para navegación, producir, investigar, cancelar, grupos, arrastre, combinación con gatillo, liberación fuera del botón y conservación de las órdenes sobre el mapa.
- Regresiones en la misma ejecución: `ProductionQueuePreview.Render` (**48 comprobaciones, 16 vistas**), `HarvestingPreview.RenderHud` (**18 vistas y verificaciones de acciones/recolección**), `MenuFlowPreview.Render` (**46 comprobaciones, 12 vistas**) y `ActionWheelPreview.Render` (**148 comprobaciones, 18 vistas**).
- Total: **376 comprobaciones enumeradas y 100 vistas estáticas**; etiquetas verificadas contra el alto disponible. Inspeccionadas ayuda del centro principal, combatiente biológico y tecnología con cola e investigación. Registro: `TestResults/unity-action-help.log`; vistas nuevas: `TestResults/ActionHelp`.
- Scripts compilados con Unity **6000.3.26f1**, mediante Unity CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones de pruebas. APK, `ProjectSettings` y `Assets/XR` mantienen los **52 hashes** previos. No se cambió la simulación ni se construyó un jugador.

Pendiente en Quest: comprobar legibilidad, comodidad de la demora y pulsaciones físicas de A/gatillo. Las pruebas usan rayos y tiempos controlados en el Editor. El APK existente **0.1.5** todavía no incluye estos cambios ni los atajos del commit anterior.

## Doble grip y atajos de la rueda — fuentes, sin nuevo APK — 15 de octubre de 2026

Dos pulsaciones rápidas del grip derecho abren la rueda, además del joystick. Con la rueda abierta, gatillo/A ejecutan y grip/B retroceden o cierran. El grip sostenido sigue siendo Shift fuera de la rueda. La pulsación de apertura no cierra la rueda, sostener botones no repite acciones y se espera a soltarlos antes de devolver la entrada al mapa.

- `ActionWheelPreview.Render`: **148 comprobaciones y 18 vistas** aprobadas para ambas facciones. Incluye doble grip rápido/lento, pulsación larga, selección aditiva, confirmación por gatillo, submenú Build, consumo de la pulsación de construcción, grip para volver/cerrar, foco, pausa y menú principal, además de las regresiones de A/B y comandos. Registro: `TestResults/unity-grip-wheel.log`.
- `MenuFlowPreview.Render`: **46 comprobaciones y 12 vistas**, con instrucciones Touch actualizadas. Registro: `TestResults/unity-grip-wheel-menus.log`. Inspeccionados el pie de ayuda de la rueda y la pantalla completa de controles, sin recortes de texto.
- Scripts compilados con Unity **6000.3.26f1** / CLI **1.0.0-beta.13**. Sin cambios de simulación. APK, `ProjectSettings` y `Assets/XR` conservan los 52 hashes de la línea base; el APK **0.1.5** anterior no incluye estos atajos.

Pendiente: comprobar la comodidad del intervalo de doble pulsación (0,35 s) y de los botones físicos en Quest. Las pruebas usan entradas y tiempo sintéticos; no se generó APK.

## APK integrado — 0.1.5 (6) — 15 de octubre de 2026

Compilación solicitada por el usuario con todos los cambios hasta `1744cb9`: siluetas de edificios, cola de producción visible, dificultad Very Easy, tablero fijo después de colocarlo, grip derecho como Shift, rueda contextual con joystick derecho y A/B, selección de minerales más tolerante, contadores persistentes de trabajadores y nuevos cristales/respiraderos de gas. Las notas históricas de «sin nuevo APK» para estas tareas quedan incluidas en esta entrega.

- **APK:** `Builds/Quest/FrontierCommand.apk`, **58.932.901 bytes**, paquete `com.alvaro.frontiercommand`, versión **0.1.5**, código **6**, Android ARM64/IL2CPP, mínimo 32 y objetivo 36.
- **SHA-256:** `a1fe9f485991a8e5d595afc5ad43f9870267d0423eeddda2e87bc2d924adfbb8`.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**: compilación correcta, `editorErrors` vacío. Integridad ZIP y firma APK v2 verificadas; el certificado coincide con 0.1.4. El manifiesto conserva Quest 3, passthrough, escena, anclas y control de visibilidad del límite.
- Registro y comprobaciones: `TestResults/unity-apk-0.1.5.log`, `apk-0.1.5-build.json`, `apk-0.1.5-signature.txt`, `apk-0.1.5-badging.txt`, `apk-0.1.5-manifest.txt`, `delivery-0.1.5.json` y `Builds/Quest/FrontierCommand.provenance.json`.
- Copia anterior conservada en `Builds/Quest/Archive/FrontierCommand-0.1.4.apk`, junto a su procedencia. Solo se actualizan las versiones de la configuración y este registro; no se cambia la jugabilidad para compilar.

Para actualizar la instalación existente: `adb install -r Builds/Quest/FrontierCommand.apk`. No se generó un ejecutable desktop ni se repitieron las pruebas de cada tarea. Esta entrega confirma compilación, firma y empaquetado; la prueba en Quest físico queda a cargo del usuario.

## Cristales minerales y respiraderos de gas — fuentes, sin nuevo APK — 15 de octubre de 2026

Los bloques minerales se sustituyen por grupos de cinco cristales facetados sobre roca. El gas cambia de esfera a una cavidad rocosa con vapor ascendente. Hay tres variantes de cada recurso y rotación estable por depósito; las mallas y los materiales se comparten.

- `ResourceVisualPreview.Render`: **12 comprobaciones y diez vistas**, incluyendo minerales, gas, vistas cenitales, hover, tablero compacto y extractor. Comprueba pausa/avance del vapor, niebla, construcción, agotamiento, ocultación y salida de partida. Los modelos usan un material cada uno, sin colliders añadidos, y quedan por debajo de 1300 vértices cada uno. Registro: `TestResults/unity-resource-models.log`; vistas inspeccionadas en `TestResults/ResourceModels/`.
- `ResourcePickingPreview.Render`: **80 comprobaciones y seis vistas** aprobadas con las nuevas mallas en Rift y Training: apuntado oblicuo, distintos tamaños, hover, trabajadores próximos, clic con temblor y órdenes de recolección. Registro: `TestResults/unity-resource-model-picking.log`.
- Compilación de scripts en Unity **6000.3.26f1** / CLI **1.0.0-beta.13**. El APK y los ajustes Unity/XR conservan los hashes de la línea base de 52 archivos. Sin modificaciones de simulación, mapas o catálogos; sin nuevo ejecutable.

Pendiente: comprobar la legibilidad y el costo del vapor en Quest real. Las vistas y rayos del Editor no certifican rendimiento ni percepción estereoscópica en el visor.

## Contadores de trabajadores sobre el tablero — fuentes, sin nuevo APK — 15 de octubre de 2026

Cada base propia terminada muestra el total de trabajadores de sus minerales y cada extractor muestra su gas, sin necesidad de selección. Los números usan asignados/óptimo; `5/8` y `3/3` son blancos, mientras `9/8` y `4/3` se vuelven rojos. El panel de recolección también marca el exceso en rojo.

- `HarvestingMarkersPreview.Render`: **96 comprobaciones y ocho vistas**, con ambas facciones. Comprueba trabajadores iniciales, órdenes reales, viajes/entregas, color normal/exceso, extractor en construcción, agotamiento, pausa, edificio destruido, ocultación y salida de partida. Verifica orientación desde cuatro lados, separación de contadores cercanos, tamaño físico en tablero de 0,6 m y ausencia de interceptación por los gráficos. Registro: `TestResults/unity-workforce-markers.log`; imágenes: `TestResults/Workforce/`.
- `HarvestingPreview.RenderHud`: **18 vistas** y comprobaciones de órdenes contextuales, selección de recursos, totales de base, exceso y superficie del extractor. Registro: `TestResults/unity-workforce-hud.log`.
- Inspeccionadas las vistas de `5/8`, exceso mineral/gas y tablero compacto. El render de prueba reproduce el orden de los elementos del Canvas al dibujar sus mallas en la cámara de preview.
- Compilación en Unity **6000.3.26f1** / CLI **1.0.0-beta.13**. Sin cambios en simulación, mapas o catálogos. El APK, `ProjectSettings` y `Assets/XR` conservan los hashes de la línea base de 52 archivos; no se generó ningún ejecutable.

Pendiente: validar legibilidad y separación de etiquetas con el visor y seguimiento reales en Quest.

## Selección de minerales más tolerante — fuentes, sin nuevo APK — 15 de octubre de 2026

El cursor ahora considera el volumen del cristal, además de su proximidad en el suelo. Un margen adaptado al tamaño del tablero facilita apuntar cerca de la base y el depósito se ilumina con un aro amarillo antes de pulsar. Los márgenes ampliados de trabajadores cercanos no ocultan el depósito; los impactos directos sobre el cuerpo de una unidad o edificio conservan prioridad. La pulsación de un recurso tolera pequeños movimientos al soltar sin convertirse en arrastre ni cambiar al depósito vecino. Hover, órdenes y datos del panel usan el mismo objetivo resuelto; una selección de recurso ya confirmada mantiene sus propios datos en el panel.

- `ResourcePickingPreview.Render`: **80 comprobaciones y seis vistas** de Rift y Training, con tableros de 0,6 / 1,2 / 2 m, rotación y rayos oblicuos desde cuatro lados. Comprueba conflicto con trabajadores, selección por gatillo con temblor, A para recolectar cerca del depósito, prioridad del trabajador apuntado directamente, eliminación del hover, pausa, niebla, agotamiento, recorte y salida de partida. Registro: `TestResults/unity-mineral-picking.log`; imágenes: `TestResults/ResourcePicking/`.
- Regresión de selección: **33 comprobaciones y seis vistas**, incluidos bordes, selección aditiva, arrastre externo y aislamiento de órdenes. Registro: `TestResults/unity-mineral-selection.log`.
- HUD y recolección: **18 vistas** con comprobaciones de asignación contextual, recurso seleccionado, saturación/exceso, totales de base y superficie del extractor. Registro: `TestResults/unity-mineral-hud.log`.
- Compilación y comprobaciones en Unity **6000.3.26f1** / CLI **1.0.0-beta.13**. Sin cambios en simulación, mapas o catálogos. El APK existente, `ProjectSettings` y `Assets/XR` conservan los hashes de la línea base (52 archivos); no se generó ningún ejecutable.

Pendiente: confirmar la facilidad de apuntado con el mando real en Quest, especialmente en mesa pequeña y desde un ángulo bajo. Los rayos y pulsaciones de esta prueba son sintéticos.

## Rueda contextual en mando derecho — fuentes, sin nuevo APK — 15 de octubre de 2026

La pulsación del joystick derecho abre la rueda junto al mando; sus ejes eligen una acción, **A ejecuta** y **B vuelve/cierra**. Se conservan el gatillo para seleccionar/indicar destinos y el grip para selección aditiva. El mando izquierdo queda sin funciones del juego, incluida la antigua opción de mano dominante. Colocación y recuperación de consola también usan combinaciones del mando derecho. La rueda y el panel comparten las mismas acciones validadas.

- Unity **6000.3.26f1**, CLI **1.0.0-beta.13**; compilación de scripts correcta. `ActionWheelPreview.Render`: **102 comprobaciones y 18 vistas** de ambas facciones, usando cuadros sintéticos de entrada en el enrutador real. Cubre A/B, gatillo sin ejecución, submenús, zona neutra, producción/investigación/costos/reembolso, rechazo con motivo visible, cambios de cola/selección, pausa/seguimiento/foco, colocación, unidad perdida, partida reemplazada y resultado. Registro: `TestResults/unity-action-wheel.log`.
- Regresión de consola: **48 comprobaciones de producción / 16 vistas**, **45 comprobaciones de menús / 12 vistas**, y HUD de recolección con **18 vistas**. Registro: `TestResults/unity-wheel-regression.log`. Los controles Touch describen el mando derecho y el izquierdo sin asignación; la configuración ya no ofrece invertir la mano.
- **33 comprobaciones de selección / 6 vistas**, incluidos bordes, áreas, selección aditiva y aislamiento de órdenes: `TestResults/unity-wheel-selection.log`.
- Inspeccionadas las vistas de trabajador, soldado, construcción y rechazo de acciones de `TestResults/ActionWheel/`, además de `TestResults/Menus/04-controls-touch.png`. El render de sectores incluye `CanvasRenderer`; texto oscuro en la opción resaltada mejora el contraste. La ayuda de la rueda indica A/B en una línea.
- Verificadas las asignaciones en el SDK local: `PrimaryThumbstick`, `PrimaryIndexTrigger`, `PrimaryHandTrigger`, `One` y `Two` sobre `RTouch` corresponden al joystick, gatillo, grip, A y B derechos. La entrada del juego no consulta `LTouch` ni el botón de menú izquierdo.
- Sin cambios en el núcleo de simulación. Sin APK ni ejecutable desktop nuevo: los **52 archivos** de la línea base de `ProjectSettings`, `Assets/XR` y APK conservan su hash. El APK disponible sigue siendo **0.1.4** y no contiene estos cambios.

Pendiente en Quest: distancia/tamaño de la rueda junto al mando, legibilidad sobre passthrough, dirección de selección con el joystick físico, uso de A/B con una sola mano, colocación con grip + joystick y comprobación de que el mando izquierdo no dispara acciones. Las pruebas del Editor no sustituyen esa validación.

## Grip derecho como Shift — fuentes, sin nuevo APK — 15 de octubre de 2026

La selección aditiva cambia del gatillo secundario al **grip físico derecho**, incluso al usar el modo zurdo. Se lee mientras está sostenido y se aplica tanto a clics como a selección por área. Se conserva el comportamiento existente: sumar unidades, quitar una unidad ya seleccionada al pulsarla con el modificador y reemplazar la selección al seleccionar sin él. La ayuda Touch ahora muestra **Hold right grip: add to selection**; desktop conserva Shift.

- Compilación correcta y **44 comprobaciones de menús** con doce vistas. Inspeccionada `TestResults/Menus/04-controls-touch.png`; registro `TestResults/unity-right-grip-menus.log`.
- **33 comprobaciones de selección** con seis vistas: selección individual, área aditiva desde fuera del tablero, bordes, cancelación y aislamiento de órdenes. Registro `TestResults/unity-right-grip-selection.log`. Esta regresión usa rayos y la bandera de selección aditiva; no emula el hardware del grip.
- Verificada la asignación en el SDK instalado: `PrimaryHandTrigger` sobre `RTouch` corresponde a `RHandTrigger`. Se utiliza `Get`, no `GetDown`, sin depender de la mano dominante. Unity **6000.3.26f1** / CLI **1.0.0-beta.13**.
- Sin cambios en las reglas de simulación y sin APK ni ejecutable desktop nuevo. El APK **0.1.4**, `ProjectSettings` y `Assets/XR` conservan sus hashes anteriores.

Pendiente: probar la combinación física de grip derecho y gatillo en Quest, tanto selección individual como por área y en modo zurdo. El APK disponible todavía no incluye este cambio.

## Tablero fijo tras colocación en Quest — fuentes, sin nuevo APK — 15 de octubre de 2026

Se retiran desplazamiento, zoom y restablecimiento de vista mediante sticks en Quest. Los ejes siguen ajustando altura/tamaño/giro durante la colocación explícita; una vez confirmada, el eje principal solo acerca/aleja un menú que se está arrastrando y la pulsación secundaria recupera el menú. `Full map` desaparece del HUD Quest y pausa ocupa su espacio. Las instrucciones Touch explican la colocación fija y el acceso a Settings para recolocar. El modo de prueba desktop conserva WASD/rueda/F.

- Compilación de scripts y **44 comprobaciones de menús aprobadas**, con doce vistas, en Unity **6000.3.26f1** / CLI **1.0.0-beta.13**. Comprueba instrucciones sin navegación de mapa, movimiento/recuperación de menú documentados, HUD Quest sin `Full map`, pausa ampliada accionable mediante rayos y conservación de controles desktop, además del flujo de colocación/cancelación y partida existente.
- Inspeccionadas `TestResults/Menus/04-controls-touch.png` y `06-actions-touch.png`. Registro: `TestResults/unity-fixed-board.log`.
- Revisión de la ruta de entrada: `CommanderInput` llama a `Board.Navigate` únicamente cuando `Quest.Active` es falso; la rama Touch no invoca `Board.Fit`. `PlaceRay` continúa recibiendo los sticks solo durante `Placing`. No cambió la simulación ni la implementación del ancla; no se repitieron pruebas del núcleo.
- No se generó APK ni ejecutable desktop. El APK **0.1.4**, `ProjectSettings` y `Assets/XR` conservan sus hashes previos a esta tarea; el APK disponible todavía no incluye este cambio.

Pendiente: comprobar físicamente que mover y pulsar ambos sticks después de colocar no altere el tablero, incluyendo modo zurdo, y que se pueda recolocar desde Settings y mover el menú. La previsualización del Editor no simula los mandos Touch ni valida el ancla del visor.

## Dificultad Very Easy — fuentes, sin nuevo APK — 15 de octubre de 2026

Se agrega **Very Easy** al selector, con una explicación breve en la preparación de partida. La IA mantiene una economía y ejército pequeños, sin tecnología ni expansión, y oleadas ofensivas de tres soldados desde el minuto 18, separadas por al menos tres minutos. Se defiende si la atacan antes. Easy y Normal conservan sus políticas previas; costos, visión y reglas de combate son los mismos para todas las dificultades.

- **207 comprobaciones de núcleo aprobadas**, incluidas 40 nuevas de dificultad, doce comprobaciones sobre cuatro escenarios de práctica y cuatro partidas completas. Registro: `TestResults/very-easy-core.log`.
- **Práctica en ambos mapas y con ambas facciones:** máximo de ocho trabajadores y seis soldados contando las colas; primera oleada a los **1.080 segundos** en los cuatro escenarios, sin tecnología ni mejoras. Simulaciones de hasta 19 minutos contra un jugador pasivo: dos terminaron antes porque la IA destruyó su base después del período inicial. Evidencia: `TestResults/very-easy-practice.json`.
- **4/4 partidas Normal contra Very Easy terminadas**, cubriendo todos los cruces de facciones. Normal ganó las cuatro en **841–853 segundos** simulados. Comprueban finalización y que Very Easy puede perder; no son una medida de dificultad percibida por principiantes. Evidencia: `TestResults/matches.json`.
- **39 comprobaciones de menús y once vistas** en Unity **6000.3.26f1**, CLI **1.0.0-beta.13**: ciclo Very Easy/Easy/Normal, descripción, creación del estratega y conservación de dificultad al volver a jugar o hacer revancha. Inspeccionada la vista `TestResults/Menus/05-very-easy.png`; registro `TestResults/unity-very-easy.log`.
- No se generó APK ni ejecutable desktop. El APK **0.1.4**, `ProjectSettings` y `Assets/XR` conservan sus hashes previos. El APK existente todavía no contiene este nivel, la cola de producción ampliada ni los últimos modelos de edificios.

Pendiente: confirmar con una partida humana que el ritmo resulte cómodo y ajustar límites/tiempos según esa experiencia. No se probó esta entrega en Quest físico.

## Cola de producción visible — fuentes, sin nuevo APK — 15 de octubre de 2026

El productor seleccionado muestra una franja propia con cinco lugares: elemento activo destacado, barra, porcentaje, segundos restantes y cuatro tarjetas numeradas con los próximos nombres/roles. Incluye cola vacía/llena, investigación, pausa y espera por salida o suministro. Mantiene el espacio con cola vacía para que agregar unidades no desplace los botones. La cancelación conserva su comportamiento: elimina y devuelve el costo del último elemento.

- **48 comprobaciones aprobadas y dieciséis vistas** en Unity Editor, con ambas facciones: producción desde botones reales, avance por ticks, orden y duplicados, rechazo del sexto elemento, devolución al cancelar, cambio de elemento activo sin cambiar la longitud de cola, investigación, pausa, hover, cambios de selección, regreso del menú, despliegue bloqueado y liberación de salida. Los escenarios preparan edificios/recursos y ocupación o pérdida de suministro; no representan una partida completa.
- **Regresiones aprobadas:** panel general de partida/recolección (dieciocho vistas) y flujo de menús (**33 comprobaciones**, diez vistas). La validación comprueba que las etiquetas entren en su altura disponible. Se inspeccionaron visualmente cola llena, trabajador al 50%, investigación biológica y salida bloqueada.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**, ejecución final exitosa. Registro: `TestResults/unity-production-queue.log`; vistas: `TestResults/ProductionQueue`, `TestResults/MatchHud` y `TestResults/Menus`.
- No cambió la simulación ni el catálogo. No se generó APK ni ejecutable desktop; el APK **0.1.4**, `ProjectSettings` y `Assets/XR` conservan sus hashes previos a esta tarea.

Pendiente: comprobar lectura, tamaño físico del panel ampliado y comodidad con mandos en Quest. El APK existente aún no incluye esta mejora ni los modelos de edificios de la tarea anterior.

## Edificios reconocibles — fuentes, sin nuevo APK — 15 de octubre de 2026

Doce modelos provisionales nuevos: seis roles para cada facción, con siluetas, alturas, acentos y símbolos distintos. La base central tiene una corona dorada y cuerpo escalonado; la barraca, una entrada ancha o nido con huevos y dos chevrones naranjas. Gas usa conductos dobles; suministro, depósitos bajos; tecnología, reactor/capullo vertical; defensa, torre con cañón/aguijón. Se mantienen colores de equipo y materiales apagados para recuerdos bajo niebla. Las torres se orientan hacia el ataque y su destello coincide con el extremo del cañón.

- **33 comprobaciones aprobadas y diez vistas en Unity Editor**, sobre los modelos reales de `BoardView`: límites de huella, altura de barras/impactos, color enemigo, niebla/restauración, construcción, daño/hover/selección, dirección de torre, reutilización, zoom/giro y salida. Las doce mallas suman **8.734 vértices**, compartidos entre instancias, con un renderer por edificio y tres submallas. Esto no mide rendimiento del visor.
- **Regresiones de daño recibido y armas aprobadas**, con ocho y once vistas respectivamente; las armas conservaron sus 15 comprobaciones. Registro conjunto: `TestResults/unity-building-roles.log`. Galería: `TestResults/BuildingRoles`; regresiones: `TestResults/Hits` y `TestResults/Weapons`. Se inspeccionaron las vistas aliada, enemiga, superior, comparativa de base/barraca y recuerdo bajo niebla.
- No cambió el núcleo, el catálogo ni las reglas de combate; no se repitieron sus pruebas ni partidas entre IA. El APK **0.1.4** y las configuraciones existentes conservan sus hashes; no se generó APK ni ejecutable desktop.

Pendiente: comprobar identificación a escala real de mesa/piso, percepción binocular y legibilidad durante combates en Quest. Las vistas del Editor no sustituyen esa prueba. El APK disponible aún no contiene estos modelos.

## APK integrado — 0.1.4 (5) — 15 de octubre de 2026

Compilación solicitada por el usuario con todos los cambios hasta `d9f40ed`: siluetas de unidades, asignación y saturación de trabajadores, hover, textos en inglés, menú principal, panel de partida por secciones, cursor sobre el mapa, daño recibido, selección desde fuera del tablero y partículas/trayectorias de armas. Las entradas anteriores que indican «sin nuevo APK» describen su estado histórico; estos cambios ahora están incluidos en 0.1.4.

- **APK:** `Builds/Quest/FrontierCommand.apk`, **58.883.605 bytes**, paquete `com.alvaro.frontiercommand`, versión **0.1.4**, código **5**, ARM64/IL2CPP, Android mínimo 32 y objetivo 36.
- **SHA-256:** `eb7cdf6eab3b6adb6c6aa01656e469aa50cbf1159257a6d0385ae1173dfe6f65`.
- Unity **6000.3.26f1** y Unity CLI **1.0.0-beta.13**: compilación correcta, `editorErrors` vacío; shaders `Frontier/Board`, `Frontier/Impact` y `Frontier/Weapons` compilados para Android. Evidencias: `TestResults/unity-apk-0.1.4.log`, `TestResults/apk-0.1.4-build.json` y `Builds/Quest/FrontierCommand.provenance.json`.
- Integridad ZIP y **firma APK v2 verificadas**. El certificado coincide con 0.1.3, por lo que se puede actualizar mediante `adb install -r`. Metadatos y firma: `TestResults/apk-0.1.4-badging.txt`, `TestResults/apk-0.1.4-signature.txt`, `TestResults/apk-0.1.4-manifest.txt`.
- Copia del APK anterior: `Builds/Quest/Archive/FrontierCommand-0.1.3.apk`. Las configuraciones locales ajenas a esta tarea permanecen fuera del commit; se registran únicamente versión y conservación de los shaders nuevos, junto con este resultado.

No se modificó la jugabilidad ni se repitieron las pruebas ya aprobadas en las entradas de cada tarea. Esta entrega valida compilación y empaquetado; no se instaló ni probó en un Quest físico. Para actualizar: `adb install -r Builds/Quest/FrontierCommand.apk`, siguiendo los requisitos de conexión de la guía inicial.

## Partículas y trayectorias de armas — fuentes, sin nuevo APK — 15 de octubre de 2026

Se reemplazó la esfera fija de disparo por destello en el arma, orientación al objetivo real, trazadora industrial, proyectil biológico, arco de artillería, golpe cercano y chispas en destino. Las torres usan el mismo sistema. Una estela tenue conecta al atacante con su destino; los golpes letales conservan esa referencia aunque la entidad ya haya desaparecido. No cambió el daño ni la cadencia: el vuelo es visual.

- **151 comprobaciones del núcleo aprobadas**, incluidas 17 nuevas sobre objetivo de ataque automático, extremos de edificios, eventos letales, copia por valor, pausa, caducidad y privacidad al perder/ganar visión: `TestResults/weapons-core.log`. No se repitieron partidas de IA porque las reglas de combate no cambiaron.
- **15 comprobaciones visuales y once vistas en Unity Editor**, con scripts y shader compilados: línea temporal de disparo, ambas facciones, cuerpo a cuerpo, artillería, torres, objetivo destruido, pausa, niebla, zoom/giro, desaparición, presupuesto de 192 disparos y limpieza al ocultar/salir/reiniciar. Registro: `TestResults/unity-weapons.log`; vistas: `TestResults/Weapons`. Se inspeccionaron trazadoras, proyectiles biológicos, arcos, impactos y recorte con zoom. Los eventos de estas capturas son fixtures; los ataques reales se verifican en el núcleo.
- Una malla/material y buffers reutilizados evitan crear objetos por disparo. Esto no es una medición de rendimiento en Quest. Se añadió `Frontier/Weapons` a la lista de shaders conservados por `ProjectBuilder` para el próximo build.

No se generó APK ni ejecutable desktop. El APK y las configuraciones locales preexistentes permanecen iguales. Pendiente: revisar legibilidad en una batalla real, percepción binocular, costo de CPU/GPU y comodidad en Quest. El APK disponible no contiene estos efectos.

## Selección desde fuera del tablero — fuentes, sin nuevo APK — 15 de octubre de 2026

El arrastre puede empezar o terminar fuera del mapa usando la continuación del plano físico del tablero. Se conserva hasta soltar el gatillo o mouse y se recorta tanto el contorno como la selección a la ventana visible. Esto permite seleccionar en bordes y esquinas sin incluir unidades recortadas por zoom. Los arrastres enteramente externos no modifican la selección; movimiento, construcción y ataque desplazándose siguen requiriendo un impacto real sobre el terreno.

Validación en Unity Editor: **33 comprobaciones aprobadas y seis vistas estáticas**, usando rayos oblicuos por el mismo procesamiento de selección de la entrada real. Cubren los cuatro bordes, esquina, empezar dentro/soltar fuera, ambos extremos fuera, clic externo, clic individual, selección aditiva, zoom y giro, cancelación y bloqueo de órdenes externas. Inspeccionados borde izquierdo, esquina y recorte con zoom. Registro: `TestResults/unity-edge-selection.log`; vistas: `TestResults/EdgeSelection`.

No cambió el núcleo de simulación y no se repitieron sus pruebas ni partidas de IA. No se generó APK ni ejecutable desktop; se conservaron el APK y las configuraciones locales preexistentes. Pendiente: comprobar comodidad del gesto en mesa y piso con mandos físicos en Quest. El APK disponible no contiene este cambio.

## Señal de daño recibido — fuentes, sin nuevo APK — 15 de octubre de 2026

Unidades y edificios visibles reciben un destello claro breve, un aro naranja expansivo y una barra de vida naranja al ser golpeados. La señal procede del momento del impacto, incluye daño de área y no se confunde con reparación, regeneración o disparo. Se mantienen hover y selección; se reutiliza un único marcador por entidad y se limpia al ocultar, reutilizar o retirar la representación. El aro desaparece a los doce pasos (0,6 segundos de simulación), y el destello del modelo a los cuatro.

- **134 comprobaciones del núcleo aprobadas**, incluidas once comprobaciones nuevas de impactos reales sobre unidades/edificios de ambas facciones, daño de área, recuperación y privacidad: `TestResults/hit-core.log`. No se modificaron costos, daño ni reglas de combate; se añadió el dato de impacto a la vista autorizada. No se repitieron partidas entre IA.
- **Scripts y shaders compilados en Unity Editor**, con ocho vistas y comprobaciones de objetivos simultáneos, hover/selección, renovación, caducidad, reutilización, recuerdos bajo niebla y salida. Inspeccionados impactos simultáneos, soldado seleccionado, edificio y desvanecimiento. Registro: `TestResults/unity-hits.log`; vistas: `TestResults/Hits`.
- Se añadió el shader de impacto a la lista conservada por `ProjectBuilder` para la siguiente compilación. No se generó APK ni ejecutable desktop; se conservaron el APK y las configuraciones locales preexistentes.

Pendiente: comprobar legibilidad con mucha acción, percepción binocular y costo de los indicadores simultáneos en Quest. Las vistas estáticas del Editor no certifican una partida interactiva ni rendimiento físico. El APK disponible no contiene este cambio.

## Indicador del cursor sobre el mapa — fuentes, sin nuevo APK — 15 de octubre de 2026

Punto blanco y líneas de mira turquesa, con borde oscuro, dibujados al ras del terreno y las rampas. Mantienen tamaño físico al hacer zoom y se recortan al tablero. Se ocultan al limpiar el puntero, pausar u ocultar el mapa. El láser termina en el impacto real del collider, evitando la aproximación de altura por celda. La consulta actualiza la transformación y sincroniza física cuando cambia, para evitar desfases al desplazar, girar o ampliar el tablero.

Validación en Unity Editor: scripts y shader compilados, rayos contra suelo/rampa/altura, zoom/desplazamiento/giro, tamaño del marcador, rechazo de paredes y fuera del tablero, limpieza al pausar/ocultar/salir. Ocho vistas estáticas en `TestResults/Pointer`; registro `TestResults/unity-pointer.log`. La comprobación inicial detectó el desfase del collider tras cambiar la transformación; la versión corregida pasa ese escenario. Las vistas usan la representación real del tablero y una línea de ejemplo, sin entrada de mandos físicos.

No se modificaron reglas del núcleo ni se repitieron partidas de IA. No se generó APK ni ejecutable desktop. Pendiente: comprobar contraste, tamaño aparente y coincidencia binocular con el láser en Quest. El APK existente no contiene este indicador.

## Panel de partida por secciones — fuentes, sin nuevo APK — 15 de octubre de 2026

El panel separa contadores grandes de minerales/gas/capacidad, ficha de selección con nombre/rol/vida/estado, acciones contextuales, ocupación de depósitos, grupos de control y controles de partida. Incluye composición de selecciones múltiples, progreso de construcción/colas, cantidad por grupo y aviso de capacidad completa. La cancelación aparece cuando existe una construcción o cola que cancelar. Se conserva el panel movible y los textos en inglés.

Los trabajadores excedentes siguen recolectando y el indicador muestra **excess**, según la decisión confirmada por el usuario; el óptimo continúa en dos por mineral y tres por gas, sin introducir un límite rígido.

- **Scripts compilados y comprobación del panel aprobada en Unity Editor**, con dieciocho vistas estáticas sin desbordes de etiquetas. Verificación de nombre, recursos, capacidad, separación de grupos/pausa y botones reales mediante rayos: producir, cancelar y devolver recursos, elegir construcción, guardar y recuperar diez unidades. Inspeccionadas base, trabajador, gas sin extractor, exceso, investigación, selección mixta y unidad dañada. Registro: `TestResults/unity-match-hud.log`; vistas: `TestResults/MatchHud`.
- **33 comprobaciones del flujo de menús aprobadas**, con diez vistas estáticas: `TestResults/unity-hud-menu-flow.log` y `TestResults/Menus`. Sin errores de compilación ni excepciones en los registros finales.
- No se modificaron reglas del núcleo ni datos y no se repitieron partidas entre IA. No se generó APK ni ejecutable desktop. Se verificaron sin cambios los hashes de las configuraciones locales preexistentes y del APK.

Pendiente: comprobar tamaño aparente, lectura e interacción de las secciones con mandos en Quest. Las vistas estáticas y los rayos del Editor no equivalen a una partida interactiva ni a validación física. El APK disponible sigue en 0.1.3 y no incluye esta reorganización.

## Menú principal y ciclo de partida — fuentes, sin nuevo APK — 15 de octubre de 2026

La aplicación abre en un menú principal con **New Match**, **Settings** y **Controls**. La preparación de facción, rival, dificultad, mapa y tablero tiene su propia pantalla. El panel de acciones aparece al comenzar. La pausa ofrece continuar, configurar, consultar controles o regresar al inicio con confirmación de pérdida de la partida; el resultado ofrece revancha o menú principal.

Salir limpia simulación, vista, selección, grupos, órdenes pendientes y representación de entidades, conservando la colocación del tablero. Se mantienen las pausas externas. El botón de retroceso funciona también sobre el panel y cancela una recolocación sin perder la posición previa. La simulación no recibe órdenes desde los menús. Los informes de rendimiento pendientes se cierran al salir de la partida.

- **33 comprobaciones del flujo aprobadas en Unity Editor**, pulsando los botones reales mediante rayos: inicio, configuración, controles de escritorio/Touch, requisito de colocación, cancelación de colocación, pausas externas, salida cancelada/confirmada, limpieza, nueva partida con otro mapa/facción, resultado y revancha. Diez vistas estáticas sin desbordes de texto; inspeccionados menú principal, configuración, controles Touch, preparación y pausa. Registro: `TestResults/unity-menus.log`; capturas: `TestResults/Menus`.
- **Comprobación existente de acciones y recolección aprobada**, con quince vistas de preparación, contadores, trabajadores, producción, investigación, pausa y resultado. Registro: `TestResults/unity-menu-actions.log`. Scripts compilados sin errores ni excepciones en ambos registros.
- No se modificaron reglas del núcleo ni datos y no se repitieron partidas entre IA. No se generó APK ni ejecutable desktop. Los hashes del APK existente y de los cambios locales de configuración preexistentes permanecieron intactos.

Pendiente: recorrer el flujo con los mandos en Quest, comprobar distancias de lectura, recolocación/cancelación con ancla real e interrupciones del visor. Las comprobaciones del Editor no validan seguimiento físico ni una partida interactiva completa. El APK disponible todavía no incluye estos menús.

## Juego en inglés — fuentes, sin nuevo APK — 15 de octubre de 2026

La interfaz, ayuda de controles, colocación y recuperación del tablero, tutorial, avisos de órdenes y resultado, contadores de recursos y catálogo se presentan en inglés. Los mapas se llaman **The Rift** y **Training Grounds**; las facciones **The Consortium** y **Symbiosis**. El tutorial toma los nombres de edificios de la facción elegida y las colas muestran el nombre del catálogo para unidades y mejoras. Se actualizaron también los textos propios de permisos de escritorio y diagnósticos de carga. Los idiomas adicionales quedan para una etapa posterior.

- **123 comprobaciones del núcleo aprobadas**, sin ejecutar nuevas partidas de IA: `TestResults/english-core.log`.
- **Compilación de scripts y 15 vistas estáticas del panel aprobadas en Unity Editor**: inicio en ambos mapas, cinco estados de recolección, trabajador/tutorial, producción e investigación de ambas facciones, pausa y partida terminada. Comprobación de altura de las etiquetas sin desbordes y revisión visual de inicio, trabajador biológico, producción industrial, investigación biológica, gas sin extractor y pausa. Evidencias en `TestResults/unity-english.log` y `TestResults/English`.
- Revisión de cadenas en fuentes y comparación estructural de datos: solo cambiaron nombres y títulos en JSON, conservando identificadores, estadísticas, terreno y recursos.
- No se generó APK ni ejecutable desktop. Se verificó el hash del APK existente y se conservaron los cambios locales previos de configuración; en `ProjectSettings.asset` solo se añadieron las traducciones de las descripciones de cámara y micrófono.

Pendiente: comprobar la lectura y los controles en Quest. Las vistas del Editor no son una partida interactiva ni una validación física del visor. El APK disponible sigue siendo 0.1.3 y no contiene esta traducción.

## Hover de unidades y edificios — fuentes, sin nuevo APK — 15 de octubre de 2026

El hover usa un contorno de terreno independiente de la selección: círculo para unidades y cuadrado para edificios, amarillo en aliados y rojo en enemigos. El cuerpo se realza mediante un bloque de propiedades del renderer, conservando los materiales de facción y rol. El contorno se dibuja en el shader del terreno, sigue rampas, excluye paredes y respeta el recorte del tablero y la visión actual. El disco de selección solo se activa para entidades seleccionadas. La entrada limpia el puntero al perder foco o desactivarse.

Validación: scripts y shader compilados en Unity Editor, con siete vistas estáticas del `BoardView` real en `TestResults/Hover`. Se inspeccionaron unidad, edificio, enemigo, edificio seleccionado bajo hover y salida del hover: el borde desaparece y la selección persiste. Registro final sin errores de compilación, shader ni excepciones en `TestResults/unity-hover.log`. La vista previa no constituye una prueba interactiva ni física en Quest.

No cambió la simulación y no se repitieron las partidas entre IA. No se generó APK ni ejecutable desktop; el APK 0.1.3 y las configuraciones preexistentes conservaron sus hashes. Pendiente: comprobar tamaño aparente y contraste del hover en Quest, desde mesa y piso.

## Reparto y contadores de trabajadores — fuentes, sin nuevo APK — 15 de octubre de 2026

Las órdenes de recolección distribuyen grupos por depósitos cercanos, con objetivos de dos trabajadores por mineral y tres por gas/catalizador. Las reservas incluyen viajes y entregas; las órdenes repetidas no duplican asignaciones. Los excedentes siguen recolectando y se redistribuyen al entregar si hay puestos libres. No se introdujo un límite rígido ni se modificó la velocidad de extracción.

El panel muestra asignados/óptimo y cantidad restante al apuntar o seleccionar recursos, totales por base y ocupación por extractor. El gas sin extractor muestra el objetivo de tres junto al requisito de construcción. La orden contextual acepta toda la superficie del extractor. Se conserva la carga al cambiar de recurso y se buscan alternativas tras agotamiento o destrucción del extractor, respetando información explorada y rutas accesibles.

- **131 comprobaciones aprobadas**: las 89 regresiones existentes, 34 comprobaciones nuevas de recolección y ocho partidas de IA. Las pruebas cubren ambas facciones, grupos, órdenes repetidas, entregas, liberación de puestos, agotamiento, extractores, cambio de carga, excedentes, rutas y privacidad bajo niebla de guerra.
- **8/8 partidas de IA completas**, cuatro cruces de facciones por dos dificultades. Duración entre 1.028 y 1.237 segundos simulados; seis victorias de la posición 0 y dos de la posición 1. Son escenarios deterministas, no una evaluación de equilibrio competitivo.
- **Scripts compilados y comprobación del panel aprobada en Unity Editor**: órdenes contextuales, selección de depósitos, totales de base, exceso y selección/órdenes del extractor. Se inspeccionaron las capturas del gas disponible y sin extractor; las cinco vistas estáticas usan la geometría y disposición del panel real. Registro en `TestResults/unity-harvesting.log`; capturas en `TestResults/Harvesting`. No se ejecutó una partida interactiva en el Editor.

Evidencias del núcleo: `TestResults/harvesting-core.log` y `TestResults/harvesting-matches.json`. Los comandos reproducibles están en [desarrollo](development.md). No se generó APK ni ejecutable desktop. El APK 0.1.3 disponible no incluye estos cambios; falta comprobar interacción, legibilidad y rendimiento en Quest.

## Identificación de unidades — fuentes, sin nuevo APK — 15 de octubre de 2026

- Cinco siluetas por facción: recolector con carga y pinzas, explorador con aletas y sensor, infantería con escudo, tirador de arma larga y artillería de doble cañón o espina. Industrial conserva formas angulares; biológica usa cuerpos lobulados.
- Símbolos de rol con forma y color redundantes, visibles desde arriba, dentro de la huella de colisión para evitar superposición. El cuerpo y el borde conservan el color del equipo. Las barras de vida se muestran para unidades dañadas, seleccionadas o bajo el puntero.
- El panel identifica rol y nombre al apuntar sin selección, seleccionar o producir. La selección de grupos desglosa cantidades por rol.

Validación: compilación de scripts con Unity CLI y render de las diez variantes desde el Editor mediante `UnitVisualPreview.Render`, sin construir jugadores. Las mallas se reutilizan por arquetipo, con tres submallas por unidad y 5.816 vértices en total para las diez variantes. Se inspeccionan las vistas aliada, enemiga y cenital en `TestResults/UnitRoles`; registro en `TestResults/unity-unit-roles.log`.

No se modificó la simulación ni se repitieron partidas entre IA. No se generó APK ni build desktop: el APK disponible sigue siendo 0.1.3 y **no contiene estos cambios**. Pendiente: probar legibilidad durante una partida y a la distancia real de uso en Quest.

## Selección al ras del mapa — 0.1.3 — 15 de octubre de 2026

El rectángulo se dibuja directamente en el material del terreno. Se eliminó la malla ubicada por encima de la cota máxima del mapa: el contorno queda a la misma altura que cada superficie, sigue las rampas y excluye las caras verticales de los acantilados. Al compartir la superficie del suelo no necesita un desplazamiento de profundidad ni genera parpadeo por superposición. Se conserva el ancho visual, el zoom y el recorte al tablero.

Verificación local: compilaciones desktop y Android correctas con Unity CLI, sin errores del Editor. Prueba gráfica completa aprobada con cero errores. Se inspeccionaron `TestResults/Desktop/04-selection-ground.png` y `04-selection-ramps.png`, que muestran el contorno sobre suelo bajo y la zona de alturas; los desniveles verticales no se resaltan. El núcleo de simulación no cambió y no se repitieron las partidas entre IA.

APK actual: `Builds/Quest/FrontierCommand.apk`, versión **0.1.3 (4)**, **58.778.881 bytes**, ARM64/IL2CPP y firma APK v2 verificada. SHA-256: `4201435c8db7145ab8dc5d90a76310107cea381a8217d235da6f672a26fddd03`.

Evidencias: `TestResults/unity-selection-desktop.log`, `unity-selection-quest.log`, `selection-smoke.log`, `Desktop/smoke-result.json` y `delivery.json`. Los resultados anteriores se conservan en `delivery-0.1.2.json`. Pendiente: comprobar la selección con ambos ojos en Quest.

## Colisiones y navegación — 0.1.2 — 15 de octubre de 2026

Las unidades ahora usan cuerpos circulares ajustados desde el catálogo. Evitan tráfico, empujan gradualmente a aliados, ceden el paso en cruces y recalculan rutas congestionadas. Las formaciones reservan destinos separados, también cerca de las esquinas. Los enemigos y las unidades en **Mantener posición** permanecen sólidos; los edificios nuevos y las salidas de producción respetan los cuerpos completos. La geometría del cuerpo visible se ajustó a esa huella.

- **89 comprobaciones del núcleo aprobadas**, incluidas 24 regresiones de navegación y construcción. Cubren movimiento dentro de una celda, empuje a un aliado quieto en una fila, mantener posición, bloqueo enemigo, privacidad del destino enemigo, grupos mixtos de 50 unidades de ambas facciones, esquina del mapa, cruces en sentidos opuestos, corredor de una celda, edificio que corta una ruta, rampas y doce recolectores sobre un mismo depósito. En cada paso de esos escenarios se comprueban separación entre cuerpos, terreno transitable y velocidad máxima, incluso al empujar.
- **8/8 partidas entre IA terminadas**, cuatro cruces de facciones × dos dificultades del rival: 97 comprobaciones totales. Duración de 983 a 1226 segundos simulados (16,4–20,4 minutos). La posición 0 ganó 4 escenarios y la posición 1 ganó 4; estas ocho partidas deterministas confirman finalización, no equilibrio competitivo.
- **Build desktop y prueba gráfica aprobados**, con movimiento efectivo, interacción del panel y cero errores registrados. Captura `TestResults/Desktop/03-command-panel.png` inspeccionada para comprobar los cuerpos actualizados.
- **APK Android ARM64/IL2CPP compilado**, versión **0.1.2 (3)**, 58.776.225 bytes. Unity CLI no reportó errores del Editor; firma APK v2 verificada y permisos MR conservados.

APK de esa entrega: `Builds/Quest/FrontierCommand.apk`. SHA-256: `9043ba8eff06bd573e2794f6e5a13b98f7df2d8afb435ab667d1e17530f157af`.

Evidencias: `TestResults/navigation-final.log`, `matches.json`, `unity-navigation-desktop.log`, `unity-navigation-quest.log`, `Desktop/smoke-result.json` y `delivery.json`. La entrega 0.1.1 queda registrada en `delivery-0.1.1.json`.

Pendiente en Quest: comprobar grupos grandes en rampas y entre edificios, trabajadores cruzándose al recolectar, legibilidad de los cuerpos y rendimiento con 100 unidades. Los resultados locales no certifican 72 FPS en el visor.

## Ajustes visuales — 0.1.1 — 15 de octubre de 2026

- Menú elevado respecto de los ojos y del tablero, barra de arrastre con gatillo, ajuste de distancia, recuperación desde el stick secundario y posición guardada localmente. Mantiene la posición respecto del tablero al hacer zoom y gira para leerse desde otros lados.
- Selección mediante cuatro franjas horizontales, a una altura común apenas sobre la cota más alta del terreno. No usa líneas orientadas hacia la cámara ni paredes verticales; el contorno permanece continuo sobre los desniveles.
- Solicitud de modo MR sin límite contextual mediante `OVRManager.shouldBoundaryVisibilityBeSuppressed`, exclusivamente mientras el passthrough está inicializado, activo, visible y opaco. El manifiesto incluye `com.oculus.permission.BOUNDARY_VISIBILITY`; no se cambia la configuración global del visor. Se conserva el anclaje de sesión aunque falle el guardado persistente.

Verificación local: **65 comprobaciones del núcleo aprobadas**, compilación desktop con Unity CLI y prueba gráfica con cero errores. La prueba acciona el panel, arrastra el menú, conserva su posición al trasladar/rotar el tablero y hacer zoom, apunta a una misma coordenada desde cuatro lados y verifica desplazamiento efectivo de un trabajador. Capturas inspeccionadas en `TestResults/Desktop/01-setup.png`, `03-command-panel.png`, `04-flat-selection.png` y `05-orbit-0.png` a `05-orbit-3.png`.

La prueba automatizada desactiva la entrada física de mouse mientras envía sus rayos al panel, para que la actividad del escritorio no modifique la selección durante la comprobación.

APK de esa entrega: `Builds/Quest/FrontierCommand.apk`, **58.773.861 bytes**, versión **0.1.1 (2)**, ARM64/IL2CPP. Unity CLI informó compilación correcta y cero errores del Editor; firma APK v2 verificada. El manifiesto empaquetado incluye passthrough y `BOUNDARY_VISIBILITY`.

SHA-256: `6da8ceffc166cb6051aaba7db51ad4a924c4df1b7ff4276c5f807281aa6153be`.

Pendiente en Quest: probar mesa y piso, mover el menú con ambos mandos, cerrar/abrir para recuperar su posición, caminar alrededor y cruzar el antiguo círculo estacionario, volver del menú del sistema y verificar el contorno con ambos ojos. La [API oficial de Meta](https://developers.meta.com/vr/documentation/unity/unity-boundaryless/) deja la decisión de suprimir el límite al sistema; un build y una prueba desktop no confirman ese comportamiento físico.

## Resultado local inicial — 0.1.0 — 15 de octubre de 2026

| Comprobación | Resultado |
|---|---|
| Núcleo C# | 65 comprobaciones funcionales aprobadas. |
| IA contra IA | 20/20 partidas terminadas; 85 comprobaciones totales con invariantes. |
| Cobertura de partidas | Cuatro cruces de facciones × dos dificultades del rival; ocho escenarios deterministas repetidos. |
| Duración simulada | Entre 859 y 1.208 segundos, aproximadamente 14,3–20,1 minutos. No mide duración de partidas humanas. |
| Carga Unity | `rift` y `training` aceptados por el cargador real de Unity. |
| Build desktop | Compilación macOS ARM64 correcta, sin excepciones en el registro final de compilación. |
| Ejecución desktop | Inicio pulsando el panel, conservación de pausas externas, selección, zoom y desplazamiento efectivo de un trabajador; cero errores de ejecución. Registro de inicio completo comprobado. |
| APK | Android ARM64/IL2CPP, Unity 6000.3.26f1, compilación correcta. Firma APK v2 verificada con `apksigner`. |
| Quest físico | No ejecutado: ADB no detectó ningún visor conectado. |

APK: `Builds/Quest/FrontierCommand.apk`, 58.763.977 bytes. Paquete `com.alvaro.frontiercommand`, versión 0.1.0 (1), Android mínimo 32 y objetivo 36. El manifiesto declara Quest 3, passthrough obligatorio y acceso a escena y anclas. No solicita cámara ni micrófono Android.

SHA-256: `94a6cecb9402c7d4714da8f87acfde41f1b1a4b388f91cc2bf370e7fc28cc896`.

Evidencias reproducibles en `TestResults/matches-run.log`, `matches.json`, `unity-ValidateContent.log`, `unity-BuildDesktop.log`, `unity-BuildQuest.log`, `desktop-player.log` y `Desktop/smoke-result.json`. Los resultados y binarios están excluidos de Git; los scripts, datos, fuentes y configuraciones permiten regenerarlos.

**Hallazgo de balance:** tras despejar salidas de producción mediante puntos de reunión y diferenciar las decisiones tecnológicas de la IA fácil, la batería final produjo cinco victorias de la posición 0 y quince de la posición 1. Ambas facciones ganaron desde la posición 0. Los escenarios se repiten de forma determinista: esa distribución no es una estimación estadística. Sigue pendiente ajustar la ventaja de posición y comprobar que la dificultad fácil resulte más accesible en partidas humanas; no se declara equilibrio competitivo.

### Núcleo C#

Se ejecutan comprobaciones independientes para ambos mapas, versiones inválidas, IDs duplicados, economía inicial, límites de propiedad, objetivos ocultos, cancelación y devolución, secuencias repetidas, pausa, recolección finita, rampas y acantilados, reserva de suministro, producción, victoria, construcción de ambas facciones, tecnología, mejoras únicas, extractores, catalizador, salidas bloqueadas y memoria bajo niebla de guerra.

Las partidas entre IA verifican invariantes de recursos y población y registran duración y resultado en `TestResults/matches.json`. Son escenarios deterministas repetidos; veinte ejecuciones no representan veinte estrategias humanas independientes. El balance y la diversión requieren pruebas humanas.

### Unity y escritorio

Los comandos reproducibles y las rutas de resultados están en la [guía de desarrollo](development.md). Las capturas de la prueba gráfica se guardan en `TestResults/Desktop`: configuración, partida y panel contextual del trabajador.

El build desktop excluye la precarga de ajustes OpenXR sin runtime y los ajustes de simulación exclusivos del editor. Usa ARM64 porque MRUK 203 distribuye su biblioteca macOS standalone para Apple Silicon. Se corrigió también la copia repetida del archivo generado `RuntimeActionBindings.json` al recompilar. Los paquetes de terceros no fueron modificados.

### Quest: comprobaciones pendientes de dispositivo

No había un visor conectado en la consulta inicial de ADB. Requiere comprobación física:

1. Instalar y abrir el APK en Quest 3 sin PC conectada después de instalarlo.
2. Probar passthrough, permisos concedidos y denegados, mesa y piso, colocación manual y superficie detectada.
3. Confirmar precisión de selección, arrastre, construcción, comandos contextuales, vibración y modo zurdo.
4. Reiniciar la aplicación y recuperar el ancla. Probar también ancla no disponible y otra habitación.
5. Quitar y poner el visor, abrir el menú del sistema, perder seguimiento y confirmar una recuperación sin avance de la partida pausada.
6. Completar una partida con cada facción y dificultad, ganar, perder y jugar revancha.
7. Mantener carga de 100 unidades y 40 edificios durante 30 minutos, medir CPU/GPU, cuadros perdidos y temperatura. Una partida normal no garantiza esa carga durante toda la medición.
8. Dar el visor a una persona ajena al desarrollo y comprobar que coloca el tablero y termina una partida sin ayuda.

`FrameMetrics` guarda informes locales en `Application.persistentDataPath/Reports`. Los tiempos CPU/GPU se marcan -1 cuando el runtime no los ofrece. Los informes excluyen pausas y los primeros cinco segundos de simulación. El indicador `framesOverBudget` cuenta intervalos mayores a 1,5 veces el período de 72 Hz; no equivale a una medición del compositor de Quest. Completar el perfil con las herramientas del dispositivo.

## Límites de esta entrega

- Arte geométrico provisional; audio de confirmación básico.
- Balance inicial y ventaja de posición pendientes de ajuste. La batería de navegación está aprobada localmente; falta evaluar tráfico y legibilidad con el visor.
- Sin guardado/restauración de partidas; el ancla se guarda localmente.
- Sin servicios multijugador ni editor visual todavía.
- No se puede declarar el MVP validado para Quest hasta completar la prueba física y de rendimiento anterior.
