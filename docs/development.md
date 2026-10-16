# Guía de desarrollo

## Convenciones del proyecto

- Mantener el `README.md` de la raíz centrado en introducción, requisitos, instalación, configuración y uso básico.
- Documentar avances, cambios y decisiones en `docs/`. Usar [arquitectura](architecture.md) para responsabilidades y decisiones técnicas, [mapas](maps.md) para contratos de datos y [validación](validation.md) para resultados, evidencias y pendientes. Crear otro documento aquí cuando el tema lo necesite.
- Mantener los controles y las mecánicas actuales en la [guía de juego](gameplay.md).
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
bash scripts/check-core.sh --matches --count=20
bash scripts/check-core.sh --practice --matches --opponent=very-easy --count=4
bash scripts/unity.sh validate
```

El núcleo se prueba con .NET 8 sin dependencias NuGet. El script busca `dotnet` o el SDK incluido en la instalación local de Unity 6.5; se puede indicar otro ejecutable mediante `DOTNET`.

Las comprobaciones de dificultad cubren reacción, límites con reservas, ritmo de producción, defensa y oleadas de Very Easy. `--practice` añade cuatro simulaciones de hasta 19 minutos contra un jugador pasivo, cruzando ambos mapas y facciones de la IA; registra economía, población y primera ofensiva en `TestResults/very-easy-practice.json`. `--matches --opponent=very-easy --count=4` enfrenta Normal contra Very Easy en los cuatro cruces de facciones y registra resultados en `TestResults/matches.json`. Sin ese modificador, las partidas mantienen la alternancia anterior entre rivales Normal y Easy. Estas pruebas no sustituyen el balance con jugadores humanos.

Incluye regresiones de navegación: 50 unidades de ambas facciones, destinos en una esquina, tráfico opuesto, aliados quietos, mantener posición, bloqueo enemigo, edificios nuevos, rampas y doce trabajadores en el mismo recurso. En cada paso verifican separación, límites de velocidad y permanencia sobre terreno transitable.

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

Comprueba las órdenes contextuales, selección de recursos, totales de base, exceso y órdenes sobre el extractor. Renderiza cinco estados del panel en `TestResults/Harvesting`. Son vistas estáticas del panel real desde el Editor; no certifican legibilidad en el visor. `HarvestingChecks` forma parte de `check-core.sh` y comprueba reparto, reservas, agotamiento, gas, carga y niebla de guerra para ambas facciones.

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

## Rueda contextual Touch

Para comprobar el enrutamiento de entrada y renderizar las ruedas de ambas facciones sin generar un ejecutable:

```sh
unity run . --log-file TestResults/unity-action-wheel.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ActionWheelPreview.Render
```

La prueba alimenta `CommanderInput.RouteWheel` con cuadros sintéticos de joystick/A/B/gatillo, pasa las entradas no consumidas a `PointBoard` y verifica los comandos reales. Comprueba la selección del sector, zona neutra, submenú Build, producción, investigación, costos, cancelación, rechazo, cambios de selección/cola, interrupciones y liberación del gatillo. Guarda las vistas en `TestResults/ActionWheel/`. El panel comparte `SelectionActions`; `ProductionQueuePreview.Render` verifica las acciones de la consola y ejecuta las regresiones de HUD y menús. Las entradas sintéticas no emulan los mandos físicos: tamaño, postura y comodidad deben confirmarse en Quest.

## Apuntado de minerales

`Frontier.Editor.ResourcePickingPreview.Render` comprueba la resolución de cristal/suelo, interacción con trabajadores cercanos, clic con temblor y órdenes de recolección; renderiza los dos mapas a distintos tamaños. Ejecutar con `unity run . --log-file TestResults/unity-mineral-picking.log --no-tail -- -buildTarget StandaloneOSX -executeMethod Frontier.Editor.ResourcePickingPreview.Render`. Las imágenes quedan en `TestResults/ResourcePicking/`. `EdgeSelectionPreview.Render` y `HarvestingPreview.RenderHud` cubren las regresiones de selección y panel. Ninguno genera APK ni ejecutable desktop.
