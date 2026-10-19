# Guía de desarrollo

## Convenciones del proyecto

- Mantener el `README.md` de la raíz centrado en introducción, requisitos, instalación, configuración y uso básico.
- Documentar avances, cambios y decisiones en `docs/`. Usar [arquitectura](architecture.md) para responsabilidades y decisiones técnicas, [mapas](maps.md) para contratos de datos y [validación](validation.md) para resultados, evidencias y pendientes. Crear otro documento aquí cuando el tema lo necesite.
- Mantener los controles y las mecánicas actuales en la [guía de juego](gameplay.md).
- Las ruedas de acciones y grupos son exclusivas de VR. Desktop se mantiene por ahora como herramienta de desarrollo para probar las mecánicas con mouse, teclado y paneles; no requiere reproducir los gestos de los mandos ni ofrecer una experiencia de producto terminada. Ambas entradas comparten las mismas reglas de simulación.
- El juego usa inglés como único idioma por ahora: interfaz, controles, avisos, tutorial, nombres de facciones/unidades/edificios/mejoras y títulos de mapas. Otros idiomas quedan para una etapa posterior; mantener los identificadores de contenido estables e independientes de los nombres visibles. La documentación de desarrollo puede continuar en español.
- Realizar un commit local por tarea. Nunca hacer push al repositorio.
- Compilar el APK únicamente cuando el usuario lo pida explícitamente; no generarlo automáticamente después de cada cambio.
- Elegir comprobaciones acordes con lo modificado. Distinguir revisión de fuentes, compilación, pruebas de simulación, ejecución en escritorio y validación física en Quest. Registrar qué se verificó y qué queda pendiente.

## Entorno y estructura

La instalación, configuración y generación de ejecutables se describen en la [guía inicial](../README.md). Ejecutar los comandos de este documento desde la raíz del proyecto.

Usar Unity **6000.3.26f1** con las dependencias fijadas en `Packages/manifest.json` y `Packages/packages-lock.json`. Durante la validación inicial, Unity 6.5 produjo errores de compatibilidad con Meta XR 203; ese resultado está separado del uso de su SDK .NET para comprobar el núcleo.

La escena `Assets/Frontier/Scenes/Frontier.unity` crea sus componentes en ejecución mediante `Bootstrap`. Las responsabilidades y contratos se describen en [arquitectura](architecture.md); el formato y la validación de mapas, en [mapas](maps.md).

## Comprobaciones

```sh
bash scripts/check-core.sh
bash scripts/check-core.sh --caster-only
bash scripts/check-core.sh --faction-parity-only
bash scripts/check-core.sh --group-production-only
bash scripts/check-core.sh --ai-only
bash scripts/check-core.sh --ai-expansion-only
bash scripts/check-core.sh --matches --count=20
bash scripts/check-core.sh --practice --matches --opponent=hard --count=4
bash scripts/unity.sh validate
```

El núcleo se prueba con .NET 8 sin dependencias NuGet. El script busca `dotnet` o el SDK incluido en la instalación local de Unity 6.5; se puede indicar otro ejecutable mediante `DOTNET`.

`--faction-parity-only` ejecuta el contrato de igualdad mecánica del catálogo y las regresiones de construcción atendida, pausa al retirar/perder al constructor, reparación y ausencia de regeneración pasiva en ambas facciones. Compara también simulaciones con las mismas posiciones y órdenes al intercambiar las identidades de facción. Forma parte de la batería general; los nombres, IDs y modelos pueden diferir, los parámetros y resultados de juego equivalentes deben coincidir.

Para comprobar los controles y la ayuda de ambas facciones sin generar un ejecutable:

```sh
unity run . --editor-version 6000.3.26f1 --log-file TestResults/unity-faction-parity.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.FactionParityValidationBatch.Run
```

Esta batería valida construcción desde el menú, asignación y liberación del constructor, reanudación contextual y reparación encolada. Recorre además ayuda, producción, recolección, rueda y menús, comprobando que el texto quepa en los paneles.

`--group-production-only` comprueba producción con varios edificios seleccionados: una compra por orden, reparto por tiempo pendiente, colas llenas, productores incompatibles/incompletos y las mismas restricciones de recursos, suministro e investigación. Para verificar selección doble, guardado/recuperación de barracas, acciones y resumen de producción en Unity:

```sh
unity run . --editor-version 6000.3.26f1 --log-file TestResults/unity-building-groups.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.BuildingGroupValidationBatch.Run
```

Las comprobaciones de dificultad cubren igualdad inicial en ambos mapas y los cuatro cruces de facciones, acceso al catálogo, reservas, presupuesto, defensa, preparación y habilidades. `bash scripts/check-core.sh --ai-only` ejecuta las pruebas de economía, táctica y perfiles. `--ai-expansion-only` comprueba desarrollo real de bases, exploración y tecnología durante doce minutos simulados. `--ai-only --practice` añade doce simulaciones contra un jugador pasivo, que registran producción, población, primera ofensiva, daño y órdenes rechazadas en `TestResults/difficulty-practice.json`. `--matches --opponent=hard --count=4` enfrenta Normal contra Hard en los cuatro cruces de facciones; también admite Easy y Normal.

Para comparar versiones de la IA bajo las mismas reglas, `bash scripts/check-ai.sh --difficulty=all --limit=6 --output=TestResults/ai-first-six.json` ejecuta inicialmente los tres niveles en ambos mapas. El script usa una copia de la estrategia anterior en `TestResults/ai-before/LegacyStrategist.cs` o la ruta indicada con `--baseline=/ruta/LegacyStrategist.cs`. Esa copia es un artefacto local de comparación y no forma parte del juego. `--difficulty=Hard --limit=16` amplía a los cuatro cruces de facciones y ambos lados de cada mapa. Registra hashes de fuentes/catálogo, daño real, pérdidas, actividad productiva, exploración y resultados; los empates por tiempo se informan como tales. Estas simulaciones no sustituyen el balance con jugadores humanos.

`--difficulty=all --limit=48 --development` ejecuta la matriz completa y doce escenarios económicos de doce minutos. Los escenarios económicos desactivan el daño para observar expansiones aunque la ofensiva ganaría antes; se registran aparte de las victorias competitivas. `--opponent=current --difficulty=Hard --legacy-difficulty=Normal --pair=mirrors --limit=8` compara los niveles actuales en ambos mapas, lados y facciones. Cada ejecución compila una copia congelada de fuentes y datos en `TestResults/ai-snapshots`, conservando sus hashes para que los resultados no se confundan con cambios posteriores.

Incluye regresiones de navegación: 60 unidades de ambas facciones, destinos en una esquina, tráfico opuesto, aliados quietos, mantener posición, bloqueo enemigo, edificios nuevos, rampas y doce trabajadores en el mismo recurso. En cada paso verifican separación, límites de velocidad y permanencia sobre terreno transitable.

`PathfindingChecks` añade grupos mixtos por corredores largos, selecciones solapadas, unidades estacionadas junto a una salida y convoyes de distintas velocidades. `CollisionChecks` comprueba masa, empujes, aceleración, frenado, Hold y nuevas órdenes durante una cesión. Parámetros y decisiones: [movimiento](movement.md).

`CombatMotionChecks` verifica frenado en Attack, Attack Move y persecución automática, continuidad durante cooldown, alcance/visión después de frenar y excepciones de Hold y torres.

`CombatBehaviorChecks` cubre prioridades de amenazas, defensores que llegan durante un asedio, foco explícito, adquisición después de una baja, estabilidad del objetivo, recarga, continuación de rutas/colas, retirada, cesión de paso durante combate, objetivos inaccesibles, búsqueda de línea de tiro y pérdida de visión. Incluye grupos de 18 soldados mixtos con una sola orden, y selecciones dispersas de 18 y 60 soldados cuyo foco muere antes de que llegue la retaguardia. Ambas baterías se pueden ejecutar con `bash scripts/check-core.sh --combat-only`. `QueuedInputChecks.Run` verifica además cinco recorridos de ataque con 18 unidades en ambas facciones: orden directa, doble A, menú confirmado con A, menú con doble A y menú confirmado con gatillo seguido de A.

`Frontier.Editor.AttackPointerPreview.Render` comprueba y renderiza la mira roja y el color del rayo para doble A y Attack Move desde el panel, incluida confirmación, cancelación, destino rechazado, pausa, expiración del feedback y cambio a Move/construcción. Guarda ocho vistas en `TestResults/AttackPointer` y ejecuta las regresiones `QueuedInputChecks.Run` y `ActionWheelPreview.Render`; esta última confirma además un destino con A después de cerrar la rueda. `CombatBehaviorChecks` verifica que una sola orden elimine dos enemigos sucesivos y una estructura antes de llegar al destino.

`CasterChecks` forma parte de la batería general y puede ejecutarse sola con `bash scripts/check-core.sh --caster-only`. Usa construcción y producción reales de la academia para ambas facciones y verifica energía inicial/regeneración/tope, pausa, recarga, daño a tropas/estructuras, curación limitada sin edificios, selección mixta de un solo caster, acercamiento con alcance/línea de visión, recuperación de Move/Attack Move/Attack/Stop/Hold y conservación de la cola. Comprueba también que un caster en Hold ejecute en orden los destinos agregados mientras prepara el lanzamiento. Incluye cancelación por nueva orden, pérdida de visión, muerte, imposibilidad de llegar, datos inválidos/roles obligatorios, privacidad de energía/recargas y eventos, y exclusión de enemigos ocultos del área de daño. Las variantes inválidas del catálogo son copias; no modifican el catálogo compartido de otras pruebas.

Para comprobar selección, panel/rueda, apuntado y efectos de habilidades sin generar un jugador:

```sh
unity run . --editor-version 6000.3.26f1 --log-file TestResults/unity-caster-abilities.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.CasterAbilityPreview.Render
```

`CasterAbilityPreview.Render` usa rayos y pulsaciones sintéticas contra las acciones reales: producción desde la academia, energía en selección mixta, habilitación desde panel/rueda, A/gatillo, segundo A rápido, área/rango, destinos bajo niebla, pausa, cancelación y cambio de selección. Guarda vistas en `TestResults/CasterControls`. También llama a `CasterVisualPreview.Render` para modelos de Conduit/Oracle y Resonance Lab/Spore Sanctum, `AbilityEffectsPreview.Render` para explosiones/curación y `AttackPointerPreview.Render` para las regresiones de ataque. Los resultados de una ejecución concreta se registran en [validación](validation.md); estos comandos y capturas de Editor no certifican mandos físicos, ergonomía ni rendimiento en Quest.

`RallyPointChecks` comprueba asignación, actualización y borrado por edificio, validación de productores, privacidad y despliegue de unidades con rutas/salidas bloqueadas. La batería de Editor para puntos de reunión incluye gestos, banderas, HUD y regresiones de producción y colas:

```sh
unity run . --log-file TestResults/unity-rally-point.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.RallyPointValidationBatch.Run
```

Las capturas de `RallyPointPreview` quedan en `TestResults/RallyPoint`. `RallyPointInputChecks.Run` comprueba A/grip/doble A, limpieza por consola y rueda, y orientación al desplegar. No genera APK ni ejecutable desktop.

`ControlGroupPreview.RunValidation` prueba los cuatro grupos dentro de la rueda unificada: entrada directa por inclinación, recuperación con gatillo sin recentrar, asignación desde abajo con gatillo, guardado alternativo al soltar clic sostenido + dirección y cancelaciones, además de las regresiones de hint, acciones, gestos de colas, rally, menús y HUD:

```sh
unity run . --log-file TestResults/unity-control-groups.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ControlGroupPreview.RunValidation
```

`ControlGroupPreview.Render` ejecuta solo el escenario focal y guarda sus capturas en `TestResults/ControlGroups`. Las entradas son sintéticas; no genera APK.

`MenuFlowPreview.Render` verifica la pantalla Hotkeys y la guía de pausa: ambos dispositivos, todas las categorías y páginas, IDs únicos, texto sin recortes, navegación sin superposición, simulación congelada y conservación de selección, recursos, grupos y modo de construcción. Las categorías consultan las asignaciones del perfil seleccionado. Genera vistas y la referencia `TestResults/Menus/default-controls.md` desde `DefaultControlCatalog`, sin construir un jugador:

```sh
unity run . --log-file TestResults/unity-hotkeys-final.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.MenuFlowPreview.Render
```

`HotkeyValidationBatch.Run` comprueba persistencia, perfiles corruptos, conflictos e intercambio confirmado, captura y cancelación, restauración por dispositivo, acceso desde preparación/pausa y entrada desktop sintética contra el enrutador real. Comprueba también selección de mano persistente, nombres A/B y X/Y y filtrado del mando inactivo mediante estados sintéticos del SDK de Meta. También ejecuta las regresiones de menús, colas y grupos. Guarda vistas en `TestResults/Hotkeys` y conserva las preferencias previas del usuario:

```sh
unity run . --editor-version 6000.3.26f1 --log-file TestResults/unity-hotkeys.log --no-tail -- \
  -executeMethod Frontier.Editor.HotkeyValidationBatch.Run
```

Estas comprobaciones no certifican los botones físicos ni la ergonomía en Quest; requieren una prueba en el visor.

`CommandQueueChecks` verifica FIFO, reemplazo inmediato, conversión del doble A, grupos, privacidad, invalidación de destinos, recolección, reparación y construcción. Para comprobar entrada y presentación sin APK:

```sh
unity run . --log-file TestResults/unity-queued-input.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.QueuedInputChecks.Run
unity run . --log-file TestResults/unity-order-plan.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.OrderQueuePreview.Render
```

La batería completa de esta entrega se reproduce con `Frontier.Editor.OrderQueueValidationBatch.Run`: reúne estos fixtures y las regresiones de orientación, selección externa, recursos, producción, ayuda, menús y rueda en una ejecución del Editor.

El primer fixture usa rayos y flancos sintéticos para los gestos y el segundo guarda capturas en `TestResults/OrderQueue`, comprueba la separación de producción y la limpieza de los destinos. No certifican ergonomía ni seguimiento en Quest. Contratos: [colas de acciones](order-queues.md).

Para comprobar el giro visual sin generar un ejecutable:

```sh
unity run . --log-file TestResults/unity-movement.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.UnitFacingPreview.Render
```

`ProductionChecks` verifica cancelación por identidad del pedido, reembolso de minerales/gas, liberación de suministro, duplicados, avance de la cola, investigación, propiedad, pausa y órdenes obsoletas. Para comprobar además las tarjetas reales, hover rojo y entrada por A/gatillo sin generar un ejecutable:

```sh
unity run . --log-file TestResults/unity-queue-cancel.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ProductionCancelPreview.Render
```

Genera vistas en `TestResults/ProductionCancel` y ejecuta también las regresiones de ayuda, producción, HUD, menús y rueda. Los rayos y las pulsaciones son sintéticos; la comodidad de uso se comprueba en Quest.

Prueba gráfica automatizada, después de generar el ejecutable de escritorio:

```sh
bash scripts/smoke-desktop.sh
```

Esta prueba acciona el botón de inicio mediante su superficie real, comprueba movimiento efectivo y guarda capturas. También arrastra el menú, verifica que siga ligado al tablero al reubicarlo y hacer zoom, prueba apuntar al mismo lugar desde cuatro lados y captura la selección sobre suelo, rampas y alturas. Revisa el registro completo para detectar fallos anteriores al inicio de los scripts. No sustituye una prueba humana ni una prueba de Quest.

Para revisar las siluetas sin generar un ejecutable, usar **Frontier → Preview unit roles** en Unity, o:

```sh
unity run . --log-file TestResults/unity-unit-roles.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.UnitVisualPreview.Render
```

Guarda vistas aliadas, enemigas y cenitales en `TestResults/UnitRoles`. De izquierda a derecha: trabajador, explorador, cuerpo a cuerpo, tirador y artillería; fila cercana industrial y fila lejana biológica.

Para revisar los contadores y la interacción de recolección sin generar un ejecutable, usar **Frontier → Preview harvesting panel**, o:

```sh
unity run . --log-file TestResults/unity-harvesting.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HarvestingPreview.Render
```

Comprueba las órdenes contextuales, selección de recursos, totales de base, exceso y órdenes sobre el extractor. Renderiza cinco estados del panel en `TestResults/Harvesting`. Son vistas estáticas del panel real desde el Editor; no certifican legibilidad en el visor. `HarvestingChecks` forma parte de `check-core.sh` y comprueba reparto, reservas, agotamiento, gas, carga y niebla de guerra para ambas facciones. Se puede ejecutar por separado con `bash scripts/check-core.sh --harvesting-only`; incluye el agotamiento completo en ambos mapas, recursos remotos explorados y desconocidos, entrega de la última carga, trabajadores que permanecen inactivos, producción en una base agotada y traslado manual a otra zona.

Para comprobar los contadores persistentes sobre el tablero, usar **Frontier → Preview workforce counters**, o:

```sh
unity run . --log-file TestResults/unity-workforce-markers.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HarvestingMarkersPreview.Render
```

Usa órdenes reales de recolección para comprobar los totales de minerales y gas, color de exceso, viajes y entrega, agotamiento, pausa, destrucción y salida de partida. Renderiza ambas facciones desde varios lados y en tablero compacto en `TestResults/Workforce/`. Comprueba orientación, separación de etiquetas y tamaño físico; no genera ejecutables ni sustituye la prueba de legibilidad en Quest.

Para comprobar el estado de construcción y la selección persistente del edificio a colocar, usar **Frontier → Check construction feedback**, o:

```sh
unity run . --log-file TestResults/unity-construction.log --no-tail -- \
  -executeMethod Frontier.Editor.ConstructionPreview.Run
```

Verifica ambas facciones: materiales grises, porcentaje y disponibilidad, transición a edificio terminado, niebla y limpieza de etiquetas; también selección de barraca/relay, cambio, colocación y cancelación desde las acciones compartidas con la rueda. Genera capturas en `TestResults/Construction/` y ejecuta las regresiones de contadores y rueda. `HarvestingChecks` comprueba además que el constructor recoge minerales en la nueva zona y entrega tres cargas en la expansión terminada.

Para revisar los textos en inglés, usar **Frontier → Preview full command panel**, o:

```sh
unity run . --log-file TestResults/unity-english.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HarvestingPreview.RenderEnglish
```

Incluye los cinco estados de recolección y añade inicio en ambos mapas, trabajadores y tutorial de cada facción, colas de unidades y mejoras, selección mixta con capacidad completa, unidad dañada, construcción, pausa y final de partida. Comprueba que las etiquetas entren en su altura disponible y guarda dieciocho vistas estáticas en `TestResults/English`, sin generar ejecutables.

Para revisar específicamente el panel de partida, usar **Frontier → Preview match HUD**, o:

```sh
unity run . --log-file TestResults/unity-match-hud.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HarvestingPreview.RenderHud
```

Ejecuta los mismos dieciocho estados y guarda las vistas en `TestResults/MatchHud`. Comprueba nombres y contadores respecto de `PlayerView`, separación de controles, capacidad completa y, mediante rayos sobre los botones reales, producción/cancelación con devolución, elección de construcción y guardado/recuperación de un grupo de diez unidades. Son comprobaciones del panel del Editor, sin partida interactiva ni seguimiento físico.

Para revisar la cola de producción, usar **Frontier → Preview production queue**, o:

```sh
unity run . --log-file TestResults/unity-production-queue.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ProductionQueuePreview.Render
```

Genera dieciséis vistas en `TestResults/ProductionQueue` para ambas facciones: cola vacía, trabajador a mitad de producción, cinco órdenes, siguiente unidad, investigación, pausa y esperas por salida/suministro. Usa rayos contra los botones reales y pasos de simulación para comprobar orden, cancelación/devolución y avance. Los escenarios de espera preparan ocupación o pérdida de suministro antes de ejecutar el paso de producción. Comprueba también ocultación al cambiar selección/menú y ejecuta las regresiones de `HarvestingPreview.RenderHud` y `MenuFlowPreview.Render`. No genera APK ni ejecutable desktop.

Para comprobar navegación entre menús y ciclo de partida, usar **Frontier → Preview and check menu flow**, o:

```sh
unity run . --log-file TestResults/unity-menus.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.MenuFlowPreview.Render
```

Ejecuta rayos contra los botones reales y comprueba inicio, configuración, selección de las tres dificultades, creación de la IA elegida, pausa, regreso, limpieza de partida, conservación de colocación y pausas externas, nueva partida y revancha. Comprueba además las instrucciones Touch sin navegación del mapa, el HUD Quest sin `Full map` y el botón de pausa ampliado. Guarda doce vistas estáticas en `TestResults/Menus`. Restaura la preferencia de mano que usa durante la comprobación y no genera ejecutables. El escenario de prueba desktop también entra ahora por **New Match** antes de **START MATCH**.

Quest conserva la configuración confirmada del tablero; los sticks solo ajustan el mapa durante su colocación. Los controles WASD/rueda/F y las vistas de prueba con zoom permanecen disponibles en escritorio. Las previsualizaciones del Editor no validan por sí solas la lectura física de los mandos ni el anclaje en Quest.

Para inspeccionar el hover en el tablero real desde el Editor, usar **Frontier → Preview hover**, o:

```sh
unity run . --log-file TestResults/unity-hover.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HoverPreview.Render
```

Guarda siete vistas en `TestResults/Hover`: unidad y edificio con/sin hover, edificio seleccionado al apuntar y retirar el puntero, y enemigo resaltado. Usa `BoardView` y su shader en una vista estática; no genera ejecutables ni valida interacción física en Quest.

Para revisar el marcador de intersección del cursor, usar **Frontier → Preview map pointer**, o:

```sh
unity run . --log-file TestResults/unity-pointer.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.PointerPreview.Render
```

Guarda ocho vistas en `TestResults/Pointer`: suelo, rampa, altura, selección, borde, zoom con tablero girado, niebla y cursor retirado. Comprueba rayos contra el terreno real, coincidencia del punto de impacto tras zoom/desplazamiento/giro, tamaño físico, rechazo de paredes y limpieza al pausar/ocultar/salir. La línea dibujada en estas vistas representa un rayo de ejemplo; no valida mandos físicos ni genera ejecutables.

Para revisar selección desde fuera del tablero, usar **Frontier → Preview selection from outside board**, o:

```sh
unity run . --log-file TestResults/unity-edge-selection.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.EdgeSelectionPreview.Render
```

Ejecuta rayos mediante el mismo procesamiento de selección usado por mandos y mouse. Verifica los cuatro bordes, esquina, liberación fuera, cruce completo, clic externo, selección individual/aditiva, zoom/giro, cancelación y ausencia de órdenes/construcciones externas. Guarda seis vistas estáticas en `TestResults/EdgeSelection`, sin generar ejecutables.

Para revisar la señal de daño recibido, usar **Frontier → Preview received damage**, o:

```sh
unity run . --log-file TestResults/unity-hits.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HitFeedbackPreview.Render
```

Guarda ocho vistas en `TestResults/Hits`: antes, impactos simultáneos, trabajador, soldado seleccionado/bajo hover, edificios de ambas facciones, desvanecimiento y final del efecto. Comprueba renovación sin acumular objetos, limpieza al reutilizar una unidad, niebla y salida de partida. Las comprobaciones del núcleo también verifican impactos reales en unidades/edificios, daño de área, recuperación de vida y privacidad de edificios recordados.

Para revisar los disparos y sus trayectorias, usar **Frontier → Preview weapon fire**, o:

```sh
unity run . --log-file TestResults/unity-weapons.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.WeaponPreview.Render
```

Guarda once vistas en `TestResults/Weapons`, muestreando distintos momentos de destello, vuelo e impacto de ambas facciones, artillería, combate cercano y torres. Comprueba orientación, objetivo destruido, pausa, niebla, caducidad, presupuesto de 192 efectos, reutilización de objetos y limpieza al ocultar/salir/reiniciar. El núcleo comprueba por separado los ataques reales y su proyección segura. Son vistas estáticas del Editor, sin APK ni validación de mandos o rendimiento del visor.

Para revisar las siluetas de edificios, usar **Frontier → Preview building roles**, o:

```sh
unity run . --log-file TestResults/unity-building-roles.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.BuildingVisualPreview.Render
```

Guarda diez vistas en `TestResults/BuildingRoles`: galería aliada/enemiga, vista superior/opuesta, comparación entre base central y barraca de ambas facciones, recuerdo bajo niebla, construcción/daño, torre disparando y zoom/giro. Los rótulos son anotaciones de la galería y no se añaden al juego. Comprueba huellas, posición de vida/impactos, restauración de materiales al recuperar visión, orientación de torres y reutilización. También ejecuta las revisiones existentes de daño y armas porque comparten la representación de entidades. No genera ejecutables.

## Resultados y entregas

Guardar registros, capturas e informes locales en `TestResults/` y los ejecutables en `Builds/`. Ambos directorios están excluidos de Git. Conservar en [validación](validation.md) las conclusiones, las rutas de evidencia y las limitaciones de cada entrega; indicar si los cambios de fuentes aún no están incluidos en el APK disponible.

## Próximas etapas

El multijugador por internet y el editor de computadora son etapas posteriores. El núcleo ya separa órdenes, estado autoritativo, información visible y colocación física. No se han conectado servicios de Relay ni creado cuentas de infraestructura. Los criterios de integración están en [arquitectura](architecture.md).

## Rueda unificada Touch

Para comprobar el enrutamiento de entrada y renderizar las ruedas de ambas facciones sin generar un ejecutable:

```sh
unity run . --log-file TestResults/unity-unified-wheel.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.UnifiedWheelValidationBatch.Run
```

La prueba alimenta `CommanderInput.RouteWheel` con cuadros sintéticos de joystick/A/B/gatillo/grip y un reloj controlado, pasa las entradas no consumidas a `PointBoard` y verifica los comandos reales. Comprueba el hint de nivel 0, entrada directa en las cuatro categorías con la dirección ya resaltada, recorridos entre niveles sin centrar, una confirmación por pulsación de gatillo, cierre tras 0,8 s, dirección sostenida, vuelta/cierre, ausencia de apertura por clic o doble grip y consumo de botones hasta liberarlos. Incluye recuperación y guardado de grupos, edificios y rally compartido, construcción, producción, investigación, ataque y habilidades con energía/recarga. Cubre cambios de selección/cola, foco, seguimiento, pausa, colocación y cambio de mano. El batch añade compatibilidad de hotkeys y regresiones de menús, selección y puntero. Guarda vistas en `TestResults/UnifiedWheel/`, `ActionWheel/` y `ControlGroups/`, además de las carpetas de los fixtures relacionados. El panel comparte `SelectionActions`. Las entradas sintéticas no emulan los mandos físicos: tamaño, postura y comodidad deben confirmarse en Quest.

Las vistas `UnifiedWheel/*-hand-hint.png` y `*-hand-groups.png` conservan escala, posición y giro reales frente a una cámara fija. El marcador amarillo identifica el origen del rayo únicamente en la previsualización. Las comprobaciones verifican ambos mandos, tamaño físico compacto y anclaje independiente de los movimientos de cabeza para cada sección de la rueda.

## Apuntado de minerales

Para inspeccionar los modelos de recursos, ejecutar **Frontier → Preview resource models** o `unity run . --log-file TestResults/unity-resource-models.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ResourceVisualPreview.Render`. Guarda diez vistas en `TestResults/ResourceModels/`, incluyendo cristales, gas desde arriba, vapor en dos momentos, hover, tablero compacto y extractor. Comprueba el ciclo visual del vapor y usa una vista preparada para mostrar los modelos; no genera un ejecutable. Después de cambiar las siluetas, `ResourcePickingPreview.Render` verifica el apuntado con rayos y órdenes reales.

`Frontier.Editor.ResourcePickingPreview.Render` comprueba la resolución de cristal/suelo, interacción con trabajadores cercanos, clic con temblor y órdenes de recolección; renderiza los dos mapas a distintos tamaños. Ejecutar con `unity run . --log-file TestResults/unity-mineral-picking.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ResourcePickingPreview.Render`. Las imágenes quedan en `TestResults/ResourcePicking/`. `EdgeSelectionPreview.Render` y `HarvestingPreview.RenderHud` cubren las regresiones de selección y panel. Ninguno genera APK ni ejecutable desktop.

### Cuadrícula de selección

`SelectionGridPreview.Render` verifica rayos y gestos del panel para ambas facciones: selección individual, doble clic limitado al conjunto y a la definición, selección en otras páginas, eliminación mantenida, cancelación y alias A. Incluye el máximo de 60 unidades y 20 edificios, recorrido de las cuatro páginas y capturas y ausencia de superposición con producción. `SelectionGridPreview.RunValidation` añade las regresiones de grupos, rueda, entrada de órdenes/rally, menús y producción. No construye un jugador:

```sh
unity run . --log-file TestResults/unity-selection-grid.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.SelectionGridPreview.RunValidation
```
