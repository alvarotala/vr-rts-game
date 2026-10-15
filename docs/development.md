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
bash scripts/unity.sh validate
```

El núcleo se prueba con .NET 8 sin dependencias NuGet. El script busca `dotnet` o el SDK incluido en la instalación local de Unity 6.5; se puede indicar otro ejecutable mediante `DOTNET`.

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

Incluye los cinco estados de recolección y añade inicio en ambos mapas, trabajadores y tutorial de cada facción, colas de unidades y mejoras, pausa y final de partida. Comprueba que las etiquetas entren en su altura disponible y guarda quince vistas estáticas en `TestResults/English`, sin generar ejecutables.

Para comprobar navegación entre menús y ciclo de partida, usar **Frontier → Preview and check menu flow**, o:

```sh
unity run . --log-file TestResults/unity-menus.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.MenuFlowPreview.Render
```

Ejecuta rayos contra los botones reales y comprueba inicio, configuración, pausa, regreso, limpieza de partida, conservación de colocación y pausas externas, nueva partida y revancha. Guarda diez vistas estáticas en `TestResults/Menus`. Restaura la preferencia de mano que usa durante la comprobación y no genera ejecutables. El escenario de prueba desktop también entra ahora por **New Match** antes de **START MATCH**.

Para inspeccionar el hover en el tablero real desde el Editor, usar **Frontier → Preview hover**, o:

```sh
unity run . --log-file TestResults/unity-hover.log --no-tail -- \
  -buildTarget StandaloneOSX -executeMethod Frontier.Editor.HoverPreview.Render
```

Guarda siete vistas en `TestResults/Hover`: unidad y edificio con/sin hover, edificio seleccionado al apuntar y retirar el puntero, y enemigo resaltado. Usa `BoardView` y su shader en una vista estática; no genera ejecutables ni valida interacción física en Quest.

## Resultados y entregas

Guardar registros, capturas e informes locales en `TestResults/` y los ejecutables en `Builds/`. Ambos directorios están excluidos de Git. Conservar en [validación](validation.md) las conclusiones, las rutas de evidencia y las limitaciones de cada entrega; indicar si los cambios de fuentes aún no están incluidos en el APK disponible.

## Próximas etapas

El multijugador por internet y el editor de computadora son etapas posteriores. El núcleo ya separa órdenes, estado autoritativo, información visible y colocación física. No se han conectado servicios de Relay ni creado cuentas de infraestructura. Los criterios de integración están en [arquitectura](architecture.md).
