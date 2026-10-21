# Historial local y replays

Cada partida nueva se registra automáticamente bajo `Application.persistentDataPath/Matches/<GUID>/`. El menú principal abre **Match history**, paginado por fecha, y permite consultar Overview, Graphs y Build order de las partidas terminadas. **Watch replay**, disponible en cada registro y en su informe, abre el tablero en modo espectador. Se conserva el catálogo original para mostrar sus nombres y datos aun cuando cambien los assets actuales. Consultar un informe no inicia una simulación ni modifica la configuración de la próxima partida.

El historial comienza con esta implementación: las partidas anteriores que no se registraron no pueden recuperarse. Los archivos son locales al dispositivo; no hay sincronización, exportación ni borrado automático.

## Vista de espectador

El replay muestra el mapa completo y un panel permanente con ambos jugadores: minerales, gas, trabajadores, ejército, población y estructuras. La producción reúne construcción, entrenamiento e investigación por tipo, separa los pedidos activos de los pendientes y muestra el progreso del próximo que termina. Cada columna tiene páginas independientes si hay más de trece tipos, sin ocultar los recursos ni los controles.

Se puede pausar, reproducir a 0,5× / 1× / 2× / 4×, saltar quince segundos, arrastrar la línea de tiempo, reiniciar y recuperar el encuadre completo. Los saltos conservan pan/zoom y el estado de pausa; al terminar permanece visible el último estado. Arrastrar la línea de tiempo congela momentáneamente la reproducción y aplica el salto al soltar. Un salto largo se reconstruye por partes y muestra Seeking. Las partidas interrumpidas reproducen solo su tramo guardado.

| Entrada | Acción |
| --- | --- |
| A / X de la mano activa, fuera del panel | Reproducir / pausar |
| Gatillo o A / X apuntando al panel | Activar el control señalado |
| Stick izquierda / derecha | Retroceder / avanzar 15 segundos; centrar para repetir |
| Stick arriba / abajo | Aumentar / reducir velocidad; centrar para repetir |
| Grip + stick | Desplazar el mapa |
| Grip + A / X o B / Y | Acercar o alejar |
| Clic del stick | Mostrar / ocultar hints de espectador |
| B / Y sin grip | Salir al historial |
| Escritorio | Espacio pausa; flechas tiempo/velocidad; WASD o arrastre central desplaza; rueda acerca; Escape sale |

La navegación y el transporte funcionan también en pausa. Se usan botones físicos de navegación, independientes de los hotkeys de combate. Si falta colocar el tablero, Watch replay solicita la colocación antes de abrirlo. Salir restaura la configuración de la próxima partida y no modifica la grabación ni crea otro registro. Durante un replay no se ejecuta el estratega ni se admiten órdenes de juego. La vista omnisciente se crea únicamente para esta sesión; las partidas normales conservan su `PlayerView` filtrado por niebla.

## Estados y ciclo de vida

- **Completed**: victoria, derrota, empate o rendición. Se guarda el informe completo al detectarse el resultado, antes de salir o iniciar una revancha. Volver a guardar conserva el mismo ID.
- **Abandoned**: abandonar explícitamente una partida, incluso desde pausa, registra una rendición final y guarda sus estadísticas con el estado Abandoned.
- **Interrupted**: cerrar o perder el proceso sin resultado conserva el último tramo confirmado. Se muestra la duración guardada; no se inventa un ganador ni un informe final. No implica poder continuar jugando esa partida.

El registro se confirma cada 15 segundos de simulación, al perder foco, al pausar la aplicación y al salir. Un cierre forzado puede perder el tramo posterior al último guardado. Pausas y tiempo fuera de la app no avanzan el reloj de la partida. Un error de almacenamiento se informa en el resultado con Retry save; los datos en memoria se conservan para reintentar mientras la sesión siga abierta. Las pruebas de Editor usan directorios temporales propios y no agregan partidas falsas al historial real.

## Formato 1

| Archivo | Contenido |
| --- | --- |
| `header.json` | ID, inicio UTC, mapa, facciones, dificultad, versión de app, build GUID, Unity, plataforma, frecuencia de ticks, versión de reglas y hash de los inputs congelados. |
| `rules.json` | Copia del mapa y catálogo completos y JSON del estado inicial de diagnóstico. |
| `orders-000000.json`, etc. | Tramos numerados con órdenes de ambos lados, ordinal global, tick de emisión, todos los argumentos y resultado aceptado/rechazado; tick y SHA-256 del estado al confirmar el tramo. |
| `progress.json` | Cantidad de tramos y órdenes confirmados, tick, fecha de guardado, estado, ganador final y hashes del mundo/informe. |
| `report.json` | DTO explícito/versionado del informe, con todas las muestras, contadores y build orders. Solo se publica un resultado después de guardar este archivo. |

Cada archivo se escribe primero a `.tmp`, se sincroniza y se reemplaza mediante una operación atómica. El marcador `progress.json` define los tramos confirmados: archivos temporales o tramos huérfanos tras una interrupción no forman parte del replay. Un archivo dañado no borra otros registros ni impide listar el resto del historial. Los IDs se validan antes de resolver rutas.

## Contrato de reproducción

`Simulation.BeginRecording()` se llama al inicio. Se copian los comandos y sus selecciones para que el llamador no pueda modificar una orden ya registrada. Se conservan también los rechazos durante una simulación activa: algunos consumen números de secuencia, por lo que omitirlos podría alterar la reproducción. Los rechazos durante pausa o después del resultado no cambian el mundo y no se registran. El APM conserva su definición: únicamente órdenes aceptadas, excluyendo rendición y comportamiento automático.

`MatchArchiveStore.CreateReplay()` valida la versión de reglas y el estado inicial, y construye una simulación independiente con el mapa, catálogo y facciones guardados. `ReplayPlayback` consume `ReadSegments()` en orden, avanza hasta el tick de cada comando y lo envía mediante Submit sin ejecutar Strategist. Compara aceptación/rechazo y el hash de Snapshot de cada checkpoint, además del ganador final. Los comandos del mismo tick conservan el ordinal global; un tramo posterior puede contener más órdenes en el tick del último checkpoint. Las divergencias detienen la reproducción y muestran el motivo, conservando la salida al historial.

Avanzar tiene un presupuesto de ticks y comandos por fotograma; una pausa larga del proceso no provoca una ráfaga para recuperar todo el tiempo perdido. Durante un seek, la sesión agrega un presupuesto de 2 ms por fotograma, comprobado entre pasos, comandos y checkpoints. Es trabajo cooperativo en el hilo principal; un paso o checkpoint individual no se interrumpe. Retroceder crea una simulación nueva y la reconstruye desde el inicio, conservando el catálogo/mapa originales. `StateRevision` también informa los cambios que ocurren sin avanzar un tick, por ejemplo órdenes simultáneas o fallos de verificación.

Mientras se calcula el salto, `GameSession` conserva las últimas proyecciones publicadas: unidades, animaciones, efectos, recursos y colas de producción quedan congelados. El tablero evita actualizar su presentación y el audio de batalla se silencia; la cámara, el encuadre, el transporte y el indicador de progreso siguen disponibles. Pulsar ±15 segundos durante un seek cambia su destino acumulado. Al terminar se publica el nuevo estado una sola vez y se limpian interpolación, audio y efectos transitorios, sin reconstruir el terreno ni cambiar el encuadre. Se conserva la intención de reproducir o pausar del usuario; una interrupción del visor suspende también el cálculo pendiente.

**Compatibilidad:** incrementar `Simulation.ReplayRulesVersion` cuando cambien las reglas, algoritmos de simulación, semántica de órdenes o condiciones iniciales. No reordenar los valores numéricos de los enums guardados. Cambiar el formato requiere otra `schemaVersion` y un lector/migración explícito. Los informes tienen versión de datos independiente y pueden seguir consultándose aunque el replay requiera reglas antiguas.

Congelar el catálogo y mapa no conserva el código de simulación. Conservar replays entre cambios incompatibles requerirá mantener ese ejecutor anterior o desarrollar una representación de estados adicional. La determinación entre plataformas y builds no se da por garantizada: los checkpoints deben detectar divergencias y detener una reproducción incorrecta. El Snapshot actual es diagnóstico y omite estado interno de navegación/visión; no se debe utilizar como checkpoint restaurable para saltar arbitrariamente en el tiempo. Por ahora, una reproducción se reconstruye desde el inicio. No se registran poses del visor/mando ni recorrido de cámara.

## Validación

- `bash scripts/check-core.sh --recording-only`: copia de argumentos, órdenes rechazadas, pausa, serialización del informe y reconstrucción exacta de seis minutos sin AI.
- `Frontier.Editor.MatchHistoryPreview.Run` vía Unity CLI: escritura/lectura real en un directorio temporal, reinicio del lector, comparación de todos los checkpoints y del informe final, versiones incompatibles, abandono, interrupción, archivos dañados, fallo de disco recuperable y navegación/capturas del historial.
- `Frontier.Editor.ReplayValidation.Run`: transporte, reconstrucción, errores de grabación, límites por fotograma, separación de sesión en vivo, colocación diferida, conservación del encuadre, controles sintéticos de ambas manos, interfaz por rayo, producción y regresión de historial/menús. Las grabaciones de prueba viven en directorios temporales; capturas en `TestResults/Spectator/` y `TestResults/ReplayInput/`.
- Estas comprobaciones cubren el entorno del Editor. No prueban duración de escrituras, comodidad ni rendimiento en un Quest físico.
