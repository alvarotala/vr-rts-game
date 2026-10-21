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

`Frontier.Editor.AttackPointerPreview.Render` comprueba que el doble A/X ejecuta Attack Move sin gatillo usando el `CommanderInput.Update` real, estados OVR sintéticos de ambas manos y el botón de orden reasignado a grip. Verifica Move inmediato al primer toque, ausencia de repetición al mantener, avance de simulación y conservación de selección/orden al soltar. Distingue la mira roja de destino pendiente desde el panel del pulso rojo de una orden aceptada, fijo en el mapa; comprueba expansión, expiración, cursor fuera del tablero, pausa, ocultación, rechazo y reemplazo por Move/Stop. Guarda doce vistas en `TestResults/AttackPointer` y ejecuta las regresiones `QueuedInputChecks.Run` y `ActionWheelPreview.Render`; esta última confirma además un destino con A después de cerrar la rueda. `CombatBehaviorChecks` verifica que una sola orden elimine dos enemigos sucesivos y una estructura antes de llegar al destino. `bash scripts/check-core.sh --combat-only` cubre además persecución con cambio de objetivo, reparto de aproximaciones, puestos de tiro separados y ocupados, foco explícito, niebla y continuidad de las órdenes. Incluye combates activos de 18 y 50 unidades por bando con los cinco roles militares, ambas facciones y ambos puestos de atacante: el defensor también dispara, y se comprueban participación y separación física. `CombatMotionChecks` mantiene las regresiones de aceleración y frenado al entrar en alcance.

`CasterChecks` forma parte de la batería general y puede ejecutarse sola con `bash scripts/check-core.sh --caster-only`. Usa construcción y producción reales de la academia para ambas facciones y verifica energía inicial/regeneración/tope, pausa, recarga, daño a tropas/estructuras, curación limitada sin edificios, selección mixta de un solo caster, acercamiento con alcance/línea de visión, recuperación de Move/Attack Move/Attack/Stop/Hold y conservación de la cola. Comprueba también que un caster en Hold ejecute en orden los destinos agregados mientras prepara el lanzamiento. Incluye cancelación por nueva orden, pérdida de visión, muerte, imposibilidad de llegar, datos inválidos/roles obligatorios, privacidad de energía/recargas y eventos, y exclusión de enemigos ocultos del área de daño. Las variantes inválidas del catálogo son copias; no modifican el catálogo compartido de otras pruebas.

Para comprobar selección, panel/rueda, apuntado y efectos de habilidades sin generar un jugador:

```sh
unity run . --editor-version 6000.3.26f1 --log-file TestResults/unity-caster-abilities.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.CasterAbilityPreview.Render
```

`CasterAbilityPreview.Render` usa rayos y pulsaciones sintéticas contra las acciones reales: producción desde la academia, energía en selección mixta, habilitación desde panel/rueda, A/gatillo, segundo A rápido, área/rango, destinos bajo niebla, pausa, cancelación y cambio de selección. Guarda vistas en `TestResults/CasterControls`. También llama a `CasterVisualPreview.Render` para modelos de Conduit/Oracle y Resonance Lab/Spore Sanctum, `AbilityEffectsPreview.Render` para explosiones/curación y `AttackPointerPreview.Render` para las regresiones de ataque. Los resultados de una ejecución concreta se registran en [validación](validation.md); estos comandos y capturas de Editor no certifican mandos físicos, ergonomía ni rendimiento en Quest.

`AbilityEffectsPreview.Render` también puede ejecutarse directamente para revisar únicamente las magias: cinco fases, vista superior, escenas con unidades de ambas facciones y rampas, en `TestResults/Abilities/`. `AbilityEffects` anima los eventos proyectados por tick, sin estado temporal acumulado: pausa y rebobinado reproducen la misma geometría/opacidad. Ion Burst usa descargas angulares; Spore Burst usa filamentos curvos y esporas. Repair Field combina un perímetro circular con pulso hexagonal y cruces; Mend combina espirales y semillas ascendentes. El daño dura 0,8 s y la curación 0,98 s, dentro del segundo que el núcleo conserva cada evento. Todos comparten malla/material, niebla y recorte, con máximo de 32 eventos y 768 vértices por evento; no se agregan objetos por lanzamiento ni cambian reglas de daño/curación.

`RallyPointChecks` comprueba asignación, actualización y borrado por edificio, validación de productores, privacidad y despliegue de unidades con rutas/salidas bloqueadas. La batería de Editor para puntos de reunión incluye gestos, banderas, HUD y regresiones de producción y colas:

```sh
unity run . --log-file TestResults/unity-rally-point.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.RallyPointValidationBatch.Run
```

Las capturas de `RallyPointPreview` quedan en `TestResults/RallyPoint`. `RallyPointInputChecks.Run` comprueba A/grip/doble A, limpieza por consola y rueda, y orientación al desplegar. No genera APK ni ejecutable desktop.

`ControlGroupPreview.RunValidation` prueba los cuatro grupos dentro de la rueda unificada: entrada directa por inclinación, recuperación con gatillo sin recentrar, asignación inmediata al pulsar stick en Groups, drenaje de pulsación/liberación, centro sin asignación y cancelaciones, además de las regresiones de hint, acciones, gestos de colas, rally, menús y HUD:

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

La batería completa de esta entrega se reproduce con `Frontier.Editor.OrderQueueValidationBatch.Run`: reúne estos fixtures, `AttackPointerPreview` y las regresiones de rally, orientación, selección externa, recursos, producción, ayuda, menús y rueda en una ejecución del Editor.

El primer fixture usa rayos y flancos sintéticos para los gestos y el segundo guarda capturas en `TestResults/OrderQueue`, comprueba líneas activas sin cola, colores distintos por unidad, cambios de selección sin avanzar simulación, separación de producción, limpieza de destinos y guía de controles vigente. No certifican ergonomía ni seguimiento en Quest. Contratos: [colas de acciones](order-queues.md).

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

Para revisar las muertes, usar **Frontier → Preview and check unit deaths**, o:

```sh
bash scripts/check-core.sh --deaths-only
unity run . --log-file TestResults/unity-death-visuals.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.DeathVisualPreview.Run
```

El núcleo verifica bajas reales, eliminación inmediata de la unidad, privacidad, observación tardía, duplicados, pausa, caducidad y cancelación sin cadáver. El preview guarda siete vistas en `TestResults/Deaths`: unidades vivas, caída, cuerpos, disolución y final; cubre los seis roles de ambas facciones, selección a través de los restos, niebla, límites de cuerpos/vértices, ausencia de objetos por muerte y limpieza al reiniciar. Es evidencia de Editor; no mide rendimiento ni percepción física en Quest.

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

La prueba alimenta `CommanderInput.RouteWheel` con cuadros sintéticos de joystick/A/B/gatillo/grip y un reloj controlado, pasa las entradas no consumidas a `PointBoard` y verifica los comandos reales. Comprueba el hint de nivel 0, entrada directa en las tres categorías y ausencia de acción abajo con la dirección ya resaltada, recorridos entre niveles sin centrar, una confirmación por pulsación de gatillo, cierre tras 0,8 s, dirección sostenida, vuelta/cierre, ausencia de apertura por clic o doble grip y consumo de botones hasta liberarlos. Incluye recuperación y guardado de grupos, edificios y rally compartido, construcción, producción, investigación, ataque y habilidades con energía/recarga. Cubre cambios de selección/cola, foco, seguimiento, pausa, colocación y cambio de mano. El batch añade compatibilidad de hotkeys y regresiones de menús, selección y puntero. Guarda vistas en `TestResults/UnifiedWheel/`, `ActionWheel/` y `ControlGroups/`, además de las carpetas de los fixtures relacionados. El panel comparte `SelectionActions`. Las entradas sintéticas no emulan los mandos físicos: tamaño, postura y comodidad deben confirmarse en Quest.

Las vistas `UnifiedWheel/*-hand-hint.png` y `*-hand-groups.png` conservan escala, posición y giro reales frente a una cámara fija. El marcador amarillo identifica el origen del rayo únicamente en la previsualización. Las comprobaciones verifican ambos mandos, cuatro hints con flechas y seis segmentos sin raycast, columna lateral reflejada por mano, anclaje al stick del prefab, altura cercana de la rueda y giro hacia la cámara limitado a 35°. Mover la cabeza no desplaza el anclaje; girarla sin trasladarla no hace girar el menú. `ComfortAudioPreview.Render` añade vistas de ambas manos con el modelo Touch Plus real y con la rueda abierta, en `TestResults/ComfortAudio/*-controller.png` y `*-controller-wheel.png`.

`ComfortAudioPreview.RenderControllerWheel` prepara los modelos/materiales y ejecuta esas vistas más `UnifiedWheelPreview`, sin las regresiones adicionales de menús y ataque del batch completo. La preparación identifica el cuerpo por nombre: no utiliza el primer renderer, porque puede ser el indicador de batería en una de las manos.

El fixture de rueda también registra el envío de vibración mediante controladores sintéticos del SDK: cambio de ítem/nivel, ausencia de pulsos duplicados al sostener o por histéresis, duración, parada por foco/desactivación/cambio de mano y ausencia de vibración desktop. El registro sustituye la salida física; la intensidad percibida se ajusta probando el visor.

## Apuntado de minerales

Para inspeccionar los modelos de recursos, ejecutar **Frontier → Preview resource models** o `unity run . --log-file TestResults/unity-resource-models.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ResourceVisualPreview.Render`. Guarda diez vistas en `TestResults/ResourceModels/`, incluyendo cristales, gas desde arriba, vapor en dos momentos, hover, tablero compacto y extractor. Comprueba el ciclo visual del vapor y usa una vista preparada para mostrar los modelos; no genera un ejecutable. Después de cambiar las siluetas, `ResourcePickingPreview.Render` verifica el apuntado con rayos y órdenes reales.

`Frontier.Editor.ResourcePickingPreview.Render` comprueba la resolución de cristal/suelo, interacción con trabajadores cercanos, clic con temblor y órdenes de recolección; renderiza los dos mapas a distintos tamaños. Ejecutar con `unity run . --log-file TestResults/unity-mineral-picking.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ResourcePickingPreview.Render`. Las imágenes quedan en `TestResults/ResourcePicking/`. `EdgeSelectionPreview.Render` y `HarvestingPreview.RenderHud` cubren las regresiones de selección y panel. Ninguno genera APK ni ejecutable desktop.

La misma prueba compara Bodies y Bases con rayos oblicuos bloqueados por un mineral o edificio, selección real con gatillo, órdenes contextuales, huellas, niebla, agotamiento y recorte. `MenuFlowPreview.Render` comprueba el selector persistente en pausa, el submenú Control guide y el retorno sin reanudar. Ambos conservan y restauran la preferencia de selección del dispositivo.

### Cuadrícula de selección

`SelectionGridPreview.Render` verifica rayos y gestos del panel para ambas facciones: selección individual, doble clic limitado al conjunto y a la definición, selección en otras páginas, eliminación mantenida, cancelación y alias A. Incluye el máximo de 60 unidades y 20 edificios, recorrido de las cuatro páginas y capturas y ausencia de superposición con producción. `SelectionGridPreview.RunValidation` añade las regresiones de grupos, rueda, entrada de órdenes/rally, menús y producción. No construye un jugador:

```sh
unity run . --log-file TestResults/unity-selection-grid.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.SelectionGridPreview.RunValidation
```

## Comodidad de controles y audio

`Frontier.Editor.ComfortAudioPreview.Run` prepara los prefabs Touch Plus usando los modelos del SDK, comprueba inicio con colocación diferida, retención/cancelación/restauración, geometría y mano activa del mando, música sin clipping y unión del bucle, sonidos de órdenes, límites de voces, deduplicación, pausa y volúmenes independientes. Conserva las preferencias de volumen previas. Ejecuta además `UnifiedWheelPreview`, `MenuFlowPreview` y `AttackPointerPreview`, incluidas entrada/colas y rueda de acciones. Las pruebas de entrada incluyen doble orden a los 0,5 s y rechazo de conversión a los 0,6 s.

```sh
unity run . --editor-version 6000.3.26f1 --log-file TestResults/unity-comfort-audio.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ComfortAudioPreview.Run
```

Las vistas de mandos y la música WAV quedan en `TestResults/ComfortAudio`. El audio se genera con código propio; las mallas, texturas y animaciones del mando siguen siendo assets del SDK de Meta. Esta validación no sustituye escuchar la mezcla, comprobar la postura de la muñeca y restaurar un ancla en un Quest físico.


`Frontier.Editor.StartupMenuPreview.Run` prueba el menú de inicio de Quest con poses sintéticas del visor y un tablero guardado a más de 20 m. Verifica espera de seguimiento, restauración tardía del ancla, entrada a Settings y recolocación/cancelación, arrastre cerca del usuario, Reset position, regreso tras foco/suspensión/visor y preservación del desplazamiento de consola durante partida. Genera tres vistas con pose real en `TestResults/StartupMenu` y ejecuta las regresiones de `MenuFlowPreview` y `ComfortAudioPreview`. Restaura la preferencia de posición previa y no genera APK.

## Informe posterior a la partida

`Simulation.Report` se publica únicamente cuando hay victoria, derrota, empate o rendición. Las estadísticas y el historial rival no se agregan a `PlayerView`; el AI sigue recibiendo la misma información filtrada. La pantalla de resultado contiene Overview, Graphs y Build order, con navegación por el mismo puntero del resto de los menús. El informe se archiva en el dispositivo y se puede volver a abrir desde Match history en el menú principal, incluso después de cerrar la app. El formato y las bases para replays se describen en `docs/match-history.md`.

- Overview compara trabajadores y ejército entrenados/perdidos/máximo simultáneo, estructuras terminadas/perdidas, minerales y gas entregados, gastos brutos, devoluciones, banco final, APM y mayor ráfaga de órdenes en una ventana móvil de un segundo. Las entidades iniciales no cuentan como producción.
- APM efectivo: una llamada aceptada a `Submit` es una acción, incluso si manda a un grupo. La rendición, órdenes rechazadas, selección, ataques automáticos y ejecución posterior de órdenes ya encoladas no cuentan. El promedio usa tiempo de simulación; la gráfica usa una ventana móvil de 60 segundos, normalizada por el tiempo transcurrido durante el primer minuto. Pausar no agrega tiempo.
- Graphs compara trabajadores, ejército, minerales/gas acumulados, minerales/gas disponibles y APM. Se registra el inicio, una muestra cada cinco segundos y el final exacto. Tras 2048 muestras se duplica el intervalo conservando el inicio; la gráfica limita los puntos dibujados. Los picos de población y ráfagas se miden en los eventos, independientemente de ese muestreo.
- Build order conserva cada construcción iniciada y cada entrenamiento/investigación encolado, con tiempo, población usada/reservada y resultado: listo, cancelado, productor destruido o pendiente. La producción inicial está excluida; una orden de construcción diferida aparece cuando la construcción comienza realmente. Cada jugador tiene su propia lista paginada.

Prueba del registro: `bash scripts/check-core.sh --match-report-only`. Prueba de menú y vistas: `Frontier.Editor.MatchReportPreview.Run` vía Unity CLI, con capturas en `TestResults/Reports/`. La prueba usa seis minutos de simulación de dos estrategas para comprobar las comparaciones, todas las gráficas, paginación de ambos bandos, revancha y partidas de duración cero. No valida comodidad o rendimiento en Quest físico.

## Historial y espectador

El contrato de grabación, compatibilidad y controles está en [match-history.md](match-history.md). `GameSession.Replay` abre una simulación independiente y bloquea órdenes de juego; `ReplayPlayback` verifica comandos/checkpoints, y `ObserverView` reúne ambas perspectivas solo para el reproductor. El panel de espectador conserva visibles economía, población y producción durante pausa, navegación y saltos.

`Frontier.Editor.ReplayValidation.Run` valida motor, sesión, mando, panel, historial y menús. `RunPresentation` permite repetir sesión/panel/historial/menús después de un cambio visual. Los fixtures crean grabaciones en directorios temporales y comprueban que observarlas no modifica sus archivos. Las capturas quedan en `TestResults/Spectator/` y `TestResults/ReplayInput/`. Por ejemplo:

```sh
unity run . --log-file TestResults/unity-replay-validation.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ReplayValidation.Run
```

La reproducción reconstruye desde el inicio al retroceder y distribuye los saltos largos entre fotogramas. No serializar `Snapshot()` como si fuera estado restaurable; omite estado interno de simulación. Cambios incompatibles del núcleo requieren incrementar `Simulation.ReplayRulesVersion`. El renderizado omnisciente no debe sustituir a `View(player)` en partidas normales ni alimentar al estratega.

## Colocación y altura del tablero

`Frontier.Editor.BoardPlacementPreview.Run` ejercita el `CommanderInput.Update` real con estados Touch de ambas manos: controles fijos, altura con stick, tamaño con Grip/Trigger, confirmación A/X y cancelación B/Y, botones retenidos al entrar/salir, pausa, cancelación, modelo y hints. Los ajustes continuos usan el mismo router con un delta explícito para probar avances dentro de un cuadro del Editor. Genera dos vistas en `TestResults/BoardPlacement/`, sin guardar anchors. `RunBatch` agrega las regresiones de menú inicial, restauración del tablero, flujo de menús, comodidad y rueda.

```sh
unity run . --log-file TestResults/unity-board-placement.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.BoardPlacementPreview.RunBatch
```
