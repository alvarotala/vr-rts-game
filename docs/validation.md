# Registro de validación

## Guía de controles — validación completa y APK 0.1.9 — 21 de octubre de 2026

Se confirmó la corrección ya presente en el directorio de trabajo: las comprobaciones de solapamiento usan `statusBanner`, que comparte el contenedor del panel con la guía y la navegación. El texto `status` es hijo del banner y tiene coordenadas locales distintas; usar su posición producía el falso error `Control guide overlaps footer`. El mismo ajuste aparece en las comprobaciones de hotkeys, historial, informe y espectador.

- `LocalizationPreview.Run` completo aprobado con Unity **6000.3.26f1**, salida 0: `TestResults/unity-control-guide-localization.log`.
- **366 comprobaciones de flujo de menús y 61 vistas**, **328 de idiomas**, **30 de replays** y **528 del panel de espectador y 7 vistas** aprobadas. La validación española registra **0 desbordamientos** y solo 9 términos compartidos legítimos sin traducción.
- Inspeccionadas las capturas `TestResults/Menus/01-guide-control-guide.png` y `TestResults/Localization/es-touch-guide-Selection.png`: tarjetas, navegación y pie separados en inglés/español y en las referencias desktop/Touch.
- El fallo pendiente registrado en la entrega 0.1.8 queda resuelto. Estas son comprobaciones de Editor con poses sintéticas; no constituyen una prueba física en Quest.
- **APK recompilado:** `Builds/Quest/LittleLegion.apk`, **74.331.482 bytes**, **Little Legion 0.1.9 (10)**, paquete **`app.sadmonkey.littlelegion`**, ARM64/IL2CPP. Unity terminó con salida 0 y `editorErrors` vacío: `TestResults/unity-little-legion-apk-0.1.9.log`.
- Verificados etiqueta, paquete, versión, arquitectura, integridad ZIP y firma APK v2. SHA-256: `aceb2708ff8ab06ab502ef67b3e5268cf069928d3c3e0a15214b6ff790cb43f0`. Evidencias: `TestResults/little-legion-0.1.9-badging.txt`, `little-legion-0.1.9-signature.txt`, `little-legion-delivery-0.1.9.json` y `Builds/Quest/LittleLegion.provenance.json`.
- Conservados y verificados por hash el APK 0.1.8 y su procedencia en `Builds/Quest/Archive/LittleLegion-0.1.8.apk` y `LittleLegion-0.1.8.provenance.json`. El nuevo APK corresponde al directorio de trabajo actual, incluidos los cambios locales de iluminación y niebla ya presentes. No se instaló en un dispositivo ni se hizo push.

## Little Legion — nombre, concepto y APK 0.1.8 — 21 de octubre de 2026

El menú y el producto pasan a **Little Legion**, con **Pequeños ejércitos. Grandes batallas.** en español y **Little armies. Big battles.** en inglés. El plan de juego gratis con venta de skins y las colecciones Pharaoh Cats, Gummy Brigade y Cosmic Crew quedan documentados en [identidad](identity.md), sin implementar tienda ni nuevas razas.

- **APK:** `Builds/Quest/LittleLegion.apk`, **74.319.474 bytes**, nombre visible **Little Legion**, identificador **`app.sadmonkey.littlelegion`**, versión **0.1.8**, código **9**, ARM64/IL2CPP, Android mínimo 32 y objetivo 36. Es una aplicación distinta del prototipo anterior; no migra sus datos.
- Unity **6000.3.26f1** y Unity CLI **1.0.0-beta.14** compilaron correctamente, salida 0 y `editorErrors` vacío. Registro: `TestResults/unity-little-legion-apk-0.1.8.log`; procedencia: `Builds/Quest/LittleLegion.provenance.json`. El build usa el directorio de trabajo actual, incluidos los cambios locales de niebla que ya estaban presentes al iniciar esta tarea.
- Integridad ZIP, firma APK v2, etiqueta, paquete, versión y arquitectura comprobados con las herramientas Android y lectura del archivo. SHA-256: `e0e1bbf497f938dbc6d2d682c0e871e5d6ab14723d5a7ece594a9dc16a7acef6`. Evidencias: `TestResults/little-legion-0.1.8-badging.txt`, `little-legion-0.1.8-signature.txt` y `little-legion-delivery-0.1.8.json`.
- Se revisaron las capturas reales del menú en español e inglés: `TestResults/Localization/es-main.png` y `TestResults/Menus/01-main.png`. El nombre y el lema caben correctamente. Se completaron cuatro traducciones que faltaban en el menú principal.
- `LocalizationPreview.Run` aprobó cobertura de traducciones y ausencia de desbordamientos en español; después falló al invocar la batería general `MenuFlowPreview` con **`Control guide overlaps footer`**. La comprobación compara la posición del texto de estado relativa a su contenedor con la posición de la guía relativa al panel; ese código y la jerarquía ya existían antes de esta tarea. **La batería completa no se considera aprobada.** Registro: `TestResults/unity-little-legion-localization-final.log`.
- `git diff --check`, sintaxis Shell/Python, catálogos JSON sin claves duplicadas, enlaces locales de documentación y resolución de la nueva imagen Compose aprobados. Los namespaces y herramientas internos `Frontier` permanecen estables; la configuración Android activa usa únicamente el nuevo identificador.

No se instaló ni probó en Quest físico. No se recompilaron el reproductor macOS ni el servidor; sus nombres de producto y próximas salidas están actualizados en fuentes. Las entregas y evidencias anteriores conservan sus nombres originales.

## Hints laterales con líneas y rueda cercana orientada a la vista — fuentes, sin nuevo APK — 20 de octubre de 2026

Los cuatro hints pasan a una columna al costado exterior del mando, con flechas de dirección y seis trazos finos conectados al stick. La columna y sus líneas cambian de lado según la mano, sin invertir letras ni flechas. La rueda abierta baja de 6 a 2,5 cm sobre la referencia del stick; su normal gira hasta 35° hacia la posición de los ojos. La cámara no desplaza el anclaje y girar la cabeza sin trasladarla no hace girar la rueda.

- `ComfortAudioPreview.RenderControllerWheel` aprobado en Unity **6000.3.26f1**, salida 0: `TestResults/unity-side-callouts.log`.
- **55 comprobaciones de mando/colocación/audio y cuatro vistas**, incluidos los hints laterales en ambos modelos y la rueda cercana inclinada hacia la cámara.
- **628 comprobaciones de rueda y 20 vistas**, incluidos gráficos sin raycast, flechas, reflejo de la columna, distancia física, inclinación limitada, movimientos de cámara, navegación, vibración y cancelaciones.
- Inspeccionadas las vistas `ComfortAudio/right-controller.png`, `left-controller.png`, `right-controller-wheel.png` y `UnifiedWheel/industrial-right-hand-groups.png`. `git diff --check` pasa.

Validación en Editor con poses sintéticas; la comodidad debe confirmarse en Quest físico.

## Hints sobre el stick y rueda paralela al mando — fuentes, sin nuevo APK — 20 de octubre de 2026

Se elimina la rueda pequeña: quedan cuatro textos con contorno alrededor del stick, sin fondo, sectores ni cruz. La referencia viene de la pose neutral del stick de cada prefab Touch Plus. Los hints quedan sobre la tapa y la rueda abierta conserva su tamaño, elevada 6 cm sobre esa referencia y paralela a la superficie del mando. Ambas capas siguen la mano sin orientarse hacia la cámara ni inclinarse con la animación del stick. Se corrige además el material izquierdo: el preparador toma la textura del cuerpo y no la del indicador de batería, cuyo orden entre hijos difiere entre manos.

- `ComfortAudioPreview.RenderControllerWheel` aprobado en Unity **6000.3.26f1**, salida 0: `TestResults/unity-stick-controller-final.log`.
- **53 comprobaciones de mando/colocación/audio y cuatro vistas**: ambos modelos, textura de color correspondiente, hints centrados sobre el stick visible y rueda elevada en el mismo plano.
- **584 comprobaciones de rueda y 20 vistas**: solo cuatro textos en reposo, geometría y orientación de ambas manos en cada sección, independencia de la cabeza, navegación continua, asignación de grupos, vibración y cancelaciones.
- Inspeccionadas `ComfortAudio/right-controller.png`, `left-controller.png` y `right-controller-wheel.png`. `git diff --check` pasa.

La comprobación usa Editor y poses sintéticas. Comodidad y legibilidad final requieren probar el Quest físico.

## Selección por bases y guía separada — fuentes, sin nuevo APK — 20 de octubre de 2026

La pausa incorpora **Selection: Bodies / Bases**, con Bodies como valor inicial y persistencia local. Bases resuelve sobre el suelo y evita que el cuerpo de un mineral o edificio intercepte la unidad situada detrás. Conserva la selección de huellas y recursos, y comparte objetivo con hover y órdenes. **Control guide** abre un submenú; Back y el atajo de menú vuelven a la pausa compacta sin reanudar.

- `ResourcePickingPreview.Render`: **114 comprobaciones / 10 vistas**, con ambos mapas, rayos oblicuos a través de mineral y edificio, gatillo, restitución del modo anterior, órdenes contextuales, niebla, agotamiento y recorte.
- `MenuFlowPreview.Render`: **304 comprobaciones / 42 vistas**, incluido modo inicial, preferencia guardada/reversible, pausa compacta, guía separada, seis categorías y regreso con Back/atajo conservando la pausa. Ambos fixtures restauran la preferencia de selección anterior.
- `OrderQueueValidationBatch.Run` completo aprobado en Unity **6000.3.26f1**, salida 0: `TestResults/unity-base-selection-batch.log`. Repite las pruebas anteriores y aprueba puntero **92**, entrada/colas **172**, rueda **452**, líneas de órdenes **68**, rally **76**, orientación **32**, selección desde bordes **33**, cancelación **168**, ayuda **140**, cola de producción **90**, además de recolección.
- Inspeccionadas las vistas de pausa y guía: navegación legible, tarjetas separadas y sin solapamientos. `git diff --check` pasa. Registros individuales: `TestResults/unity-base-targeting.log` y `TestResults/unity-base-menu-flow.log`.

Validación en Editor con entrada sintética. No se generó un APK ni se probó en Quest físico.

## Comodidad, mando visible y audio — fuentes, sin nuevo APK — 19 de octubre de 2026

La ventana de doble A/X pasa de 0,35 a 0,55 s, conservando la respuesta inmediata del primer toque y la selección a 0,35 s. Hint y rueda aplican una inclinación local de 45° hacia arriba. El mando activo muestra el Touch Plus del SDK con materiales URP, escala física original y botones animados; el láser nace de un emisor del modelo. La aplicación deja de cargar superficies o abrir colocación al adquirir seguimiento en el menú. Start Match usa el tablero confirmado o espera restauración; si falta, pide colocarlo y comienza al confirmar, con cancelación disponible.

Se incorporan quince efectos sintetizados originales y música estéreo instrumental de 40 s, con controles persistentes separados de Music/Effects. El combate usa eventos de PlayerView, tres inicios por vista y doce voces reutilizadas. Pausa, interrupciones, deduplicación y límites evitan repetir o acumular ráfagas. La cámara de Quest recibe AudioListener si no lo tenía.

- `ComfortAudioPreview.Run` aprobado en Unity **6000.3.26f1**, salida 0: `TestResults/unity-comfort-audio-final.log`. **47 comprobaciones nuevas**, dos vistas del mando y exportación WAV: flujo de inicio/colocación y cancelación durante restauración, retención del tablero, mano y escala del modelo, emisor, ausencia de colliders, ocultación, muestras finitas sin clipping y unión del bucle, sonidos distintos, límites de combate, pausa y volúmenes.
- Regresiones aprobadas: **586 comprobaciones de rueda / 20 vistas**, **297 de menús / 41 vistas**, **92 de puntero / 12 vistas**, **172 de entrada/colas** y **452 de acciones / 18 vistas**. Incluyen doble orden a los 0,5 s, separación a los 0,6 s, conversión con grip sin duplicar y pose inclinada en ambas manos/facciones.
- Tras proteger el láser al cerrar la rueda durante una interrupción, se repite `UnifiedWheelPreview.Render`: **586 comprobaciones / 20 vistas**, salida 0 (`TestResults/unity-comfort-wheel-final.log`). `git diff --check` pasa.
- Inspeccionadas `ComfortAudio/right-controller.png` y `UnifiedWheel/industrial-right-hand-groups.png`: modelo de mando, origen del láser y rueda inclinada. Música disponible en `TestResults/ComfortAudio/quiet-orbit.wav`. Los modelos conservan referencias al SDK de Meta; no se descargaron assets externos.

No se generó APK ni ejecutable desktop. Estas pruebas usan Editor y seguimiento sintético; no validan el ajuste físico del modelo, la comodidad, el balance audible en el visor ni la restauración real de anclas de Quest. La mezcla y la música son una primera versión para probar en el dispositivo.

## Órdenes directas y destinos visibles — fuentes, sin nuevo APK — 19 de octubre de 2026

Se conserva la ejecución inmediata existente de A y doble A/X, y se elimina el rojo temporal del cursor después de aceptar un ataque, que podía confundirse con una confirmación pendiente. Cada Move/Attack/Attack Move aceptado deja un pulso expansivo de 1,2 segundos en el punto ordenado, cian para mover y rojo para atacar. El cursor rojo queda reservado para seleccionar un destino desde el menú. Todas las unidades propias seleccionadas muestran su destino activo con líneas punteadas de color, incluso sin cola; los pasos futuros siguen numerándose para una unidad representativa.

- `OrderQueueValidationBatch.Run` completo aprobado en Unity **6000.3.26f1**, salida 0: `TestResults/unity-direct-order-batch.log`.
- **92 comprobaciones de entrada/puntero y 12 vistas**: doble A/X mediante `CommanderInput.Update` con ambas manos, ambas facciones y orden reasignada a grip; el gatillo nunca se pulsa. Se verifica Move al primer toque, Attack Move al segundo, avance de simulación, ausencia de repeticiones al mantener, selección conservada, animación fija aunque se mueva el cursor o salga del mapa, expiración, rechazo, pausa, tablero oculto y reemplazo por Move/Stop.
- **68 comprobaciones visuales de órdenes y 9 vistas**, incluidas líneas sin cola, colores distintos en dos unidades y deselección sin cambiar la vista de simulación. **166 comprobaciones de entrada/colas** y **76 de rally con 14 vistas**. Pasan además rueda de acciones, orientación, selección desde fuera del tablero, recursos, producción, ayuda y menús.
- Inspeccionadas `AttackPointer/industrial-fixed-attack-pulse.png` y `OrderQueue/industrial-mixed-active-orders.png`: pulso separado del cursor normal y líneas rojas/cian diferenciadas. Las pruebas de guía y rally se actualizaron al panel vigente y al resumen de producción agrupada; se conservan las colas individuales. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. La entrada es sintética y las capturas corresponden al Editor; comodidad, legibilidad y rendimiento en Quest requieren prueba física.

## Adquisición y aproximación de combate — fuentes, sin nuevo APK — 19 de octubre de 2026

El ataque automático aprovecha primero un disparo disponible. Entre objetivos al alcance conserva el foco y prioriza amenazas; al perseguir considera distancia y aliados de alcance similar ya comprometidos, con una preferencia limitada por el objetivo actual. Cada aproximación busca un puesto con línea de tiro y espacio libre, reserva puestos de aliados y reconsidera los que se ocupan. Los combatientes de corto alcance pueden usar el borde del alcance sin superponer cuerpos. Las reglas son comunes a ambos jugadores; las órdenes explícitas, estadísticas, economía y física general de movimiento se conservan.

- **180 comprobaciones de combate aprobadas**, incluidas **34 nuevas**: `TestResults/combat-engagement.log`. Cubren ambas facciones y puestos de jugador, cambio desde una persecución desfavorable, disparos disponibles, distribución de aproximaciones, puestos libres/ocupados y combates activos de **18 contra 18 y 50 contra 50** con los cinco roles militares. Se verifican participación y separación física; pasan las regresiones de aceleración, frenado, foco explícito, niebla, colas y continuidad de Attack Move.
- Las mismas pruebas de comportamiento ejecutadas con el `Combat.cs` anterior reproducen **12 fallos** de adquisición, concentración de aproximaciones y puestos compartidos (`TestResults/combat-new-cases-old-code.log`). El código anterior se compiló en un harness temporal, sin reemplazar los archivos del proyecto.
- **1.688 comprobaciones del núcleo completo aprobadas**, salida 0: `TestResults/combat-core-final.log`, incluyendo movimiento/colisiones, paridad de facciones, economía, habilidades e IA.
- Unity **6000.3.26f1** compiló y ejecutó `AttackPointerPreview.Render`, salida 0: **36 comprobaciones del puntero**, **166 de entrada/colas** y **452 de rueda de acciones**; registro `TestResults/unity-combat-engagement.log`. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Estas pruebas verifican simulación e integración de entradas en Editor; la sensación del combate y el rendimiento en Quest requieren probarlo en el visor. No constituyen una medición de la dificultad Easy contra una persona.

## Vibración de navegación de la rueda — fuentes, sin nuevo APK — 19 de octubre de 2026

El mando activo recibe un pulso suave al cambiar de ítem (25 ms, amplitud 0,12) y uno más marcado al abrir o cambiar de nivel, incluido Back (55 ms, amplitud 0,28). El cambio de nivel y su resaltado inicial producen un solo pulso. Mantener dirección/gatillo, centrar, el temblor dentro de la histéresis o actualizar descripciones no repiten vibración. El feedback existente de comandos se conserva. Expirar el tiempo, cambiar de mano, perder foco o desactivar entrada detiene el mando que recibió el pulso.

- Batch completo `UnifiedWheelValidationBatch.Run` aprobado en Unity **6000.3.26f1**, salida 0, registro `TestResults/unity-wheel-haptics.log`: **586 comprobaciones de rueda / 20 vistas**, además de hotkeys, grupos, acciones, menús, colas, rally, habilidades y puntero.
- **60 comprobaciones nuevas de vibración** cubren ambas manos y facciones. Controladores sintéticos registran el despacho real de `OVRInput.SetControllerVibration` sin activar hardware. Comprueban una sola emisión por cambio, amplitudes relativas, duración, ausencia de repeticiones, feedback de rechazo, parada al expirar/foco/cambio de mano/desactivación y silencio en desktop. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. El registro verifica las solicitudes al SDK; la sensación e intensidad de los pulsos requieren prueba física en Quest.

## Asignar grupos desde abajo y confirmar con gatillo — fuentes, sin nuevo APK — 19 de octubre de 2026

El hint incorpora una cuarta dirección, **Assign group** abajo. Abre una rueda de cuatro grupos en amarillo, con el grupo 3 resaltado por la dirección sostenida. Girar el stick elige otro grupo; una nueva pulsación del gatillo reemplaza sus miembros con la selección actual y cierra la rueda. Arriba conserva la recuperación de grupos. La raíz ofrece la misma categoría abajo; B/Y cierra desde la raíz. Se mantiene el guardado alternativo por modificador dentro de Groups.

- Batch completo `UnifiedWheelValidationBatch.Run` aprobado en Unity **6000.3.26f1**, salida 0; registro `TestResults/unity-trigger-group-assignment.log`. Incluye **526 comprobaciones de rueda / 20 vistas**, **548 de grupos / 12 vistas** y **110 de hotkeys**, además de las regresiones de acciones, menús, rally, colas, habilidades y puntero.
- Se asignan los cuatro espacios y se comprueban sus miembros exactos, conservación de la selección, reemplazo por edificios y asignación vacía para limpiar un espacio. Se verifican apertura sin escritura, dirección continua, una confirmación por gatillo, drenaje antes del mapa, cancelación por B/tiempo/cambio de selección/seguimiento y regreso al modo de recuperación. Clic de stick no confirma la página Assign group. Entrada real de `CommanderInput.Update` con estados OVR sintéticos, ambas manos y selección reasignada a Grip.
- Inspeccionadas `UnifiedWheel/industrial-idle-hint.png`, `industrial-direct-assign-group.png` y `ControlGroups/industrial-trigger-assign-building.png`: cuarta dirección visible, modo amarillo, grupo resaltado y ayuda de gatillo legibles. Las comprobaciones de pose incluyen la nueva sección. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. La validación corresponde al Editor; ergonomía y legibilidad física quedan pendientes en Quest.

## Navegación continua de la rueda — fuentes, sin nuevo APK — 19 de octubre de 2026

La inclinación de apertura ya resalta la opción de la sección: arriba entra a Groups con el grupo 1 listo para confirmar. Entrar o retroceder aplica la dirección sostenida al nuevo nivel en el mismo cuadro; se puede recorrer raíz → Selection → Build → construcción sin centrar el stick. Cada paso requiere una nueva pulsación del gatillo. Centrar sigue limpiando el resaltado e inicia el cierre tras 0,8 s, sin confirmar nada. Se conservan el tamaño compacto y la pose junto al origen del rayo.

- `UnifiedWheelValidationBatch.Run` completo aprobado en Unity **6000.3.26f1**, salida 0: **462 comprobaciones de rueda / 18 vistas** y **104 de hotkeys**, además de grupos, rally, colas, menús, acciones y habilidades. Registro: `TestResults/unity-continuous-wheel-final.log`.
- Se verifica recuperación directa del grupo 1, giro entre opciones sin centrar, navegación continua de varios niveles, retroceso con B/gatillo, ausencia de confirmaciones repetidas al mantener el gatillo, tiempo de cierre y consumo de la confirmación antes de volver al mapa. `CommanderInput.Update` recorre el gesto con OVR sintético en ambas manos y selección reasignada a Grip. La prueba de habilidades se actualizó para quitar la confirmación de categoría anterior.
- Pasan las regresiones de cambio externo de cola, selección, foco, pausa y pose de la mano. Inspeccionada `UnifiedWheel/industrial-direct-groups.png`: grupo 1 resaltado inmediatamente y descripción visible. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Las entradas son sintéticas de Editor; la fluidez percibida requiere prueba física en Quest.

## Rueda compacta anclada a la mano — fuentes, sin nuevo APK — 19 de octubre de 2026

El hint y la rueda abierta comparten posición a 2,5 cm sobre el origen del rayo, según el eje local del mando. Se elimina el avance hacia delante y el ajuste por distancia a la cámara; la orientación sigue la mano, incluido el giro de muñeca. El hint mide aproximadamente 9 cm de ancho y el marco de la rueda abierta 18 cm (anillo de 16 cm). Se conserva el acceso directo a Groups, Selection y Unit orders.

- `UnifiedWheelValidationBatch.Run` completo aprobado en Unity **6000.3.26f1**, salida 0: **442 comprobaciones de rueda y 18 vistas**, más hotkeys, menús, grupos, colas, rally, acciones y habilidades. Registro: `TestResults/unity-hand-attached-wheel.log`.
- Las pruebas de pose cubren ambas manos y facciones, hint, grupos, selección, construcción y órdenes. Verifican cercanía al origen del rayo, ausencia de avance, escala física, giro completo e independencia de la posición/orientación de la cabeza, incluso a 12 cm de distancia.
- Las vistas `UnifiedWheel/*-hand-hint.png` y `*-hand-groups.png` conservan la escala y pose reales con una cámara fija; el marcador amarillo representa el origen del rayo solo en el fixture. Inspeccionadas ambas manos: anclaje, giro y texto correctos. La repetición focalizada después de corregir el orden de capas del renderizador de capturas aprobó las mismas **442 comprobaciones y 18 vistas**, salida 0 (`TestResults/unity-hand-wheel-views.log`). `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Las poses son sintéticas de Editor; tamaño percibido, legibilidad y comodidad requieren prueba física en Quest.

## Acceso directo desde el hint de la rueda — fuentes, sin nuevo APK — 19 de octubre de 2026

Inclinar desde el hint entra directamente en Groups (arriba), Selection (derecha) o Unit orders (izquierda), cuando la sección está disponible. El hint conserva su aspecto. La apertura no hereda una opción seleccionada: hay que centrar antes de elegir una acción o grupo y confirmarlo con gatillo. Abajo mantiene la rueda cerrada; volver desde una sección conserva el acceso a la raíz.

- Batch completo `UnifiedWheelValidationBatch.Run` aprobado en Unity **6000.3.26f1**, salida 0, incluyendo **334 comprobaciones de navegación** y **100 de hotkeys**, además de acciones, grupos, colas, rally, menús, habilidades y entrega del puntero al mapa. Registro: `TestResults/unity-direct-wheel-entry.log`.
- Entradas OVR sintéticas por `CommanderInput.Update` verifican las tres entradas directas en ambas manos con selección reasignada a Grip, neutral antes de elegir y recuperación por gatillo. Se generan diez vistas de navegación; inspeccionadas `UnifiedWheel/industrial-direct-groups.png`, `industrial-direct-selection.png` e `industrial-direct-orders.png`: sección correcta, sin opción heredada y texto legible.

No se generó APK ni ejecutable desktop. La validación corresponde al Editor; el gesto y la comodidad en Quest físico quedan pendientes.

## Retiro de la leyenda de botones — fuentes, sin nuevo APK — 19 de octubre de 2026

Se retira la tarjeta de Trigger, A/B, Grip y Stick junto al mando, incluyendo su componente, conexiones y previsualización específica. La rueda conserva su hint direccional. Compilación y **190 comprobaciones de la rueda aprobadas**, con cuatro vistas, en Unity **6000.3.26f1**, salida 0; registro `TestResults/unity-remove-controller-hints.log`. No se generó un APK nuevo.

## Leyenda permanente del mando — fuentes, sin nuevo APK — 19 de octubre de 2026

Una tarjeta junto al mando muestra gatillo para seleccionar, A para mover/dar órdenes, Grip para agregar/encolar, B para pausa y stick para la rueda. Las etiquetas siguen la mano activa y los botones configurados; cambian al apuntar ataques, construir, lanzar habilidades, abrir menús, guardar grupos o colocar el tablero. La tarjeta permanece con la rueda abierta y la pausa de usuario, y se oculta al perder foco o durante interrupciones del sistema.

- **367 comprobaciones de Editor aprobadas**: 177 de la leyenda y 190 de regresión de la rueda. Unity **6000.3.26f1**, salida 0; registro `TestResults/unity-controller-hints-final.log`.
- **14 vistas generadas**, diez de la leyenda y cuatro de la rueda. Se comprueban botones por mano, remaps, cambios de contexto, cancelación, guardado de grupos, visibilidad y texto completo. Inspeccionadas `ControllerHints/hand-context.png` y `wheel-context.png`: leyenda y rueda separadas, texto legible. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Las entradas y capturas son de Editor; la comodidad y legibilidad sobre el mando físico quedan pendientes en Quest.

## Rueda unificada Touch — fuentes, sin nuevo APK — 19 de octubre de 2026

El mando activo muestra un hint compacto durante la partida. Inclinar el joystick desde el centro abre la raíz: arriba Groups, derecha Selection, izquierda Unit orders y abajo Close. El gatillo entra o confirma; mantener la dirección conserva la rueda y centrar durante 0,8 segundos sin acción la oculta. Al cambiar de nivel se requiere centrar antes de elegir. Clic del joystick y doble grip dejan de abrir menús; volver al centro no recupera grupos. Dentro de Groups, gatillo recupera y clic sostenido + dirección + soltar guarda; B/Y vuelve a la raíz. Patrullar sigue pendiente.

La rueda comparte comandos con la consola: construcción, producción, investigación, rally, órdenes y habilidades. Cambiar selección, pausar, perder foco/seguimiento, mover el panel, recolocar el tablero o cambiar de mano invalida los gestos pendientes. Se consume la confirmación hasta liberar los botones antes de aceptar destinos sobre el terreno. Los perfiles de hotkeys mantienen los remaps compatibles; `wheel` ahora alterna los hints; si antes usaba gatillo o B/Y, ese par se intercambia con Stick click para reservar la navegación física.

- **2089 comprobaciones de Editor aprobadas**, sin sumar las repeticiones internas de la suite de colas. Registro: `TestResults/unity-unified-wheel-final.log`, Unity **6000.3.26f1**, salida 0. El batch cubre navegación (190), acciones (512), grupos (544), hotkeys (96), menús (296), colas (166), rally (126), controles de caster (79), sus modelos (29), efectos (15) y puntero de ataque (36).
- La prueba de `CommanderInput.Update` usa estados OVR sintéticos en ambas manos: selección remapeada a Grip con inclinación simultánea, mantenimiento y liberación, retorno al centro, apertura posterior y recuperación por gatillo físico. También se comprueban migración de perfiles, rechazos sin modificar bindings, neutral entre niveles, cambios de contexto y consumo de las confirmaciones antes de volver al mapa.
- Las capturas y las pruebas de texto verifican hint, raíz, guardado de grupos, construcción y múltiples productores; cada etiqueta se comprueba por altura y generación de glifos completos. La simulación de economía y combate no cambia en esta tarea. La revisión adicional de acciones y grupos aprobó 512 + 544 comprobaciones (`TestResults/unity-wheel-capture-final.log`). Las dos líneas de ayuda de construcción y productores se confirmaron también por sus píxeles: las regiones inferiores son idénticas (81.000 píxeles comparados).

No se generó APK ni ejecutable desktop. Son entradas sintéticas y vistas de Editor; postura, comodidad y legibilidad con seguimiento físico siguen pendientes en Quest.

Las referencias históricas a ruedas separadas, apertura por clic/doble grip y recuperación al centrar describen versiones anteriores; las instrucciones vigentes están en [guía de juego](gameplay.md#rueda-unificada).

## Alcance de la evidencia

La simulación, la compilación Unity, la ejecución gráfica desktop y el funcionamiento en Quest son comprobaciones diferentes. Este archivo registra cada una; no considera una compilación como prueba física de realidad mixta.

## Grupos de edificios y selección doble de barracas — fuentes, sin nuevo APK — 19 de octubre de 2026

El doble clic o doble gatillo sobre un edificio selecciona los propios de la misma definición dentro de doce celdas, tal como el usuario eligió para mantener el alcance local de las unidades. Incluye construcciones incompletas y admite el modificador aditivo. Los grupos ya aceptaban edificios; se verificaron ahora cohortes de barracas desde los gestos reales de guardado/recuperación, conservando producción y puntos de reunión.

Seleccionar varios productores mantiene las acciones de entrenamiento/investigación en panel y rueda. Cada activación compra un único pedido y lo asigna a un productor compatible terminado con espacio, dando prioridad a los que pueden avanzar, después al menor tiempo de cola, cantidad de pedidos e ID. El resumen grupal muestra actividad y pedidos; la cancelación requiere un edificio individual. El catálogo y las reglas por facción permanecen iguales.

- **1738 comprobaciones del núcleo aprobadas**, incluidas **96 de producción grupal** y doce escenarios de práctica. Se comprueban compras/reservas únicas, distribución, mezcla de productores, rechazo atómico por costo/suministro/requisitos/propiedad, investigación única y cancelación individual. Un caso con perímetros realmente ocupados verifica que un pedido no quede detrás de una unidad bloqueada cuando hay otra fábrica disponible, que se pueda seguir encolando si todas están bloqueadas y que liberar la salida reanude despliegue y reparto. Ambas facciones, barracas y laboratorios con rally. Evidencias: `TestResults/building-groups-core-full.log` y `group-production-core.log`.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**: **1601 comprobaciones y 159 vistas**, salida 0, sin errores de scripts, shaders ni excepciones (`TestResults/unity-building-groups.log`). Entrada **166**, grupos **232 / 12 vistas**, rally **126**, cuadrícula **399 / 12**, ayuda **140 / 38**, producción **90 / 20**, menús **296 / 41**, rueda **152 / 18** y **18 vistas** de recolección.
- Se verifica doble selección de barracas a doce celdas y exclusión justo fuera del radio, adición, clics distintos/lentos, interrupción por foco, muerte y recuperación del grupo. Los clics reales de panel y confirmación de rueda compran unidades distribuidas sin perder la selección. Cambiar de selección entre presionar y soltar una tarjeta no cancela otro pedido.
- La selección máxima de productores expone nueve acciones. Se ajustó la altura mínima de tarjetas compactas sin ampliar el panel; las pruebas verifican límites, texto y separación. Todos los sectores de la rueda son alcanzables. Inspeccionadas `ControlGroups/industrial-barracks-group.png`, `ControlGroups/biological-producer-actions.png` y `ProductionQueue/biological-all-producers.png`: acciones, resumen y grupos legibles. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Las entradas y vistas son sintéticas de Editor; la interacción y legibilidad en Quest físico quedan pendientes.

## Paridad mecánica entre facciones — fuentes, sin nuevo APK — 19 de octubre de 2026

Consortium y Symbiosis comparten los valores industriales como base: seis unidades y siete edificios equivalentes con idénticos costos, tiempos, salud, movimiento, colisión, ataque, visión, suministro, requisitos y energía. Las habilidades correspondientes también son iguales. Se conservan nombres, IDs, modelos y efectos visuales. Ambas facciones requieren un trabajador durante toda la construcción, reparan gastando minerales y carecen de regeneración pasiva de salud. Retirar o perder al constructor pausa la obra; otro trabajador puede retomarla sin volver a pagar.

- **1642 comprobaciones del núcleo aprobadas**, incluidas **106 de paridad**. Estas comparan todos los campos de juego del catálogo y trazas completas de simulación al intercambiar facciones en las mismas posiciones: ingresos, producción de los seis roles, movimiento, combate y habilidades. Las regresiones de construcción verifican asignación, abandono, muerte, reemplazo, pago y liberación del trabajador. Evidencias: `TestResults/faction-parity-full.log` y `faction-parity.log`.
- **12/12 escenarios de práctica aprobados**: dos mapas × dos facciones × tres niveles. Todos terminaron sin órdenes rechazadas. Al excluir únicamente el identificador de facción, los seis pares registraron métricas idénticas, incluidos producción, tiempos de ataque/daño, habilidades, recursos y resultado.
- **26 comprobaciones de expansión aprobadas**, con seis escenarios de doce minutos. En Rift, ambas facciones completan la segunda base a los **243 s / 244 s** según el lado, y la tercera a los **436 s / 433 s**; los registros económicos coinciden al cambiar de facción. Training conserva una base al no disponer de depósitos neutrales. Son escenarios sin daño para observar desarrollo económico; no prueban expansión bajo presión humana. Evidencia: `TestResults/faction-parity-expansion.log`.
- Unity **6000.3.26f1**: compilación y **776 comprobaciones / 131 vistas** aprobadas con salida 0 en `TestResults/unity-faction-parity.log`. Incluyen entrada encolada **140**, ayuda **140 / 38 vistas**, producción **48 / 16**, menús **296 / 41**, rueda **152 / 18** y **18 vistas** de recolección. Se ejercitan los controles reales de ambas facciones para construir, pausar, reanudar y reparar. La primera pasada detectó texto de ayuda recortado; se acortó y la repetición pasó sin errores de scripts, shaders ni excepciones. Inspeccionadas la ayuda del Seeder y las guías de órdenes/producción en pausa: texto legible y completo. `git diff --check` pasa.

El contrato de paridad queda protegido por pruebas para detectar futuras diferencias accidentales. Introducir asimetrías más adelante requiere definir y evaluar sus compensaciones; esta validación no afirma una tasa de victorias del 50 % ni equilibrio entre lados de cada mapa. No se generó APK ni ejecutable desktop; la prueba física en Quest queda pendiente.

## IA más activa, expansión y continuidad de combate — fuentes, sin nuevo APK — 19 de octubre de 2026

Se reforzaron Easy, Normal y Hard mediante decisiones: exploración temprana, producción paralela, reservas de presupuesto, ejércitos proporcionales con refuerzos, uso coordinado de casters y expansiones deliberadas. Hard puede mantener tres bases en Rift; Easy y Normal, dos. Las condiciones iniciales, el catálogo, los ingresos y las estadísticas no dependen de la dificultad. La retirada y el reagrupamiento, antes pospuestos, se incluyeron por autorización del usuario.

- **1527 comprobaciones del núcleo aprobadas**, con **85 económicas**, **108 tácticas** y doce partidas contra un rival pasivo. Estas doce partidas terminaron con victoria, **cero órdenes rechazadas** y **16 habilidades ejecutadas**. Evidencias: `TestResults/ai-core-final.log` y `difficulty-practice.json`.
- Las regresiones usan `Submit` y `Step`: producción simultánea de varios edificios; reserva del primer caster sin bloquear infantería por tecnología ausente; extracción real de gas; recuperación de trabajadores y constructores; reparación de tropas; expansión bloqueada que libera recursos y elige otro sitio; defensa proporcional; refuerzos; retirada que llega antes de Hold y cambia de destino al perder su base; continuidad de daño cuando un enemigo abandona el radio de alarma.
- La comparación detectó un explorador Easy industrial encerrado por reservas en Hold entre edificios, que tardaba **304–305 s** en salir. Separar reunión adelantada y retirada, despejar realmente las salidas y esperar con Stop permite que el mismo explorador salga a los **92 s en ambos mapas** y descubra el cuartel enemigo a los **92 s en Training / 113 s en Rift**, antes del ataque principal. La regresión final comprueba desplazamiento y visión reales.
- Otra regresión reprodujo órdenes de reunión que cancelaban un combate activo al salir el enemigo del radio de defensa. Las tropas enfrentadas a un enemigo visible conservan ahora su orden; las reservas libres siguen reuniéndose. La prueba verifica daño continuado en ambas facciones y los tres niveles.
- **48/48 victorias frente a la IA anterior**, **16 por dificultad**, cruzando ambas facciones, mapas y lados. Sin empates por tiempo ni órdenes rechazadas de la IA nueva; **470 órdenes de habilidades y 470 efectos ejecutados**. La comparación inyecta `LegacyStrategist` únicamente en el harness y usa la misma simulación, catálogo y reglas para ambos jugadores. Evidencias: `ai-final-matrix.json` y `ai-final-matrix.log`.
- **12/12 escenarios económicos de doce minutos completos**, también sin órdenes rechazadas. En Rift, la segunda base termina a los **383–461 s en Easy**, **331–351 s en Normal** y **233–243 s en Hard**; Hard completa la tercera a los **420–431 s**. Training conserva una base porque no tiene depósitos neutrales. Los exploradores salen efectivamente a los **64–93 s**. Estos escenarios desactivan el daño para observar desarrollo tardío: no cuentan como victorias ni prueban expansión bajo presión humana.
- **Hard contra el nuevo Normal: 4 victorias y 4 derrotas**, sin empates por tiempo ni órdenes rechazadas, en espejos de ambas facciones, mapas y lados (`ai-final-tiers.json`). La corrección de continuidad cambia resultados individuales, pero esta muestra no demuestra una superioridad general de Hard. Los límites de desarrollo y la presión inicial difieren; el balance relativo todavía requiere partidas humanas y más escenarios.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**: compilación y **296 comprobaciones / 41 vistas** del menú aprobadas, código de salida 0, sin errores de scripts, shaders ni excepciones (`unity-ai-levels-final.log`). Inspeccionado `TestResults/Menus/05-hard.png`: descripción de economía rápida y ataques desde 2:30 legible, sin desbordamiento.

El benchmark conserva fuentes y datos congelados en `TestResults/ai-snapshots/run.AxIrQd`, con hash de Core `00b78ca89bed3f67410e90afcecb3e4e2e162c29fe2b11157fdcb961cbd2e293`; las trece fuentes de Core, el catálogo y ambos mapas de esa ejecución coincidían byte por byte. La comparación Hard–Normal usa el mismo hash. Las **12 comprobaciones de frontera de información** confirman decisiones iguales al alterar estados enemigos ocultos. `git diff --check` pasó. Estos resultados son anteriores a la posterior igualación de facciones registrada arriba.

No se generó APK ni ejecutable desktop. Las simulaciones y las entradas sintéticas no validan dificultad frente a una persona, rendimiento ni experiencia física en Quest.

## Casters y habilidades manuales — fuentes, sin nuevo APK — 19 de octubre de 2026

Se agregaron Resonance Lab → Conduit y Spore Sanctum → Oracle, con modelos propios, producción, reunión y dos habilidades por facción: daño en área y curación de tropas aliadas. El menú y la rueda permiten elegir la habilidad y después el punto visible; muestran energía, disponibilidad, área y alcance. Cada confirmación asigna un caster preparado, que puede acercarse y luego retoma su orden y cola. La IA utiliza el mismo sistema de energía, visión, alcance y recarga.

- **1358 comprobaciones del núcleo aprobadas**, incluidas **154 específicas de casters**: construcción y producción reales, ambas facciones, selección mixta, un caster por orden, daño a unidades/estructuras visibles, curación limitada a vida máxima, energía, recarga, aproximación, cancelación, continuación de órdenes, colas, niebla y privacidad de eventos. Registros finales: `TestResults/caster-full.log` y `caster-core.log`.
- La revisión reprodujo una cola bloqueada con Hold → Cast → Move encolado durante el acercamiento (`caster-queue-before.log`). La regresión final verifica que los dos destinos pendientes se completan en orden; Hold se conserva cuando no hay cola. El catálogo también rechaza facciones sin caster o academia.
- **12/12 escenarios de práctica aprobados**: dos mapas × dos facciones × Easy/Normal/Hard, manteniendo las condiciones de economía, población y oleadas. Todos construyen academia y producen casters; se registraron **19 órdenes de habilidad y 19 efectos ejecutados** en Normal/Hard. Easy termina su ataque de cuatro unidades antes de desplegar casters; sus decisiones de habilidad tienen pruebas específicas. Hubo un rechazo de construcción por unidades bloqueando la ubicación en Training/biológica/Hard. Evidencias: `caster-practice.log` y `difficulty-practice.json`. Estos escenarios se ejecutaron antes del ajuste final de cola manual y validación del catálogo; después se repitió la batería general de 1358 comprobaciones. El fallo previo de retirada de la IA sigue fuera de alcance por decisión del usuario.
- **433 comprobaciones de Unity y 41 vistas aprobadas** en `unity-casters.log`: controles de casters **77 / 8 vistas**, modelos **29 / 3**, efectos **15 / 4**, puntero de ataque **36 / 8**, entrada encolada **124** y rueda **152 / 18**. Se prueban botones reales, pulsación sostenida mientras cambia la energía, A/gatillo, segundo A rápido, selección mixta, pausa, cancelación, pérdida de selección, cambio de modo, niebla, entrenamiento, colores y ejecución del efecto en el tablero.
- Regresión de interfaz ampliada en `unity-caster-layouts.log`: selección máxima **399 / 12 vistas**, ayuda **138 / 38**, producción **48 / 16**, menús **296 / 41**, rueda **152 / 18** y entrada de reunión **126**, además de 18 vistas de recolección. El fixture de selección distribuye el límite de edificios entre los siete roles. Los controles de construcción permanecen dentro del panel.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**: las dos ejecuciones finales terminaron con código 0, sin errores de scripts, shaders ni excepciones. La inspección gráfica detectó y corrigió un identificador reservado de HLSL en el shader de apuntado; se agregó una aserción de compilación del shader. También se corrigió el GUID de un nuevo archivo antes de la validación final.
- Inspeccionados `TestResults/CasterControls/industrial-caster-menu.png`, `industrial-worker-menu.png`, `biological-heal-target.png`, `TestResults/Casters/friendly.png` y `TestResults/Abilities/pulse-30.png`: energía y habilidades legibles, siete construcciones, modelos diferenciados y áreas/efectos de daño y curación. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Las pruebas usan simulación y entradas sintéticas; quedan pendientes la ergonomía de apuntado, legibilidad, rendimiento y balance con una partida humana en Quest físico.

## APK integrado — 0.1.7 (8) — 18 de octubre de 2026

Compilación solicitada por el usuario desde el commit limpio `d6a572d`, con todos los cambios hasta `9737dcf`: pantalla separada de Hotkeys, referencia CSV por categorías, recuperación de grupos al inclinar el stick y cuadrícula interactiva de selección. El guardado de grupos conserva el comportamiento confirmado: mantener pulsado el stick más dirección y soltar la pulsación para reemplazar el grupo. Las notas históricas de «sin APK» de estas tareas quedan incluidas en esta entrega.

- **APK:** `Builds/Quest/FrontierCommand.apk`, **59.091.981 bytes**, paquete `com.alvaro.frontiercommand`, versión **0.1.7**, código **8**, Android ARM64/IL2CPP, mínimo 32 y objetivo 36.
- **SHA-256:** `3062c569042b7098a217243636127c458300c454033ba7872cb9eb91ac54b342`.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**: compilación correcta, `editorErrors` vacío. Integridad ZIP y firma APK v2 verificadas; certificado idéntico al de 0.1.6. El manifiesto conserva Quest 3, passthrough, escena, anclas y control de visibilidad del límite.
- Evidencias: `TestResults/unity-apk-0.1.7.log`, `apk-0.1.7-build.json`, `apk-0.1.7-signature.txt`, `apk-0.1.7-badging.txt`, `apk-0.1.7-manifest.txt`, `delivery-0.1.7.json` y `Builds/Quest/FrontierCommand.provenance.json`.
- APK anterior y procedencia conservados en `Builds/Quest/Archive/FrontierCommand-0.1.6.apk` y `FrontierCommand-0.1.6.provenance.json`; hash del APK anterior verificado. La compilación no dejó cambios adicionales en los archivos versionados.

Actualizar con `adb install -r Builds/Quest/FrontierCommand.apk`. Se conservan las pruebas de las tareas anteriores; esta entrega solo cambia la versión y registra el empaquetado. Pendiente del usuario: instalación, interacción, legibilidad y rendimiento en Quest físico.

## Iconos interactivos de selección — fuentes, sin APK — 18 de octubre de 2026

El panel de partida muestra **SELECTED ITEMS** con iconos cuadrados de los modelos reales, nombres, vida y páginas de hasta 20 elementos. Trigger/A selecciona uno, doble Trigger/A filtra por definición dentro de la selección completa y mantener 0,65 s elimina un miembro con progreso rojo. Mouse izquierdo comparte los gestos. El clic simple espera 0,35 s para distinguir el doble sin perder el conjunto original.

- `SelectionGridPreview.Render`: **359 comprobaciones y 12 vistas** para ambas facciones, con el máximo de **50 unidades y 20 edificios** y recorrido de cuatro páginas. Registro final: `TestResults/unity-selection-grid-capacity.log`.
- Verificados: aislamiento de un elemento, coincidencias en otras páginas, exclusión de aliados cercanos no seleccionados, edificios de la misma definición, eliminación antes de soltar, retirada del último miembro, cancelación al salir de la casilla, cambios externos de selección, muerte, pausa, cancelación de foco/puntero, nueva partida y salida. A comparte la ruta y no interrumpe una pulsación del trigger ni produce clics al mapa.
- Regresiones en `TestResults/unity-selection-grid.log`: grupos **186 / 8 vistas**, rueda **148 / 18**, órdenes encoladas **64**, entrada de rally **126**, menús/Hotkeys **294 / 41**, producción **48 / 16** y **18 vistas** de recolección/HUD. Algunas rutinas vuelven a ejecutar menús/HUD; no se suman como cobertura nueva.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.13**: ambas ejecuciones finales terminaron con código 0, sin errores de compilación ni excepciones. Se inspeccionaron vistas industriales y biológicas, incluyendo progreso rojo y texto del CSV. El primer prototipo requirió añadir CanvasRenderer al gráfico personalizado y ajustar el rayo del fixture después de una captura que cambia el transform del panel. La suite conjunta expuso además dos aserciones preexistentes `IsNormalized(direction)` en el rayo inicial de `ControlGroupPreview` (también presentes en `unity-group-tilt.log`); se corrigió ese rayo y se repitió su suite por separado en `unity-selection-grid-group-regression.log`.
- Hotkeys, guía de pausa y CSV se mantienen sincronizados: las mismas **94 filas / 6 categorías**, con solo seis filas modificadas. `git diff --check` pasa. Los **55 hashes** de `TestResults/selection-grid-baseline.json` confirman que ProjectSettings, Assets/XR y APK existentes permanecen iguales. No se cambió la simulación ni se construyó APK.

Pendiente: tamaño percibido, precisión del puntero y comodidad de los tiempos de clic en Quest físico. La evidencia anterior usa el Editor, rayos sintéticos y capturas.

## Recuperación de grupos al inclinar el stick — fuentes, sin APK — 18 de octubre de 2026

Inclinar el joystick derecho sin pulsar ningún botón abre la rueda de grupos y resalta inmediatamente 1 arriba, 2 derecha, 3 abajo o 4 izquierda. Girarlo cambia el resaltado y volver al centro confirma. Se retiró la apertura por doble clic del stick. Clic conserva la rueda de acciones; mantener clic más dirección y soltar sigue reemplazando un grupo. Hotkeys, guía de pausa y referencia CSV reflejan el nuevo gesto.

- `ControlGroupPreview.RunValidation`: **818 comprobaciones enumeradas y 85 vistas**. Incluye grupos (**186 / 8 vistas**), acciones (**148 / 18**), entrada de colas (**64**), rally (**126**), menús y Hotkeys (**294 / 41**), más **18 vistas** del HUD/recolección.
- Se verificaron apertura y resaltado en el mismo cuadro para ambas facciones, las cuatro direcciones, giro, confirmación al centro, grupos vacíos, cancelación, guardado, ausencia de selección previa y conservación del tablero. Los pequeños movimientos dentro de la zona muerta no abren grupos.
- Las direcciones de una rueda de acciones, colocación, arrastre del menú o gatillo sostenido no inician recuperación. Después de cancelar, pausar o terminar esos ajustes, un eje inclinado no abre grupos hasta volver al centro. Foco, seguimiento, cambios de selección, nueva partida y salida invalidan gestos pendientes.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13** terminó correctamente, sin errores de compilación ni excepciones de pruebas. Registro: `TestResults/unity-group-tilt.log`. Se inspeccionaron `TestResults/ControlGroups/industrial-direct-tilt.png` y `TestResults/Menus/hotkeys-touch-groups-1.png`.
- Los **55 hashes** de `TestResults/group-tilt-baseline.json` verifican que ProjectSettings, Assets/XR y los APK existentes permanecen iguales. No se cambió la simulación ni se generó APK o ejecutable desktop.

Pendiente: validar ergonomía y sensibilidad con el mando físico. El APK 0.1.6 disponible aún utiliza el gesto anterior.

## Pantalla separada de Hotkeys y catálogo de defaults — fuentes, sin APK — 18 de octubre de 2026

Hotkeys reemplaza la entrada Controls del menú principal y de pausa con una referencia dedicada: selector Quest Touch / Mouse-Keyboard, seis categorías, cinco filas por página y navegación Previous/Next. Cada fila identifica comando, entrada o acción de menú y contexto. La guía de tarjetas sigue visible dentro de pausa. Ambas pantallas usan `DefaultControlCatalog`, con esquema 1, datos inmutables e IDs estables; la personalización de bindings queda pendiente y no cambió el enrutador de entrada. La revisión incorpora los gestos existentes de alternar con clic modificado, limpiar selección y cerrar ruedas, además de los atajos de colocación de escritorio.

- `MenuFlowPreview.Render`: **294 comprobaciones y 41 vistas**, Unity **6000.3.26f1** / CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones. Registro final: `TestResults/unity-hotkeys-final.log`.
- Se recorrieron las **47 entradas de cada dispositivo**, con IDs únicos, correspondencia entre tabla y catálogo, paginación completa y regreso a la primera página al cambiar de dispositivo. Consultar Touch en escritorio no activa Quest; reabrir usa el dispositivo real.
- Desde una partida pausada, cambiar categoría/dispositivo y pulsar una fila conserva tick, selección, recursos, grupos y modo de construcción. Back/B regresa a pausa; se conservan las regresiones de inicio, configuración, ayuda, interrupciones del visor, salida y revancha.
- Todas las capturas comprueban altura de texto y ausencia de superposición con navegación/pie. Se inspeccionaron las vistas Touch de grupos y producción en `TestResults/Menus/hotkeys-touch-*.png`. Referencia exportada: `TestResults/Menus/default-controls.md`; copia de consulta y acciones de contenido en [hotkeys](hotkeys.md).
- Los **55 hashes** de `TestResults/hotkeys-baseline.json` confirman que ProjectSettings, Assets/XR y APK existentes permanecen iguales. No se modificó la simulación ni se construyó APK o ejecutable desktop.

Pendiente: comprobar legibilidad, tamaño y comodidad con mandos en Quest real. El APK 0.1.6 existente todavía no contiene esta pantalla.

## APK integrado — 0.1.6 (7) — 16 de octubre de 2026

Compilación solicitada por el usuario desde el commit limpio `cb23cda`, con todas las tareas hasta `c17e3cf`: doble grip y atajos de rueda, ayuda de acciones y A en menús, cancelación individual de producción, movimiento con masa e inercia, colas de órdenes y gestos de selección, trayectorias punteadas, puntos de reunión, cuatro grupos con guardado por pulsación mantenida del stick y guía de controles en pausa. Las notas históricas de «sin APK» para estas tareas quedan incluidas en esta entrega.

- **APK:** `Builds/Quest/FrontierCommand.apk`, **59.048.849 bytes**, paquete `com.alvaro.frontiercommand`, versión **0.1.6**, código **7**, Android ARM64/IL2CPP, mínimo 32 y objetivo 36.
- **SHA-256:** `ebe553b743b54678df078329426e18ff6d2ee2f76d6d5cae77beb321acbff8e3`.
- Unity **6000.3.26f1** / CLI **1.0.0-beta.13**: compilación correcta, `editorErrors` vacío. Integridad ZIP y firma APK v2 verificadas; certificado idéntico al de 0.1.5. El manifiesto conserva Quest 3, passthrough, escena, anclas y control de visibilidad del límite.
- Evidencias: `TestResults/unity-apk-0.1.6.log`, `apk-0.1.6-build.json`, `apk-0.1.6-signature.txt`, `apk-0.1.6-badging.txt`, `apk-0.1.6-manifest.txt`, `delivery-0.1.6.json` y `Builds/Quest/FrontierCommand.provenance.json`.
- APK anterior y procedencia conservados en `Builds/Quest/Archive/FrontierCommand-0.1.5.apk` y `FrontierCommand-0.1.5.provenance.json`; hash del APK anterior verificado. La compilación no dejó cambios adicionales en los archivos versionados.

Para actualizar: `adb install -r Builds/Quest/FrontierCommand.apk`. Esta entrega cambia únicamente la versión y registra el empaquetado; conserva las comprobaciones por tarea documentadas abajo, sin repetirlas ni generar un ejecutable desktop. La prueba de controles, comodidad, legibilidad y rendimiento en Quest físico sigue pendiente del usuario.

## Guía de controles visible en pausa — fuentes, sin APK — 16 de octubre de 2026

La pausa incorpora `CONTROL GUIDE` con seis categorías y tarjetas de comando, gesto y descripción. Cubre selección, órdenes, menús/ruedas, cuatro grupos, producción y tablero, incluidos los gestos recientes de colas y reemplazo con stick, cancelación de producción y Rally Points. Controls comparte el contenido y cambia entre Touch y mouse/teclado; la guía se oculta al reanudar.

- `MenuFlowPreview.Render`: **58 comprobaciones y 28 vistas** en Unity **6000.3.26f1** / CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones. Registro: `TestResults/unity-pause-help.log`.
- Se recorrieron ambas variantes de las seis categorías desde Controls, y las seis categorías Touch dentro de pausa. Se verificó que consultar ayuda mantiene el tick detenido, bloquea entrada de juego y conserva selección/recursos. También pasan inicio, configuración, regreso desde submenús, pausa por visor, salida y revancha.
- Las capturas comprueban alto de todos los textos y ausencia de superposición con navegación o pie. Se inspeccionaron las vistas de grupos, órdenes y producción en `TestResults/Menus/07-pause-touch-*.png`.
- Los **54 hashes** de `TestResults/pause-help-baseline.json` confirman que ProjectSettings, Assets/XR y los APK existentes permanecen iguales. No se modificó la simulación ni se generó APK o ejecutable desktop.

Pendiente: validar tamaño y lectura del panel ampliado en Quest físico. La evidencia usa rayos sintéticos y capturas del Editor.

## Reemplazo de grupos con pulsación mantenida del stick — fuentes, sin APK — 16 de octubre de 2026

Mantener pulsado el joystick derecho desde el centro e inclinarlo abre `REPLACE GROUP`. Soltar la pulsación guarda la selección actual reemplazando todos los miembros del grupo resaltado. Girar cambia el grupo; centrar todavía presionado conserva el último resaltado y no confirma. El gesto empieza al inclinar, sin espera temporal. B cancela. Grip deja de modificar el guardado; doble clic conserva la recuperación de grupos al centrar el eje.

- **168 comprobaciones focales y ocho vistas** para ambas facciones. Incluyen reemplazo completo de los cuatro grupos, liberación con el eje inclinado o centrado, cambio de dirección, ausencia de guardado temprano, pulsación sin dirección, clic sobre eje previamente inclinado, cancelación, pérdida de liberación, tracking, foco, cambio de selección y desactivación. Mantener la segunda pulsación de un doble clic conserva recuperación y no arma un guardado.
- Regresiones de rueda de acciones (**148 / 18 vistas**), gestos de selección/colas (**64**), rally (**126**), menús (**48 / 12**) y HUD/recolección (**18 vistas**). Total: **554 comprobaciones enumeradas y 56 vistas**. Las entradas de prueba ahora incluyen estado sostenido y liberación del botón del stick.
- `ControlGroupPreview.RunValidation` terminó correctamente con Unity **6000.3.26f1** / CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones. Registro: `TestResults/unity-group-hold.log`. Se inspeccionaron las ruedas de recuperación/reemplazo y la ayuda Touch en `TestResults/ControlGroups` y `TestResults/Menus`; los textos se muestran completos.
- Los **54 hashes** de `TestResults/group-hold-baseline.json` confirman que ProjectSettings, Assets/XR y los APK existentes permanecen iguales. No se modificó el núcleo de simulación ni se generó APK o ejecutable desktop.

Pendiente: verificar en Quest la comodidad de mantener presionado el stick al inclinarlo y soltarlo. La evidencia anterior usa entradas sintéticas del Editor.

## Rueda de cuatro grupos — fuentes, sin APK — 16 de octubre de 2026

Doble pulsación del joystick derecho abre cuatro grupos con posiciones fijas: arriba, derecha, abajo e izquierda. Inclinar/girar resalta y volver al centro recupera el grupo directamente. Mantener grip al volver al centro guarda la selección en el grupo resaltado. B o pulsar el stick cancelan. La pulsación simple conserva la rueda de acciones inmediata, y doble grip sigue abriendo acciones. El HUD y los atajos 1–4/Ctrl+1–4 comparten los cuatro grupos.

- **126 comprobaciones focales y ocho vistas** para ambas facciones: cuatro direcciones, giro, zona muerta e histéresis, ausencia de elección por defecto, regreso al centro, grip para guardar, cambio de modo, botón 4 del HUD, selección vacía, miembros destruidos, propiedad y reinicio de grupos. Incluyen doble stick desde una rueda de acciones ya abierta y cancelación por pausa, foco, selección o partida.
- Regresiones: rueda de acciones (**148 comprobaciones / 18 vistas**), gestos de selección/colas (**64**), rally (**126**), menús (**48 / 12**) y HUD/recolección (**18 vistas**). Total: **512 comprobaciones enumeradas y 56 vistas**, sin contar la ejecución previa.
- `ControlGroupPreview.RunValidation` terminó correctamente con Unity **6000.3.26f1** / CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones. Registro final: `TestResults/unity-control-groups-final.log`.
- Se inspeccionaron las ruedas de selección y guardado, los cuatro botones del HUD y la pantalla Touch de controles. Las capturas están en `TestResults/ControlGroups` y `TestResults/Menus`; los textos no se recortan.
- Los **54 hashes** de `TestResults/group-wheel-baseline.json` confirman que ProjectSettings, Assets/XR y APK existentes permanecen iguales. No se modificó el núcleo de simulación ni se generó APK o ejecutable desktop.

Pendiente: comprobar en Quest la comodidad del doble clic de 0,35 segundos y la confirmación al centrar el stick. Las pruebas usan botones, ejes y tiempos sintéticos; no certifican el mando físico.

## Puntos de reunión de edificios — fuentes, sin APK — 16 de octubre de 2026

Seleccionar productores propios terminados y pulsar A sobre terreno define su `Rally Point`. Las nuevas unidades reciben un Move hacia ese destino, con orientación inicial y separación normales. Cada edificio conserva un punto independiente; cambiarlo no altera unidades desplegadas ni colas de producción. `Clear Rally Point`, en consola y rueda, restaura el comportamiento predeterminado. El mapa muestra bandera, punto y línea punteada cian al seleccionar el productor, y el HUD separa el destino de la producción. Los trabajadores con rally explícito se desplazan al punto; no reciben una orden de recolección.

- **591 comprobaciones de núcleo aprobadas**, incluidas 135 de rally para ambas facciones y todos los productores. Cubren destinos vigentes al nacer, borrado, costos y colas intactos, seis despliegues sucesivos sin solapamientos, propiedad, selección múltiple atómica, rutas/salidas bloqueadas y privacidad de edificios ocultos o recordados. Registro: `TestResults/rally-point-core-final.log`. No se repitieron partidas IA en esta tarea.
- **126 comprobaciones de entrada**: A, grip, doble A, limpieza mediante consola y rueda, consumo de botones, productores inválidos, orientación al aparecer y conservación de gestos de unidades. Registro: `TestResults/unity-rally-point-final.log`.
- **76 comprobaciones de presentación y 14 vistas**: banderas planas, puntos en centros de celda, trazos con espacios, recorte, ausencia de colliders, destinos múltiples, selección mixta, privacidad y limpieza al ocultar/destruir/salir. Capturas: `TestResults/RallyPoint`; registro final: `TestResults/unity-rally-point-captures.log`.
- Regresiones de orientación (**32**), gestos de colas (**64**), visualización de colas (**55 / 5 vistas**), cancelación de producción (**168 / 14**), ayuda (**134 / 36**), producción (**48 / 16**), recolección (**18 vistas**), menús (**47 / 12**) y rueda (**148 / 18**). Registro: `TestResults/unity-rally-point-presentation.log`. Total Editor: **898 comprobaciones enumeradas y 133 vistas**, sin contar ejecuciones repetidas.
- Se corrigieron dos problemas de fixtures durante la validación: un GUID inválido del archivo de pruebas y un muestreo de línea que no contemplaba el ajuste del destino al centro de celda. El registro de entrada conserva ese fallo inicial de presentación; la ejecución posterior de presentación y las capturas terminaron correctamente con Unity **6000.3.26f1** / CLI **1.0.0-beta.13**.
- Inspeccionados el panel del centro industrial con producción, el panel de tecnología biológica con investigación y los mapas de destinos múltiples/selección mixta. Producción, rally y acciones permanecen separados y legibles.
- Los **54 hashes** de `TestResults/rally-point-baseline.json` verifican que ProjectSettings, Assets/XR y APK existentes no cambiaron. No se generó APK ni ejecutable desktop.

Pendiente: comprobar comodidad del apuntado y legibilidad física de las banderas en Quest. Las pruebas usan entrada sintética y capturas del Editor; el APK existente no incluye esta función.

## Destinos con puntos y líneas punteadas — fuentes, sin APK — 16 de octubre de 2026

La cola del mapa usa puntos sólidos con aro y número separado, conectados por trazos punteados. Move es cian, Attack/Attack Move rojo y Gather/Build/Repair verde. Los enlaces conservan el color completo y aumentan su ancho para mejorar la lectura; la geometría sigue terreno/rampas y conserva el recorte y la selección existentes.

`OrderQueuePreview.Render` aprobó **55 comprobaciones y generó cinco vistas** con Unity 6000.3.26f1 y CLI 1.0.0-beta.13, sin errores de compilación ni excepciones. Se inspeccionaron las capturas del mapa y el panel industrial en `TestResults/OrderQueue`. Registro: `TestResults/unity-dashed-queue.log`. Los **54 hashes** de `TestResults/dashed-queue-baseline.json` confirman que ProjectSettings, Assets/XR y APK existentes permanecen iguales. No se modificó la simulación ni se generó APK. La legibilidad física en Quest sigue pendiente.

## Colas de acciones y gestos del mando derecho — fuentes, sin APK — 16 de octubre de 2026

Grip + A encola acciones por unidad; doble A convierte su primer toque en Attack Move o ataque directo sobre enemigo visible, sin duplicar un Move ni demorar el primer toque. Doble trigger selecciona unidades cercanas del mismo tipo. Grip + mantener trigger quieto durante 0,5 segundos quita la unidad apuntada y consume su liberación; el arrastre conserva selección de área. La pulsación del stick queda siempre para la rueda, incluso sosteniendo grip; doble grip sigue abriéndola.

La consola separa `UNIT ORDER PLAN` de producción, muestra conteo y próximos pasos. El mapa dibuja hasta ocho destinos numerados para un representante del grupo. Los destinos futuros no cambian la orientación visual; al activarse el siguiente waypoint se emite el nuevo rumbo inmediatamente. Las colas contienen hasta 32 pendientes por unidad, se revalidan al ejecutarse y se limpian mediante una nueva orden sin modificador, Stop o Hold. Contratos y criterios de finalización: [colas de acciones](order-queues.md).

- **460 comprobaciones de núcleo aprobadas**: 411 regresiones anteriores, 45 nuevas de colas y cuatro partidas IA completas. FIFO, conversión del primer toque activo/pendiente/ya terminado, límite, propiedad, privacidad/copia de vista, destinos bloqueados, construcción/costos, recolección, reparación y grupo de ocho sin solapamientos. Registro: `TestResults/order-queue-core-final.log`.
- Las cuatro partidas conservan los resultados y duraciones anteriores: 1336, 1060, 1048 y 1047 segundos simulados, cubriendo todos los cruces de facciones. No constituyen una prueba de balance humano.
- **809 comprobaciones enumeradas del Editor y 131 vistas estáticas**: `QueuedInputChecks` (64), `OrderQueuePreview` (55 / 5 vistas), `UnitFacingPreview` (32), selección externa (33 / 6), apuntado de recursos (80 / 6), cancelación de producción (168 / 14), ayuda (134 / 36), producción (48 / 16), HUD/recolección (18 vistas), menús (47 / 12) y rueda (148 / 18).
- Ejecutadas juntas mediante `OrderQueueValidationBatch.Run`, Unity **6000.3.26f1**, CLI **1.0.0-beta.13**, sin errores de compilación ni excepciones de pruebas. Registro: `TestResults/unity-order-queue.log`.
- Inspeccionadas `TestResults/OrderQueue/industrial-orders.png`, `industrial-waypoints.png` y `controls.png`: plan separado, números sobre terreno y ayuda sin desbordamiento. La selección mixta de edificio/unidad no superpone producción y órdenes.
- `ProjectSettings`, `Assets/XR` y APK existentes mantienen los **54 hashes** de `TestResults/order-queue-baseline.json`. No se generó APK ni ejecutable desktop; el APK existente no incluye estos cambios.

Pendiente: verificar en Quest la comodidad del hold de 0,5 segundos, la tolerancia del doble toque y la legibilidad de los destinos. Las pruebas usan rayos, tiempos y botones sintéticos; no certifican controles físicos ni rendimiento en el visor.

## Movimiento, masa, inercia y orientación — fuentes, sin APK — 16 de octubre de 2026

MOVE sustituye la ruta y las maniobras automáticas de cesión inmediatamente; la presentación empieza a girar ese mismo cuadro y el cuerpo responde desde el siguiente paso de simulación, conservando inercia. Cada tipo tiene masa, aceleración y desaceleración. STOP frena y HOLD POSITION ancla. El frenado también se aplica al llegar, al dar un MOVE dentro de la celda actual y al entrar en rango de ataque; después de frenar se revalida alcance y visión antes de disparar. Los giros visuales siguen las posiciones de simulación, filtran pequeñas correcciones y no vuelven al rumbo antiguo durante una reversa.

La navegación adelanta los puntos del camino cuando hay paso libre, conserva tramos rectos y desliza la componente libre contra el terreno. Los lotes de órdenes tienen identidades independientes. Los apartaderos se eligen fuera de la ruta que deben liberar; una cesión termina si el flujo dejó de viajar. Los aliados ya apartados aceptan empujes seguros sin rebotar hacia su punto anterior. Los empujes respetan masa, Hold, terreno, cuerpos y presupuesto de velocidad.

- **415 comprobaciones aprobadas** con `bash scripts/check-core.sh --matches --count=4`: 411 de reglas y regresiones más cuatro partidas IA completas. Incluye 52 comprobaciones nuevas de tráfico, 56 de colisión/inercia/micro y 60 de frenado en combate. Registro: `TestResults/pathfinding-core.log`.
- Las cuatro partidas Normal contra Normal cubren todos los cruces de facciones. Terminan en **1336, 1060, 1048 y 1047 segundos simulados**, sin violar límites de economía o población. Resultados: `TestResults/matches.json`. No se usan como prueba de balance humano.
- `UnitFacingPreview.Render`: **24 comprobaciones** en Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Giro antes del primer tick, reversa con inercia, filtrado de zigzag, curvas reales, pausa, reutilización de modelos, combate y órdenes aceptadas/rechazadas. Compilación de scripts correcta; registro: `TestResults/unity-movement.log`.
- `ProjectSettings`, `Assets/XR` y los APK existentes conservan los **54 hashes** de la línea base `TestResults/movement-baseline.json`. No se generó APK ni ejecutable desktop.

Comparación de escenarios de tráfico, en segundos simulados hasta que todas las unidades llegan:

| Escenario | Industrial antes → después | Biológica antes → después |
|---|---:|---:|
| Dos grupos de 15, corredor de una celda | No termina en 120 → 62,85 | No termina en 120 → 55,20 |
| Dos grupos de 15, corredor de dos celdas | 82,00 → 35,45 | 78,05 → 31,15 |
| Cruce perpendicular, 40 unidades | 34,95 → 24,60 | 30,95 → 20,95 |

Limitación observada: una reselección de 31 unidades biológicas con flujos opuestos en un corredor de una celda tarda **112,85 segundos** en completar todos los destinos. Hay congestión local en la entrada; finalmente llega todo el grupo sin solapamientos ni pérdida de órdenes. No se afirma ausencia de congestión en cualquier disposición de cuerpos. Los tiempos anteriores comparan la simulación previa con la nueva, que ahora incluye aceleración y frenado.

Pendiente: comprobar sensación de micro, comodidad visual y rendimiento térmico en Quest 3. Las pruebas de Editor y simulación no certifican FPS en el visor. Parámetros y decisiones: [movimiento](movement.md).

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

## Curva de dificultad y apertura simétrica — fuentes, sin nuevo APK — 18 de octubre de 2026

Very Easy, Easy y Normal comparten todos los tipos de unidad, la composición objetivo y ambas mejoras. Las diferencias se concentran en ritmo económico, tecnología, reacción y destacamentos: primeras oleadas desde 12 / 10 / 8 minutos, de 3 / 4 / 6 unidades, creciendo hasta 5 / 7 / 16. Una sola oleada permanece activa, sin refuerzos continuos; al perderla o retirarla empieza un descanso completo. El mismo grupo cambia de objetivo si destruye un edificio y conoce otros. Las reservas mantienen posición detrás de la base, con producción y defensas del lado propio, evitando ataques automáticos tempranos en Training Grounds.

Ambos lados mantienen 400 minerales, cero gas, una base, seis trabajadores y capacidad diez, con salud y reglas propias de su facción. Se reflejaron también las posiciones de los trabajadores iniciales: antes ambos lados salían por el borde superior de la base aunque los recursos estuvieran rotados, con distintas distancias de salida. Las pruebas comprueban posiciones y asignaciones iniciales reflejadas en ambos mapas, los cuatro cruces de facciones y cada dificultad.

- **995 comprobaciones aprobadas**, incluyendo límites con colas, acceso a todos los roles y mejoras, apertura, defensa local, recuperación tras perder una oleada, límite de diez oleadas sucesivas y continuidad hacia edificios conocidos. Registro: `TestResults/difficulty-balance-core.log`.
- **12/12 escenarios de práctica aprobados**, cruzando ambos mapas, las dos facciones de IA y las tres dificultades. Todos produjeron los cinco roles de unidad con recursos ordinarios; no hubo órdenes rechazadas. Primer ataque exactamente a 720 / 600 / 480 segundos, sin daño al oponente pasivo antes del período inicial. Todos ganaron contra un jugador completamente inactivo. Evidencia: `TestResults/difficulty-practice.json`.
- **4/4 partidas Normal contra Very Easy completadas**, cubriendo los cuatro cruces de facciones. Normal ganó en 989–1.582 segundos simulados. Esto comprueba progresión y cierre de partidas, no una tasa de victoria humana. Evidencia: `TestResults/matches.json`.
- **Compilación y validación de ambos mapas aprobadas** en Unity 6000.3.26f1, CLI 1.0.0-beta.13, mediante `ProjectBuilder.ValidateContent`. Registro: `TestResults/unity-difficulty-validation.log`. El texto de preparación y la expectativa de `MenuFlowPreview` reflejan los nuevos tiempos; no se volvió a ejecutar la previsualización gráfica de menús.
- No se generó APK ni ejecutable desktop. Falta comprobar la dificultad percibida y los controles durante partidas humanas en Quest.

## Prioridad y continuidad de combate — fuentes, sin nuevo APK — 18 de octubre de 2026

La adquisición automática distingue amenazas de edificios económicos, mantiene objetivos útiles y cambia a defensores que aparecen durante un asedio. Conserva Attack Move, su destino y su cola mientras combate; la muerte del foco explícito permite adquirir otro enemigo sin un paso vacío. La navegación de combate busca una posición con línea de tiro, omite temporalmente objetivos inaccesibles y no cancela la orden por ese desvío. Se conserva la recarga, la retirada por Move, el anclaje de Hold y el foco directo. Una búsqueda sin visión usa la última posición observada durante un máximo de tres segundos y permite responder a enemigos visibles mientras tanto.

- **1041 comprobaciones del núcleo aprobadas**, frente a 981 antes del cambio. Las 60 nuevas cubren ambas facciones y órdenes de grupo de 18 soldados mixtos; las 132 específicas de combate incluyen también las regresiones anteriores de inercia. Registros: `TestResults/combat-core.log` y `TestResults/combat-after.log`. La prueba de priorizar un defensor frente a un edificio cercano falla contra el código anterior: `TestResults/combat-before.log`.
- **Unity 6000.3.26f1 / CLI 1.0.0-beta.13:** compilación y validación de ambos mapas aprobadas (`TestResults/unity-combat.log`). La compilación final y **64 pruebas de entrada sintética** de `QueuedInputChecks.Run` también pasan (`TestResults/unity-combat-input.log`). No se validaron controles físicos ni rendimiento en Quest.
- **3/4 partidas Normal contra Very Easy terminaron antes de 30 minutos simulados:** industrial/biológico en 1409 s; biológico/biológico en 1144 s; biológico/industrial en 1557 s. La última registró una orden rechazada; las otras dos, ninguna. Registros: `TestResults/combat-full.log` y `TestResults/combat-matches-2-3.log`. El cruce industrial/industrial alcanza 1800 s sin ganador: **la batería de partidas largas no está completamente aprobada**.
- Se comparó ese cruce con las fuentes de HEAD en una copia temporal: el código anterior termina a los 1754 s (`TestResults/combat-match1-before.log`); el nuevo sigue activo a los 1800 s (`TestResults/combat-match1-after.log`). La nueva prioridad cambia el desarrollo del enfrentamiento, por lo que falta revisar duración/balance en ese caso.
- Durante ese diagnóstico se confirmó además un fallo previo de `Strategist.MoveAndHold`: envía Move y enseguida Hold con `append`, pero Hold es inmediato por contrato. Así puede dejar tropas a mitad de la retirada. Una modificación **solo en la copia temporal**, que deja concluir Move antes de Hold, aumenta la actividad de combate pero tampoco termina ese cruce a los 1800 s (`TestResults/combat-diagnostic-ai-retreat.log`); no demuestra que la retirada sea la única causa. `Strategist` permanece sin cambios en esta entrega.
- No se generó APK ni ejecutable desktop. Los cambios necesitan una próxima compilación y una partida humana en Quest para evaluar comodidad, respuesta y balance.

## Continuidad del grupo y confirmación de ataque — fuentes, sin nuevo APK — 18 de octubre de 2026

Se reprodujeron dos causas concretas del problema informado: al morir el objetivo directo, los soldados de retaguardia quedaban en Stop fuera del alcance de adquisición; y confirmar Attack Move desde el menú seguido de otro A rápido sustituía la orden por Move. Ahora la retaguardia avanza combatiendo hacia la última posición conocida del objetivo, salvo que tenga una orden encolada. El segundo A inmediato después de confirmar el menú conserva la orden aceptada y no agrega otro punto a la cola.

El reconocimiento de doble A tolera bajas dentro de la selección, la desaparición del primer objetivo y pequeños cambios del puntero entre suelo y unidades. Dos objetivos enemigos vivos distintos siguen aceptando órdenes separadas. Un clic rápido en el suelo después de atacar directamente a un enemigo vivo sigue siendo Move, conservando la retirada manual para micro. Un destino rechazado deja el modo Attack Move armado para reintentar.

- **1051 comprobaciones del núcleo aprobadas**, incluyendo **142 específicas de combate**. Se verificó la continuidad de grupos dispersos de 18 y 60 unidades de ambas facciones después de que el frente mata al objetivo, y la prioridad de una retirada encolada. Registros: `TestResults/group-attack-full.log` y `TestResults/group-attack-core.log`. La regresión de retaguardia falla con el comportamiento anterior: `TestResults/group-attack-before.log`.
- **124 comprobaciones de entrada aprobadas** en Unity 6000.3.26f1 mediante `QueuedInputChecks.Run`. Incluyen cinco recorridos completos con grupos de 18 unidades por facción: ataque directo, doble A con muerte del objetivo entre pulsaciones, menú confirmado con A, menú seguido de doble A y menú confirmado con gatillo seguido de A. Se comprueba la orden recibida, la selección conservada y el avance efectivo de la retaguardia. Registro: `TestResults/unity-group-attack.log`. El comportamiento anterior del menú quedó reproducido en `TestResults/unity-group-attack-before.log`.
- `git diff --check` aprobado. `Strategist` permanece sin cambios: el arreglo de retirada de la IA se dejó expresamente para otra tarea. No se repitió la batería de partidas largas; su límite documentado en la sección anterior sigue pendiente.
- No se generó APK ni ejecutable desktop. La entrada se verificó de forma sintética en el Editor; queda pendiente probar estos cambios con mandos físicos y una partida humana en Quest.

## Agotamiento de minerales sin traslado automático — fuentes, sin nuevo APK — 18 de octubre de 2026

Se eliminó la búsqueda global de minerales que se activaba cuando no había un reemplazo local. Al agotarse un parche, la búsqueda queda centrada en ese grupo de recursos, incluso después de entregar la última carga. Si no quedan depósitos locales utilizables, el trabajador pasa a Stop y espera órdenes. Los trabajadores producidos en una base agotada tampoco salen hacia otra zona. Se conservan el reparto entre parches cercanos, las entregas parciales, la recolección local del constructor de una expansión y las órdenes explícitas de traslado.

- La prueba con minerales lejanos recordados bajo niebla **falló antes de la corrección**: los trabajadores abandonaban el grupo agotado. Registro: `TestResults/local-harvesting-before.log`.
- **98 comprobaciones de recolección aprobadas**, incluidas 52 nuevas que cruzan ambos mapas, ambas facciones y destinos lejanos explorados/desconocidos. Verifican entrega de la última carga, permanencia de todos los trabajadores en Stop, producción de un trabajador que espera órdenes y traslado manual a minerales recordados. Registro: `TestResults/local-harvesting-after.log`.
- **1103 comprobaciones del núcleo aprobadas** y `git diff --check` sin errores. Registro: `TestResults/local-harvesting-full.log`. La batería general incluye las regresiones de combate y colas anteriores; no se ejecutaron partidas largas de IA.
- Validación de simulación C# local. No se recompiló Unity ni se generó APK; la comprobación física en Quest queda pendiente.

## Easy, Normal y Hard — fuentes, sin nuevo APK — 18 de octubre de 2026

Se retiró Very Easy del núcleo y del selector. Easy y Normal conservan todos sus parámetros; Normal sigue seleccionado de forma predeterminada. Hard reacciona cada segundo, apunta a 20 trabajadores y 40 soldados, permite hasta tres barracas y adelanta producción, tecnología y expansión. Su primera oleada sale desde los seis minutos con ocho soldados, crece de a tres hasta 24 y descansa un minuto después de terminar. Ordena retirar supervivientes después de 150 segundos. No hay bonificaciones de recursos, daño, salud o visión.

- **1176 comprobaciones de simulación aprobadas**: 1104 del núcleo y 72 correspondientes a **12 escenarios de práctica**, cruzando ambos mapas, ambas facciones y las tres dificultades. En todos se produjeron los cinco roles de unidad, se respetaron población y presupuesto, no hubo daño ofensivo durante la preparación y la IA venció al rival completamente pasivo. Primer ataque exactamente a 600 / 480 / 360 segundos para Easy / Normal / Hard. Registro: `TestResults/easy-normal-hard-core.log`; resultados: `TestResults/difficulty-practice.json`.
- Hard terminó sus cuatro escenarios entre 384 y 420 segundos simulados. Se registraron dos órdenes de construcción rechazadas por unidades que ocupaban el sitio en el conjunto de doce escenarios: una Normal biológica en Training y una Hard biológica en Rift. Las demás ejecuciones no registraron rechazos. Esto verifica funcionamiento contra un rival pasivo; no demuestra balance ni una tasa de victoria contra jugadores humanos o Normal.
- **296 comprobaciones de menú y 41 vistas renderizadas aprobadas** en Unity 6000.3.26f1 con CLI 1.0.0-beta.13. Se comprobó el ciclo Easy → Normal → Hard → Easy, los textos de preparación, la creación de la IA Hard y su conservación al iniciar otra partida y jugar revancha. Registro: `TestResults/unity-easy-normal-hard-menu.log`. Inspeccionada la captura `TestResults/Menus/05-hard.png`.
- `git diff --check` aprobado. La lógica de retirada `MoveAndHold` no se modificó; su corrección continúa reservada para otra tarea. No se ejecutaron partidas largas entre IA, no se generó APK ni ejecutable desktop y falta probar la dificultad percibida en Quest.

## Puntero rojo y confirmación de ataque — fuentes, sin nuevo APK — 19 de octubre de 2026

Attack Move desde el panel y la rueda mantiene rojas la mira del tablero y el rayo del mando mientras espera un destino separado. Confirmar con A/gatillo envía la orden y conserva el rojo durante 0,8 segundos; doble A también produce esa confirmación. Cancelar o cambiar a movimiento/construcción limpia la señal. Un destino inválido conserva el modo armado. La expiración visual no detiene el ataque de las unidades.

- **146 comprobaciones de combate aprobadas**, incluidas cuatro nuevas que verifican, en ambas facciones, dos unidades enemigas sucesivas y una estructura destruidas con un único Attack Move, seguido de la llegada al destino. No hizo falta cambiar las reglas de combate ya implementadas. Registro: `TestResults/attack-pointer-combat.log`.
- **312 comprobaciones de Unity y 26 vistas aprobadas**: `AttackPointerPreview` (36 / 8 vistas), `QueuedInputChecks` (124) y `ActionWheelPreview` (152 / 18 vistas). Incluyen el clic real del panel, la entrada sintética de doble A y gatillo, el cierre de la rueda sin ejecutar sobre el mapa y la confirmación posterior con A. Registro: `TestResults/unity-attack-pointer.log`.
- Inspeccionadas las capturas `TestResults/AttackPointer/industrial-normal.png` y `industrial-double-a.png`: la mira pasa de guías cian con centro blanco a guías y centro rojos, conservando el borde oscuro. Las pruebas comprueban también el material del rayo del mando.
- Unity 6000.3.26f1 / CLI 1.0.0-beta.13; compilación de scripts y render del shader aprobados. `git diff --check` sin errores. No se generó APK ni ejecutable desktop; la señal y su duración requieren comprobación con el mando físico en Quest.


## Hints de todos los botones y clic del stick — 20 de octubre de 2026

Los hints laterales incluyen las cuatro direcciones, gatillo, A/X, B/Y, grip y clic del stick, con líneas al mando y textos según los hotkeys actuales. El clic alterna toda la ayuda; mantenerlo o soltarlo no repite el cambio. Ocultar los hints permite seguir abriendo la rueda. El antiguo gesto de asignar manteniendo el stick queda eliminado: se asigna desde abajo → Assign group → grupo → gatillo.

- `unity-controller-hints.log`: `FRONTIER_COMFORT_AUDIO passed` (57 comprobaciones, 4 vistas) y `FRONTIER_UNIFIED_WHEEL passed` (660 comprobaciones, 20 vistas). Incluyen anclajes distintos para cada botón, texto sin recorte, visibilidad tras cerrar/pausar y navegación con ayuda oculta.
- `unity-hint-hotkeys.log`: `FRONTIER_HOTKEYS passed` (122), `FRONTIER_MENU_FLOW passed` (304), `FRONTIER_QUEUED_INPUT passed` (172) y `FRONTIER_CONTROL_GROUPS passed` (590). Entrada sintética por `CommanderInput.Update` en ambas manos, clic y remap a A/X, actualización de etiquetas y protección contra asignación por el antiguo gesto. La prueba de guía se actualizó para entrar al submenú antes de revisar sus textos.
- Inspeccionadas `TestResults/ComfortAudio/right-controller.png` y `left-controller.png`. Scripts compilados y `git diff --check` aprobado. Validación local en Editor; sin nuevo APK ni prueba física de legibilidad/comodidad en Quest.


## Edificios directamente en Selection — 20 de octubre de 2026

Se eliminó el nivel intermedio Build/Back. Con uno o varios trabajadores, Selection muestra los siete edificios y Back vuelve a la raíz. La dirección sostenida resalta un edificio al entrar; una nueva pulsación de gatillo lo elige y la colocación requiere una confirmación posterior sobre el mapa.

- `UnifiedWheelValidationBatch.Run` aprobado con salida 0: `TestResults/unity-direct-selection.log`. Pasan rueda unificada (642 comprobaciones), hotkeys con entrada sintética para ambas manos (120) y acciones de rueda para ambas facciones (450), además de las regresiones de grupos, menús, colas, rally, ataque y habilidades.
- Inspeccionada `TestResults/UnifiedWheel/industrial-direct-selection.png`: siete opciones directas, Back y textos completos. Guía actualizada a Worker → Selection → edificio. `git diff --check` aprobado. Validación de Editor; sin nuevo APK ni prueba física en Quest.


## Una sola rueda de grupos — 20 de octubre de 2026

Groups se abre desde arriba y ofrece ambas acciones sobre el mismo grupo: gatillo recupera; pulsar el stick físico asigna inmediatamente la selección actual. Desaparecen Assign group, el hint inferior y la acción de abajo en la raíz. Dentro de Groups abajo sigue eligiendo el grupo 3. Asignar cierra la rueda y conserva la visibilidad de los hints; fuera de Groups el hotkey de hints sigue siendo editable.

- `UnifiedWheelValidationBatch.Run` aprobado, salida 0: `TestResults/unity-single-groups.log`. Rueda (570 comprobaciones), hotkeys (126), grupos (496) y acciones (442), más regresiones de menús, colas, rally, ataque y habilidades.
- Se comprueban los cuatro grupos, selección de varios trabajadores y edificios, asignación con selección vacía, pulsación inmediata sin esperar liberación, centro sin asignación, gatillo solo para recuperar, pérdida de foco/seguimiento y cambio de selección. Entrada real del router con OVR sintético para ambas manos, incluso con hints en A/X y selección en stick; mantener y soltar la asignación no produce selección ni órdenes sobre el mapa.
- Inspeccionadas `TestResults/UnifiedWheel/industrial-direct-groups.png` y `industrial-root.png`: ayuda Trigger/Stick click en una misma rueda y tres accesos cardinales sin Assign. `git diff --check` aprobado. Validación de Editor, sin nuevo APK ni prueba física en Quest.
- Vistas del mando actualizadas: `unity-single-groups-controller.log`, 57 comprobaciones y cuatro capturas aprobadas. Inspeccionado `TestResults/ComfortAudio/right-controller.png`, con tres hints direccionales y sin acceso inferior.


## Menú de inicio frente al visor — 20 de octubre de 2026

El menú previo a la partida en Quest usa su propia posición en mundo, a 1,25 m frente al visor, después de recibir una pose rastreada. Ignora el desplazamiento guardado de la consola de partida y no se mueve cuando se restaura o recoloca el tablero. Settings y preparación conservan esa ubicación; arrastrar o restablecer el menú inicial no modifica las preferencias de la consola en partida. Recuperar foco, volver de suspensión o ponerse el visor de nuevo antes de jugar recalcula la posición frente al usuario.

- `StartupMenuPreview.Run` aprobado, salida 0: `TestResults/unity-startup-menu.log`. 32 comprobaciones específicas y tres vistas, más 304 de menús y 57 de colocación/mando/audio.
- Tablero simulado a más de 20 m, ancla restaurada tardíamente, tamaño distinto, ausencia inicial de seguimiento, apertura nueva desde otra pose, pausa/foco/visor, acceso real a Settings → Reposition board, cancelación, arrastre lejos del mapa y Reset position. Se conservan el ancla, tamaño, preferencia de consola en partida y comportamiento desktop; sin ancla se sigue solicitando colocación únicamente al iniciar match.
- Inspeccionadas `TestResults/StartupMenu/01-restored-board-start.png` y `03-fresh-start-new-position.png`, renderizadas con las poses de cámara reales del escenario. `git diff --check` aprobado. Pruebas de Editor con poses sintéticas, sin nuevo APK ni validación física en Quest.


## Selection persistente y cancelación solo en el tablero — 20 de octubre de 2026

Selection permanece abierta después de una confirmación correcta. Cada nueva pulsación de Trigger ejecuta una orden; mantener el gatillo no repite. Soltar el stick conserva el cierre por inactividad de 0,8 s. Cancel / refund se filtra exclusivamente en la rueda y sus hints, de modo que iniciar o vaciar producción no altera los sectores. La consola conserva cancelación y reembolsos, incluido cancelar construcción.

- `TestResults/unity-repeat-selection.log`: pasaron navegación (570), hotkeys con OVR sintético para ambas manos (126), menús (304), colas (172), grupos (496) y rally (126). El batch se detuvo en una expectativa antigua de cierre automático al armar una habilidad; se actualizó esa prueba al cierre por stick.
- Continuación aprobada, salida 0, mediante `CasterAbilityPreview.Render`: `TestResults/unity-repeat-selection-actions.log`. Habilidades (83), presentación de casters (29), efectos (15), puntero de ataque (92), colas (172) y acciones de rueda (456 comprobaciones, 20 vistas).
- Ambas facciones: tres pulsaciones sin soltar la dirección producen exactamente tres unidades y tres cobros; sostener Trigger no agrega órdenes; los sectores y el resaltado sobreviven a colas que empiezan o se vacían. Dos investigaciones pueden encolarse en la misma rueda. Se comprueba el cierre tras soltar stick, recuperación después de recursos insuficientes, confirmación sin filtrarse al mapa y cancelación mediante clics reales del panel. Clear rally sigue disponible y elimina su resaltado cuando desaparece.
- Inspeccionadas `TestResults/ActionWheel/industrial-repeat-production.png` y `biological-construction-panel.png`: rueda sin Cancel y botón de cancelación conservado en el tablero. Guía y documentación sincronizadas; `git diff --check` aprobado. Validación en Editor, sin nuevo APK ni prueba física en Quest.


## Caída, restos y desvanecimiento de unidades — 20 de octubre de 2026

Las bajas móviles producen un evento proyectado de muerte. Caen durante 0,5 s, permanecen como cuerpos hasta los 3,5 s y se disuelven hasta desaparecer a los 6 s. Trabajadores, scouts y artillería industriales añaden pequeños fragmentos. Los restos no participan en simulación, colisiones, selección ni capacidad de ejército. La pérdida de visión y la cancelación de construcción no producen cadáveres.

- `bash scripts/check-core.sh --deaths-only`: 24 comprobaciones aprobadas en `TestResults/death-core-checks.log`, incluidas bajas mediante ataques reales y privacidad en ambas facciones. `--combat-only`: 180 comprobaciones aprobadas en `TestResults/death-combat-checks.log`.
- `DeathVisualPreview.Run`, salida 0: `TestResults/unity-death-visuals.log`, `FRONTIER_DEATH_VISUALS passed: 18 checks, 7 views`. Prueba los doce modelos, permanencia/disolución, pausa mediante tiempo visual congelado, niebla, selección, límites de 64 cuerpos/90.000 vértices, reutilización sin objetos por muerte, shader y limpieza.
- Inspeccionadas `TestResults/Deaths/00-alive.png`, `death-080.png` y `death-475.png`: modelos vivos, cuerpos caídos sin insignias y disolución gradual. `git diff --check` aprobado. Sin nuevo APK ni prueba física de Quest; el rendimiento en el visor queda por medir.


## Tanque Hammer más grande — 20 de octubre de 2026

Escala visual 1,8× para el Hammer industrial, aplicada al modelo completo, torreta y animación, salida de disparos, aro de selección y restos. No cambia el catálogo ni el balance de simulación.

- `DeathVisualPreview.Run`: salida 0, 18 comprobaciones y siete vistas en `TestResults/unity-larger-tank.log`. Inspeccionada `TestResults/Deaths/00-alive.png`: Hammer claramente más voluminoso que Bastion y Lancer.
- `SoldierVisualPreview.Render`: salida 0 en `TestResults/unity-larger-tank-combat.log`. Animación (32 comprobaciones), armas (15), orientación (32) y presentación de impactos aprobadas, incluidas torreta, retroceso, origen de disparos, reutilización y ambas facciones.
- `git diff --check` aprobado. Validación de Editor; sin APK nuevo ni prueba física en Quest.


## Doble A rápido y ataques repetidos — 20 de octubre de 2026

Se mantiene la regla elegida por el usuario: cada Attack Move requiere dos pulsaciones; un A individual sigue dando Move. La ventana de 0,55 s no tiene intervalo mínimo. Se corrigen dos causas de pérdida del gesto: desplazamiento físico pequeño sobre un tablero alejado y cambio de un cuerpo aliado a otro entre pulsaciones. Quest usa un margen de 3,5 cm, con mínimo de 1,5 celdas; no se cambia el muestreo físico de OVR.

- Reproducción anterior a la corrección: `TestResults/unity-rapid-attack-repro.log`. El tiempo de pulsaciones 0 / 16 / 40 / 100 / 500 ms se reconocía, pero el desplazamiento de 2,5 cm sobre el tablero reducido fallaba.
- `AttackPointerPreview.Render` aprobado, salida 0, en `TestResults/unity-rapid-attack.log`: puntero/entrada real del router (124 comprobaciones), colas e interpretación del gesto (200), acciones de rueda (456). Las pruebas usan OVR sintético para ambas manos, A/X y la orden reasignada a grip; varias parejas consecutivas redirigen Attack Move a distintos destinos y conservan el pulso rojo. Se mantienen los casos de una pulsación, tercer toque, pausa, cambio de selección/modificador, foco enemigo, retirada, muerte del objetivo y colas sin duplicados.
- `git diff --check` aprobado. Evidencia de Editor con entrada sintética; sin nuevo APK ni comprobación física de la cadencia del botón en Quest.

## Construcción con unidades propias en la zona — 20 de octubre de 2026

La orden reserva y cobra la obra una sola vez. Las unidades propias que interfieren, incluido el constructor y unidades en Hold, salen mediante la navegación y física normales; conservan órdenes, carga y colas. La huella se vuelve sólida y empieza el progreso cuando queda libre. Un grupo apretado puede pedir espacio a sus filas exteriores. Cancelar o destruir una obra pendiente libera la reserva y el desplazamiento temporal. Los enemigos y las restricciones de terreno, recursos y edificios siguen bloqueando la colocación; sin salida accesible se rechaza antes de cobrar. El indicador propio muestra **CLEARING AREA** durante la espera.

- `bash scripts/check-core.sh --construction-clearance-only`: 60 comprobaciones en `TestResults/construction-clearance-core.log`, ambas facciones. Incluye constructor dentro, tanques, grupos de 16 y 56 unidades, Hold, colas de movimiento y construcción, recolección/carga, pausa, cancelación/destrucción, nuevos ocupantes, enemigos, privacidad y falta de salida. La prueba de navegación anterior que exigía rechazar una unidad propia en el borde se actualizó para comprobar salida segura y cobro único.
- `bash scripts/check-core.sh`: salida 0, **1773 comprobaciones aprobadas** en `TestResults/construction-clearance-regression.log`, con navegación, colisiones, combate, economía, IA, producción y colas.
- `ConstructionPreview.Render`: salida 0, **52 comprobaciones aprobadas** en `TestResults/unity-construction-clearance.log`. Colocación real desde el menú sobre una unidad propia, conservación de Hold y transición del indicador en ambas facciones. Inspeccionada `TestResults/Construction/industrial-clearing-area.png`.
- `git diff --check` aprobado. Validación de núcleo y Editor; sin nuevo APK ni prueba física en Quest.

## Menú tras relocalización y descarte de tablero lejano — 20 de octubre de 2026

Se cubre una corrección tardía de la pose del visor, sin eventos de foco ni pérdida de seguimiento, que dejaba el menú fijo en las coordenadas anteriores. El menú se recupera frente al visor ante saltos o al quedar fuera de alcance. Al restaurar un tablero, se valida su distancia después de 0,75 s de seguimiento relativo estable; más de 3 m horizontales o 2,5 m verticales descarta la ubicación guardada. Un inicio solicitado durante esa comprobación espera y luego inicia con el tablero cercano o pide una colocación nueva.

- Reproducción previa: `TestResults/unity-startup-relocation-repro.log`, falla al corregir la pose después del primer fotograma válido.
- `StartupMenuPreview.Run`: salida 0 en `TestResults/unity-startup-relocation.log`. Menú inicial: **36 comprobaciones y 3 vistas**; colocación guardada: **25 comprobaciones** con poses sintéticas y archivo temporal aislado; flujo de menús: **304 comprobaciones y 42 paneles**; comodidad/audio: **57 comprobaciones y 4 vistas**. Total: **422 comprobaciones**.
- Se verifica conservación del tablero cercano y del tablero de piso, descarte a distancia y en otro nivel, origen provisional, correcciones tardías de visor/ancla, pausa, inicio anticipado, cancelación, colocación manual y ausencia de descarte durante una partida activa. No se modifica el archivo real de colocación en estas pruebas; la eliminación del ancla mediante el servicio de Meta queda pendiente de comprobación física.
- `git diff --check` aprobado. Validación de Editor; sin nuevo APK ni prueba física en Quest.

## Menús unificados, referencia del catálogo y artillería — 20 de octubre de 2026

Inicio y pausa comparten acceso a Guides y Settings. Settings agrupa Controls, Audio y Play space; Nueva partida contiene únicamente facción, rival, dificultad, mapa, comenzar y volver. Las guías de unidades y estructuras muestran los 26 arquetipos con retratos de sus mallas, datos base, requisitos, producción y habilidades. La navegación conserva el menú de origen y la pausa al retroceder por submenús. Los avisos de operaciones siguen visibles, incluso al repetir el mismo resultado o esperar la restauración del tablero.

- `StartupMenuPreview.Run`: salida 0 en `TestResults/unity-menu-unification.log`; **37 comprobaciones de inicio**, **25 de colocación guardada**, **57 de comodidad/audio**, y la primera pasada de menús (**332 comprobaciones, 50 vistas**). Se conservan los escenarios de relocalización y colocación diferida.
- `HotkeyValidationBatch.Run`: salida 0 en `TestResults/unity-unified-menu-hotkeys.log`; **129 comprobaciones de hotkeys**, **335 del flujo final de menús con 50 vistas**, **200 de entrada con colas** y **496 de grupos con 10 vistas**. Incluye Back anidado, acceso desde inicio/pausa, configuración fuera de New Match, avisos repetidos y espera/cancelación de restauración. Se inspeccionaron inicio, pausa, nueva partida y las pestañas de Settings.
- `CatalogReferencePreview.ValidateAndRender`: salida 0 en `TestResults/unity-catalog-reference.log`; **3031 comprobaciones**, recorriendo las **26 fichas antes del match y las mismas 26 en pausa**, con 26 PNG en `TestResults/CatalogReference`. Se verifican valores del catálogo, requisitos, hechizos, producción, retratos, navegación por rayo, desbordes y conservación de configuración, economía, órdenes y selección. Se inspeccionaron Bombardier, Arsenal y Oracle.
- `UnifiedWheelPreview.Render`: **570 comprobaciones y 18 vistas**, salida 0 en `TestResults/unity-wheel-fast-close.log`. `ActionWheelPreview.Render`: **456 comprobaciones y 20 vistas**, salida 0 en `TestResults/unity-selection-wheel-fast-close.log`. La rueda permanece abierta a los 0,3 s de centrar el stick y cierra a los 0,45 s, con plazo configurado de **0,4 s**, manteniendo confirmaciones repetidas y continuidad entre niveles.
- `SoldierVisualPreview.Render`: salida 0 en `TestResults/unity-bombardier-scale.log`; **32 comprobaciones y 152 vistas**, más **15 comprobaciones de armas con 11 vistas**, **8 vistas de impactos** y **32 comprobaciones de orientación**. Bombardier usa el mismo multiplicador visual **1,8×** que Hammer, incluido el rig y el arma; se inspeccionó `TestResults/Soldiers/biological-strikes.png`.
- `DeathVisualPreview.Run`: salida 0 en `TestResults/unity-bombardier-remains.log`; **18 comprobaciones y 7 vistas**. Se inspeccionó `TestResults/Deaths/00-alive.png` para comparar ambas artillerías con la infantería; se conserva la escala en los restos.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Todos los registros finales anteriores terminan correctamente, sin errores de compilación, excepciones ni fallos nativos. `git diff --check` aprobado. Pruebas de Editor y capturas; sin nuevo APK ni validación física de comodidad/legibilidad en Quest.

## Amber Wastes, niebla con volumen y caídas por tipo — 20 de octubre de 2026

Amber Wastes agrega un desierto de 160 × 160 con ocho zonas de recursos en total: dos iniciales y seis expansiones, cada una con seis minerales y dos gases. El relieve tiene cinco mesetas, veinte rampas anchas y areniscas decorativas sobre terreno bloqueado. Terreno, recursos y salidas tienen simetría de 180°. Los dos jugadores conservan las mismas condiciones iniciales. El mapa se elige desde Nueva partida junto a The Rift y Training Grounds.

- `python3 scripts/generate-desert-map.py --check`: el JSON coincide con su generador. `bash scripts/check-core.sh --desert-map-only`: **240 comprobaciones**. Suite completa `bash scripts/check-core.sh`: **2013 comprobaciones aprobadas**, salida 0 en `TestResults/desert-full-core.log`. Incluye rutas a los 64 recursos con campamentos desarrollados y radio de artillería, construcción de bases y extractores, entrega real de gas, desplazamiento físico por mesetas y condiciones iniciales equivalentes al intercambiar facciones.
- `DesertMapPreview.Render`: salida 0 en `TestResults/unity-desert-map.log`, **47 comprobaciones y 8 vistas**. Rayos sobre las veinte rampas coinciden con la altura del terreno; mesetas, rampas y suelo usan geometría real. La malla del terreno tiene **105.712 vértices y 52.856 triángulos**. Las vistas de inicio de ambos jugadores ocultan al rival; la vista de inspección revela únicamente su copia de presentación. Inspeccionadas `TestResults/DesertMap/survey-angle.png` y `mesa-expansion.png`.
- `FogPreview.Run`: salida 0 en `TestResults/unity-fog-layers.log`, **64 comprobaciones y 12 renders** sobre los tres mapas. Comprueba tres capas acotadas a 12.675 vértices, ausencia de colliders, densidad mayor en terreno sin explorar, transparencia completa en celdas visibles, movimiento reproducible, pausa, recorte con zoom/pan, reconstrucción y salida. Inspeccionadas `TestResults/Fog/desert-relief.png` y `rift-overview.png`; no se cambian las reglas de visibilidad ni el filtrado de enemigos.
- `DeathVisualPreview.Run`: salida 0 en `TestResults/unity-death-collapse.log`, **79 comprobaciones y 13 vistas**. Comprueba los doce modelos, conservación de la última pose visible, patas biológicas plegadas, chasis asentados, soldados caídos desde el apoyo posterior, contacto en suelo/rampas, pausa, niebla, disolución y límites de cuerpos/vértices. Inspeccionadas las vistas finales `biological-settled.png` e `industrial-settled.png` en `TestResults/Deaths`.
- `MenuFlowPreview.Render`: salida 0 en `TestResults/unity-desert-menu-flow.log`, **338 comprobaciones y 51 paneles**, incluido el ciclo de los tres mapas. `PointerPreview.Render`: salida 0 en `TestResults/unity-desert-terrain-pointer.log`, **8 vistas**, con intersecciones exactas en suelo/rampa/altura, zoom, desplazamiento, rotación, recorte, niebla, selección, pausa y salida.
- `ProjectBuilder.RetainRuntimeShaders`: salida 0 y `FRONTIER_SHADERS_RETAINED` en `TestResults/unity-runtime-shader-retention.log`; los shaders de niebla y restos quedan referenciados en GraphicsSettings para próximas compilaciones. Los registros finales no contienen errores de compilación, excepciones ni fallos de shader. `git diff --check` aprobado.
- Validación del núcleo, Editor y capturas locales con Unity **6000.3.26f1**. Sin nuevo APK ni prueba física de comodidad, legibilidad o rendimiento en Quest.

## Confirmación visual de rueda y cinco pedidos en producción grupal — 20 de octubre de 2026

Las confirmaciones aceptadas de la rueda producen un pulso verde de 0,35 s, un borde sobre el sector y una marca pequeña que no cubre el título. Cuando la acción cierra la rueda queda una confirmación compacta que sigue el mando sin retener la entrada. Producción grupal usa sus cinco espacios para pedidos reales, muestra las producciones paralelas antes de las pendientes y conserva el quinto pedido con `+N MORE` para los adicionales; las barras pequeñas reflejan el progreso de cada productor activo.

- `ProductionQueuePreview.Render`: salida 0 en `TestResults/unity-group-production-slots.log`; **116 comprobaciones y 26 paneles**, ambas facciones. Dos y tres barracas, cinco pedidos exactos, desbordes de 3/7/10, pedidos duplicados, avance con igual longitud total, progreso paralelo, pausa, regreso a cancelación individual, productores incompletos y selección mixta. También pasaron recolección (**18 paneles**) y menús (**338 comprobaciones, 51 paneles**). Inspeccionada `TestResults/ProductionQueue/industrial-three-barracks-full.png`: cinco tarjetas y `+10 MORE` con quince pedidos.
- `UnifiedWheelPreview.Render`: salida 0 en `TestResults/unity-wheel-confirmation.log`; **710 comprobaciones y 22 vistas**. Confirmación y expiración, pulsaciones repetidas sin soltar la dirección, gatillo sostenido sin repetir, cambio de opción durante el pulso, grupos, cierre sin bloquear entrada, pérdida de seguimiento/foco y cambio de selección. Inspeccionadas `TestResults/UnifiedWheel/industrial-confirmed-selection.png` y `industrial-confirmed-group.png`: texto legible y confirmación visible con rueda abierta/cerrada.
- `ActionWheelPreview.Render`: salida 0 en `TestResults/unity-wheel-confirmation-actions.log`; **458 comprobaciones y 20 vistas**. Las órdenes rechazadas conservan el motivo sin mostrar confirmación de éxito; producción repetida y comportamiento de Selection siguen funcionando.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Registros finales sin errores de compilación, excepciones ni errores de shader; `git diff --check` aprobado. Pruebas de Editor con entrada sintética y capturas; sin nuevo APK ni prueba física en Quest.

## Conservar la posición del menú al volver de una partida — 20 de octubre de 2026

La pose del menú inicial se conserva durante la partida y se recupera al salir, tanto desde pausa como desde resultado. Recuperar foco, suspensión o seguimiento estando cerca tampoco borra el arrastre elegido. Girar la cabeza y desplazamientos cercanos mantienen la posición. La recuperación automática solo se aplica si el menú queda a más de 3,25 m del visor con seguimiento válido; Reset position y una nueva apertura de la app siguen colocándolo delante del jugador. El tablero y la preferencia de consola relativa a él permanecen independientes.

- Reproducción previa en `TestResults/unity-lobby-position-repro.log`: después de arrastrar el menú, entrar y salir de una partida sin mover el visor no conservaba la posición.
- `StartupMenuPreview.Run`: salida 0 en `TestResults/unity-retain-lobby-position.log`. **51 comprobaciones de posición y 4 vistas**, **25 de tablero guardado**, **338 de menús y 51 paneles**, y **57 de comodidad/audio**. Total: **471 comprobaciones**. Incluye regreso cercano desde pausa/resultado, posición arrastrada, giro del visor, interrupciones cercanas, regreso distante, estabilidad en la partida siguiente, apertura nueva y ausencia de modificaciones a las preferencias del tablero/consola.
- Inspeccionada `TestResults/StartupMenu/04-retained-menu-after-match.png`: menú en su posición desplazada después de salir. Registro final sin errores de compilación, excepciones ni errores de shader; `git diff --check` aprobado. Unity **6000.3.26f1**, CLI **1.0.0-beta.13**; poses sintéticas en Editor, sin nuevo APK ni prueba física en Quest.

## Ubicar el tablero directamente desde Nueva partida — 20 de octubre de 2026

MatchSetup agrega **Position board** junto a **START MATCH**, disponible tanto si falta el mapa como para reajustar uno existente. La acción abre la colocación sin inicio pendiente: confirmar deja el tablero listo en la misma pantalla; cancelar conserva la pose anterior si existía. Start Match mantiene el inicio diferido cuando falta colocar el tablero. Los ajustes avanzados permanecen en Settings.

- `StartupMenuPreview.Run`: salida 0 en `TestResults/unity-new-match-board-placement.log`; **51 comprobaciones de posición y 4 vistas**, **25 de tablero guardado**, **342 de menús y 52 paneles**, y **57 de comodidad/audio**. Los casos nuevos verifican acceso directo, cancelación sin tablero previo, confirmación sin iniciar partida y reajuste cancelado que conserva posición y tamaño.
- Inspeccionada `TestResults/Menus/05-direct-board-placement.png`: Back, Position board y START MATCH comparten una fila sin solapamientos. Registro final sin errores de compilación, excepciones ni errores de shader; `git diff --check` aprobado. Validación en Editor con poses sintéticas, sin nuevo APK ni prueba física en Quest.

## Informe de partida, gráficas y build order — 20 de octubre de 2026

El resultado abre Overview, Graphs y Build order. Se registran ambos bandos durante la simulación y se publica una copia inmutable al terminar. El informe compara economía, población, producción, bajas, APM y ráfagas; separa recursos entregados, gastos y devoluciones. La historia de construcción/producción/investigación conserva tiempos y resultados, con paginación independiente por jugador. Las estadísticas rivales no entran en PlayerView durante la partida.

- `bash scripts/check-core.sh --match-report-only`: **56 comprobaciones aprobadas**, registro `TestResults/match-report-core.log`. Comprueba ambas facciones, agrupación/rechazo/duplicados/colas para APM, pausas, entrega y pérdida de carga, devoluciones, producción completada/cancelada, pérdida del productor, investigación, picos, resultado natural/rendición, tiempo cero y separación de partidas.
- `bash scripts/check-core.sh`: **2069 comprobaciones aprobadas**, salida 0 en `TestResults/match-report-regression.log`.
- `MatchReportPreview.Run`: **849 comprobaciones y 13 paneles**, salida 0 en `TestResults/unity-match-report.log`. Dos estrategas generan seis minutos de historia; se prueban las siete gráficas, sus mallas finitas, todas las páginas de ambos jugadores, navegación real por rayos, revancha, orden vacío y duración cero. Texto y contenido sin recortes ni solapamiento con el pie.
- `MenuFlowPreview.Render`: **342 comprobaciones y 52 paneles**, salida 0 en `TestResults/unity-report-menu-regression.log`; conserva los flujos existentes de inicio, pausa, resultado, revancha y menú principal.
- Inspeccionadas `TestResults/Reports/01-overview.png`, `02-graph-army.png`, `02-graph-apm.png` y `03-build-ai.png`. Series distinguibles por color y trazo; tiempos, estado y población de cada orden legibles.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Registros finales sin excepciones ni errores de compilación; `git diff --check` aprobado. Validación en Editor, sin nuevo APK ni prueba física en Quest. El informe no se persiste entre sesiones.

## Historial persistente y base de replays — 20 de octubre de 2026

Match history permite volver a abrir informes desde el menú principal. Se guardan archivos locales por ID de partida con fecha, resultado, mapa, facciones, dificultad, informe completo, reglas originales y tramos ordenados de comandos. Las partidas abandonadas conservan su informe; las interrumpidas conservan el último tramo confirmado. Hay detección de formatos/reglas incompatibles y de datos dañados. Un fallo de escritura se muestra con Retry save. No se incluye aún un reproductor visual.

- `bash scripts/check-core.sh --recording-only`: **12 comprobaciones aprobadas**, registro `TestResults/match-recording-core.log`. Copias independientes de comandos, rechazos que consumen secuencias, exclusión de pausas, DTOs/versionado y reconstrucción exacta de una partida de seis minutos con sus estadísticas.
- `bash scripts/check-core.sh`: **2081 comprobaciones aprobadas**, salida 0 en `TestResults/match-history-regression.log`.
- `MatchHistoryPreview.Run`: **240 comprobaciones y 7 paneles**, salida 0 en `TestResults/unity-match-history.log`. Escritura real en directorio temporal aislado; un nuevo lector recupera todas las estadísticas. Se reconstruyen desde disco todos los comandos, cada checkpoint y el estado/informe finales sin ejecutar el AI. Se comprueban incompatibilidades, IDs únicos, revancha, abandono desde pausa, interrupciones, aislamiento de archivos corruptos, paginación y navegación sin iniciar una partida.
- La misma prueba provoca un fallo de almacenamiento de forma intencional, verifica el aviso y Retry save y confirma que el registro original se guarda cuando el destino vuelve a estar disponible. La advertencia de almacenamiento de ese registro es esperada; el proceso termina correctamente.
- `MenuFlowPreview.Render`: **342 comprobaciones y 52 paneles**, salida 0 en `TestResults/unity-history-menu-regression.log`. Flujos existentes de inicio, guías, settings, pausa, resultado y revancha conservados.
- Inspeccionadas `TestResults/History/01-main.png`, `03-history.png` y `05-saved-overview.png`: acceso desde inicio, lista paginada legible y estadísticas históricas completas. Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Sin nuevo APK ni prueba física en Quest; no se afirma determinismo entre plataformas/builds distintos. Contrato de compatibilidad y limitaciones en `docs/match-history.md`.

## Replays e interfaz de espectador — 20 de octubre de 2026

Watch replay abre las grabaciones locales desde historial o informe. El tablero revela ambos jugadores y el panel mantiene economía, workers/army, población, estructuras y producción con progreso. Incluye pausa, velocidades 0,5–4×, saltos de quince segundos, timeline por rayo, reinicio, pan/zoom y salida al historial. Los controles de la mano activa tienen hints propios. No se ejecuta el AI ni se aceptan órdenes de juego durante la reproducción; observar, buscar y salir no modifican la grabación.

- `bash scripts/check-core.sh`: **2081 comprobaciones aprobadas**, salida 0 en `TestResults/replay-core.log`.
- `ReplayPlaybackChecks.Run`: **30 comprobaciones aprobadas** en `TestResults/unity-replay-validation.log`. Reconstrucción exacta de mundo e informe, comandos y checkpoints en el mismo tick, pausa/velocidad, saltos acotados, reinicio, resultado, grabaciones interrumpidas o de duración cero, seis variantes de archivos inválidos y ráfaga de 2500 comandos. Esa primera ejecución integrada se detuvo después por una referencia destruida en el helper de captura del panel; se corrigió el helper y se repitió la parte afectada.
- `ReplayValidation.RunPresentation`: salida 0 en `TestResults/unity-replay-presentation.log`; sesión **23**, panel de espectador **476 / 6 vistas**, historial **251 / 7 vistas**, menús **342 / 52 vistas**. Comprueba navegación por rayo, timeline pausado/en reproducción/al finalizar, recursos de ambos lados, producción paralela y pendiente, construcción/investigación, páginas independientes, aviso de divergencia y retorno al historial.
- `ReplaySessionChecks.Run` ampliado: **25 comprobaciones**, salida 0 en `TestResults/unity-replay-board-session.log`. Colocación diferida y cancelación; mapa y catálogo congelados; vista omnisciente separada de PlayerView; renderizado de ambos bandos antes y después de rebobinar; reutilización de terreno/modelos; conservación de pan/zoom y pausas externas; archivo intacto y restauración de preferencias de la siguiente partida.
- `ReplayInputChecks.Run`: **57 comprobaciones y 2 vistas**, salida 0 en `TestResults/unity-replay-input.log`. Transporte por flancos, ausencia de repetición al sostener stick, navegación/zoom en pausa, regreso a neutral después de grip, controles fijos aunque cambien hotkeys, hints de ambas manos, bloqueo por interrupción del sistema y archivos intactos. Se regeneraron las capturas con aspecto horizontal para incluir las dos columnas de hints completas.
- Se inspeccionaron `TestResults/Spectator/02-spectator-playing.png`, `04-production-full.png`, `05-production-page-2.png` y `06-error.png`: contadores, filas de producción, paginación, errores y transporte sin recortes ni superposiciones.
- `git diff --check` aprobado; registros finales de presentación, sesión y entrada sin errores de compilación ni excepciones. Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Validación del núcleo y Editor con entrada sintética; no se generó APK ni se probó comodidad, legibilidad o rendimiento en un Quest físico. No se afirma compatibilidad de simulación entre plataformas/builds diferentes.

## Animaciones de curación y ataque — 20 de octubre de 2026

Las cuatro habilidades tienen una secuencia de impacto/pulso y desaparición, con identidades por facción: descargas angulares y cruces de reparación para Consorcio; filamentos, esporas y semillas para Simbiosis. El perímetro de curación conserva el radio circular real, y las capas abiertas permiten ver las unidades. La presentación sigue el terreno y consume solo eventos de PlayerView; no cambia daño, curación, energía ni recargas.

- Núcleo completo: **2081 comprobaciones aprobadas**, salida 0 en `TestResults/ability-animation-core.log`.
- `CasterAbilityPreview.Render`: salida 0 en `TestResults/unity-ability-animation.log`; controles de habilidades **83 / 8 vistas**, modelos **29**, efectos **132 / 9 vistas**, puntero de ataque **124 / 12 vistas**, entrada con colas **200**, rueda **458 / 20 vistas**. El batch también conserva las tres vistas de modelos de casters.
- Los efectos se verifican en varias fases, cámaras oblicua/superior, cuatro esquinas y dos combinaciones de tamaño/zoom. Se comprueba reproducción exacta de vértices/colores al rebobinar, pausa por tick, expiración antes de retirar el evento, geometría finita, límite de 32 eventos/24.576 vértices, ninguna creación por lanzamiento y ninguna modificación del estado autoritativo. Niebla, eventos futuros/desconocidos y tablero oculto siguen descartando efectos.
- Inspeccionadas `TestResults/Abilities/pulse-18.png`, `industrial-with-units.png`, `biological-with-units.png`, `top-down.png` y `rift-ramps.png`. Tipos distinguibles, unidades legibles y trazos sobre rampas. Se mantienen nueve capturas de fases y escenarios en esa carpeta.
- `git diff --check` aprobado; registro final sin errores de compilación, excepciones ni errores de shader. Unity **6000.3.26f1**, CLI **1.0.0-beta.13**. Pruebas y capturas de Editor; sin APK nuevo ni validación física de comodidad/rendimiento en Quest.

## Igualar tamaño visual de Hammer y Bombardier — 20 de octubre de 2026

Hammer aumenta su multiplicador visual de 1,8 a 3,2 para compensar su chasis estrecho; Bombardier conserva 1,8. Los límites de sus mallas sobre el suelo quedan en **9,349 y 9,436 celdas²** respectivamente (menos de 1 % de diferencia), conservando la forma alargada del tanque. El indicador de rol del tanque mantiene el tamaño del de Bombardier para dejar visible el chasis. La escala central se aplica también al arma, rig, origen del disparo y restos; catálogo, colisiones y reglas no cambian.

- `UnitVisualPreview.RenderArtillery`: salida 0 en `TestResults/unity-artillery-size.log`; comparación de ambos modelos a la misma escala desde arriba y en ángulo, en `TestResults/Artillery/`. Ambas capturas inspeccionadas. La misma ejecución completa `DeathVisualPreview.Run`: **79 comprobaciones y 13 vistas**.
- `git diff --check` aprobado. Unity Editor **6000.3.26f1**; sin APK nuevo ni prueba física en Quest.

## Densidad de niebla en Settings — 20 de octubre de 2026

Settings → Visuals permite elegir 0 %, 25 %, 50 %, 75 % o 100 % desde inicio y pausa. La preferencia se guarda en el dispositivo y se aplica inmediatamente; 100 % conserva la densidad original y 0 % desactiva el renderizado de las capas de neblina. La visibilidad del terreno y los enemigos siguen sujetos a las reglas de exploración.

- `FogPreview.Run`: **70 comprobaciones y 15 renders**, salida 0 en `TestResults/unity-fog-density.log`. Comparación de píxeles a 0/50/100 %, conservación de visibilidad, guardado y reutilización de malla/material. Inspeccionadas las tres capturas de densidad.
- `MenuFlowPreview.Render`: **347 comprobaciones y 54 paneles**, salida 0 en `TestResults/unity-fog-settings.log`. Cambio desde inicio y pausa sin avanzar la partida; inspeccionadas `02-settings-visuals-off.png` y `07-paused-visuals.png`.
- `git diff --check` aprobado. Unity Editor **6000.3.26f1**; sin APK nuevo ni prueba física en Quest.

## Pasos entre mesetas y rampas del desierto — 20 de octubre de 2026

Se retraen los bordes interiores de las mesetas y se recolocan sus rampas para eliminar los encuentros de 2×2 celdas y las franjas bajas de 2–4 celdas. Cada una de las veinte rampas conserva doce celdas de ancho y tiene ocho celdas de terreno bajo despejado a su salida. Los dos antiguos cruces de esquinas quedan separados por pasos de 12×8. Las ocho zonas de recursos, posiciones iniciales y simetría de 180° se conservan; cambia el espacio disponible alrededor de las alturas.

- `python3 scripts/generate-desert-map.py --check`: JSON sincronizado con su generador.
- `bash scripts/check-core.sh --desert-map-only`: **262 comprobaciones aprobadas**, salida 0 en `TestResults/desert-passages-core.log`. Incluye accesos de artillería a los 64 depósitos con bases/extractores construidos, movimiento real por rampas, construcción, recolección y condiciones iniciales iguales.
- `DesertMapPreview.Render`: **67 comprobaciones y 11 vistas**, salida 0 en `TestResults/unity-desert-passages.log`. Continuidad de altura en ambos extremos y todo el ancho de las veinte rampas, coincidencia de raycast y superficie. Inspeccionadas `central-mesa.png`, `north-passage.png`, `corner-passage.png` y `outer-passage.png` en `TestResults/DesertMap/`.
- `git diff --check` aprobado. Unity Editor **6000.3.26f1**; sin APK nuevo ni prueba física en Quest.

## Altura y hints durante la colocación — 20 de octubre de 2026

Se eliminan los presets Table/Floor de Play space. Durante la colocación, A/X sube y B/Y baja 2,5 cm por toque; mantener continúa a 30 cm/s después de 0,3 s, dentro de 0–1,5 m. Trigger confirma, Grip cancela, stick cambia tamaño/orientación y su clic alterna los hints específicos del mando. Los controles son fijos e independientes de los hotkeys de juego. Ajustar una superficie detectada toma su altura visible y pasa al plano manual; cancelar restaura también los ajustes de altura/detección anteriores. Desktop usa Page Up/Down, clic izquierdo y Escape.

- `BoardPlacementPreview.RunBatch`: salida 0 en `TestResults/unity-board-placement.log`. Colocación **52 comprobaciones / 2 vistas**, inicio **51 / 4 vistas**, restauración **25**, menús **350 / 54 paneles**, comodidad/audio **57 / 4 vistas**, rueda **710 / 22 vistas**. Ambos mandos, remaps, pausa, entrada/salida retenida, límites, restauración exacta y ausencia de órdenes de juego durante la colocación.
- `BoardPlacementPreview.Run`: repetición dirigida con cámara de inspección corregida; **52 / 2 vistas**, salida 0 en `TestResults/unity-placement-hints.log`. Inspeccionadas ambas capturas de `TestResults/BoardPlacement/` y `TestResults/Menus/02-settings-play-space.png`.
- `git diff --check` aprobado. Unity Editor **6000.3.26f1**; entrada/poses sintéticas, sin escrituras de anchors, APK nuevo ni validación física en Quest.

## Controles revisados de colocación — 20 de octubre de 2026

Grip achica y Trigger agranda al mantenerlos; stick arriba/abajo ajusta altura y a izquierda/derecha gira. A/X confirma y B/Y cancela. El clic de stick alterna los hints. Se actualizan mando, Play space y guía, conservando controles fijos y liberación obligatoria tras entrada, interrupción o salida. Los ajustes continuos usan tiempo sin escalar y un paso máximo de 50 ms para evitar saltos después de un cuadro lento.

- `BoardPlacementPreview.RunBatch`: salida 0 en `TestResults/unity-placement-controls.log`. Colocación **68 comprobaciones / 2 vistas**, inicio **51 / 4 vistas**, restauración **25**, menús **350 / 54 paneles**, comodidad/audio **57 / 4 vistas**, rueda **710 / 22 vistas**. Verificados ambos mandos, cambios simultáneos de tamaño/altura, límites, stick en reposo, remaps, pausa, confirmación, cancelación y bloqueo de entradas sostenidas.
- Inspeccionadas ambas capturas de `TestResults/BoardPlacement/` con los hints nuevos. `git diff --check` aprobado.
- Unity Editor **6000.3.26f1**; entrada sintética y pasos de tiempo explícitos para movimiento continuo, sin escrituras de anchors, APK nuevo ni prueba física en Quest.

## Selección agrupada por tipo — 21 de octubre de 2026

El panel muestra una tarjeta por definición, con cantidad y vida media de sus miembros seleccionados. Trigger/A/X filtra ese tipo al soltar sobre la tarjeta; Grip al iniciar la pulsación quita el tipo completo de la selección. Desktop usa clic y Shift + clic. Se elimina la espera de doble clic y la eliminación por mantener sin modificador. La acción no incorpora aliados fuera de la selección ni elimina entidades del mundo.

- `SelectionGridPreview.RunValidation`: salida 0 en `TestResults/unity-selection-types-final.log`. Selección **177 comprobaciones / 8 vistas**, ambas facciones/manos, límites de 60 unidades + 20 edificios, conteos, vida media, exclusión de aliados no seleccionados, Shift de ambos lados, cancelación por pausa/foco/bajas y bloqueo de releases superpuestos.
- Regresiones aprobadas: rueda unificada **710**, grupos **496**, rueda de acciones **458**, colas de entrada **200**, rally **126**, menús **350**, producción **116**, además del panel de recolección.
- Se corrigió un texto recortado de la guía detectado en la primera ejecución. Inspeccionadas las capturas agrupadas de ambas facciones y la tarjeta roja al quitar un tipo, en `TestResults/SelectionGrid/`. Guía y referencias de controles actualizadas.
- `git diff --check` aprobado. Unity **6000.3.26f1**, CLI **1.0.0-beta.14**. Entrada sintética en Editor, sin APK ni prueba física en Quest.

## Densidad de niebla sin transparencia del tablero — 21 de octubre de 2026

La mezcla de las capas de niebla utilizaba los mismos factores para color y alfa, reduciendo la opacidad de terreno ya opaco. Se separa la composición del alfa para conservar esa cobertura mientras la densidad sigue controlando únicamente el aspecto de la neblina.

- Regresión reproducida antes del cambio con captura RGBA real, terreno activo y fondo transparente: con densidad de 25 %, el alfa mínimo del terreno del desierto era **189/255**. Fallo esperado en `TestResults/unity-fog-opacity-before.log`.
- `FogPreview.Run` tras la corrección: **148 comprobaciones y 45 renders**, salida 0 en `TestResults/unity-fog-opacity-fixed.log`. Los tres mapas conservan **255/255** en todos los píxeles que eran opacos, con densidades 25/50/75/100 %, desde arriba y en ángulo, contra la referencia de 0 %. El exterior conserva transparencia; las zonas visibles conservan su color; la neblina sigue variando y la visibilidad de enemigos no cambia.
- Las pruebas leen RGBA directamente del render target, evitando la captura estática RGB que no conserva alfa. Inspeccionados `TestResults/Fog/desert-opaque-25.png` y `desert-opaque-100.png`. `git diff --check` aprobado.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**. Sin APK ni validación del compositor en un Quest físico.

## Iluminación mejorada experimental — 21 de octubre de 2026

Configuración → Visuales permite alternar entre Actual (predeterminada) y Mejorada (experimental), antes de comenzar o desde pausa. La preferencia se aplica inmediatamente y se guarda. La prueba combina luz suave y relleno ambiental según la temática, reflejos moderados en objetos y dos mallas de sombras de contacto que siguen el terreno. No añade luces con mapas de sombras ni posprocesado de pantalla completa.

- `LightingPreview.Render`: **98 comprobaciones y 33 renders** de los tres mapas, con detalles de ambas facciones. Compara Actual/Mejorada/Actual, persistencia, conservación de partida/selección/colocación/niebla, sombras en rampas, geometría acotada, ausencia de colliders, liberación al reconstruir y cobertura alfa. Enemigos recordados, omitidos o fuera de visión no producen sombras dinámicas incluso con niebla al 0 %. Fase aprobada en `TestResults/unity-experimental-lighting.log`; imágenes en `TestResults/Lighting/`. Inspeccionadas las comparaciones de Neon Loop y detalles de Desser Alert y Forest Watch.
- La ejecución integrada inicial detectó comprobaciones antiguas del pie de menú que medían el texto dentro del nuevo `statusBanner` en otro sistema de coordenadas. Se actualizaron los fixtures para medir el banner; no cambió la interfaz por este ajuste.
- Regresión final con **salida 0** en `TestResults/unity-lighting-settings-fog-final.log`: menú **366 comprobaciones / 61 paneles**, localización **328 / 0 desbordamientos**, replay **30**, espectador **528 / 7 paneles** y niebla **160 / 60 renders**. Inspeccionado el selector en español. `FRONTIER_SHADERS_RETAINED` confirma que el shader nuevo queda referenciado para próximas compilaciones.
- Validación mediante Unity Editor con Burst desactivado solo para esa ejecución. No se generó una nueva APK ni se midieron rendimiento, comodidad o aspecto en el visor físico.

## Modelos en construcción, entrenamiento y producción — 21 de octubre de 2026

Las tarjetas Build/Train y los cinco espacios de producción muestran miniaturas de los modelos reales de ambas facciones, reutilizando la proyección cacheada de `SelectionPortrait`. Se conservan nombres, costos, resaltado SELECTED, progreso, cancelación individual y desbordamiento de colas agrupadas. Las investigaciones y los espacios vacíos retiran cualquier miniatura anterior.

- `ConstructionPreview.Render`: **224 comprobaciones**, salida 0 en `TestResults/unity-construction-models.log`. Clic sobre las miniaturas, cambio de edificio, selección persistente y colocación real. El caso de academia detectó un costo seleccionado recortado; se compactaron las unidades M/G y se verificó el texto completo.
- `ProductionCancelPreview.Render`: salida 0 en `TestResults/unity-production-models-cancel.log`; cancelación **168 comprobaciones / 14 vistas**, ayuda **140 / 38**, producción **776 / 26**, menús **350 / 54** y rueda **458 / 20**, además de 18 paneles de recolección. Verifica que cada retrato corresponda al pedido, cambie aunque la longitud de la cola se mantenga y desaparezca al investigar o vaciarse; comprueba nombres/costos legibles y acciones reales sobre las miniaturas.
- Inspeccionadas capturas de construcción y producción de ambas facciones, incluidas academia seleccionada, colas llenas y unidad pendiente detrás de una investigación. `git diff --check` aprobado.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**. Capturas y entrada sintética en Editor; sin APK ni validación física en Quest.

## Selección rápida hacia abajo en el stick — 21 de octubre de 2026

Abajo entra directamente a Quick select con Idle workers resaltado; arriba dentro de esa rueda elige All army. Gatillo reemplaza la selección, conserva las órdenes y cierra con confirmación visual. Los contadores se actualizan desde PlayerView y se revalidan al confirmar; un resultado vacío conserva la selección previa. Se añade el hint lateral, la referencia de controles y una segunda página de la guía Selection para no ocultar las nuevas entradas.

- `QuickSelectionPreview.Render`: **228 comprobaciones / 12 vistas**, ambas facciones y manos. Incluye trabajadores Stop/Hold, trabajo/traslado/entrega/órdenes pendientes, todos los roles militares lejos de la base, muertos/incompletos/enemigos/recordados, cambios entre apuntar y confirmar, confirmación sin recentrar, consumo del release, pausa, cierre y conservación de órdenes/grupos.
- Regresiones: rueda unificada **724 / 22 vistas**, grupos **496 / 10**, acciones **466 / 20**, menús **356 / 55**. Salida 0 en `TestResults/unity-quick-selection-final.log`; la guía permite llegar a las nuevas entradas y volver sin reanudar la partida.
- Inspeccionadas la rueda de dos opciones, el hint completo y la página de guía en `TestResults/QuickSelection`, `TestResults/UnifiedWheel` y `TestResults/Menus`. Referencias Markdown/CSV actualizadas desde el catálogo exportado. `git diff --check` aprobado.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**. Validación de Editor con entrada sintética; sin APK ni prueba física en Quest.

## Replay congelado durante seek — 21 de octubre de 2026

Los saltos conservan la última escena y las proyecciones de ambos jugadores mientras se reconstruye la simulación. El tablero omite animación/efectos y el panel reutiliza economía/producción hasta publicar el destino. El cálculo añade un presupuesto cooperativo de 2 ms por fotograma, comprobado entre pasos, órdenes y checkpoints; cada operación individual sigue siendo indivisible. Cámara, navegación, transporte y progreso continúan disponibles, y ±15 segundos acumula sobre el destino pendiente.

- `ReplayValidation.Run`: **1.255 comprobaciones**, salida 0 y marcador `FRONTIER_REPLAY_VALIDATION passed` en `TestResults/unity-replay-seek-freeze.log`.
- Motor **33**, entrada **57 / 2 vistas**, sesión **30**, panel de espectador **528 / 7 vistas**, historial **251 / 7 vistas**, menús **356 / 55 vistas**. Se comprueban presupuesto agotado, comandos simultáneos, reconstrucción exacta, inmovilidad de poses y contadores entre lotes, retargeting, reinicio visual único, pausas, retroceso, interrupción del visor y salida durante un seek. Los archivos de grabación permanecen intactos.
- Inspeccionado `TestResults/Spectator/07-seeking-frozen.png`: estadísticas congeladas, destino y transporte visibles; texto y controles dentro del panel. `git diff --check` aprobado.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**. Sin cambios de reglas ni formato de grabación. Sin APK ni medición de FPS en Quest físico; el presupuesto no garantiza la duración de un paso/checkpoint individual.
## Inglés, español e idioma del sistema — 21 de octubre de 2026

Configuración → Idioma comparte el selector entre inicio y pausa: Sistema, English y Español. Por defecto resuelve el idioma del dispositivo y usa inglés cuando todavía no está disponible. La elección explícita se guarda y se aplica en vivo. Los catálogos JSON admiten idiomas futuros; la traducción queda en la presentación, con los identificadores, comandos y archivos de partida originales.

- `LocalizationPreview.Run`: **321 comprobaciones**, salida 0 y marcador `FRONTIER_LOCALIZATION passed` en `TestResults/unity-localization-final.log`, con Unity **6000.3.26f1** / CLI **1.0.0-beta.14**. Incluye sistema/elección explícita, persistencia, respaldo, los **243 templates**, actualización de etiquetas ocultas, selección y simulación intactas, navegación real por rayos y pausa conservada.
- Se recorrieron ajustes, guías de las dos facciones, controles desktop/Touch, HUD, hints de mando y colocación, resultados, historial y replay en español. La auditoría registra **667 textos visibles**, ocho términos compartidos permitidos y **cero desbordamientos**. Capturas y pares fuente/traducción en `TestResults/Localization/`; se inspeccionaron inicio, idioma, hints, selección rápida, guía de habilidades y espectador.
- Las regresiones en inglés aprobaron **356 comprobaciones de menús** con 55 paneles, **30 de sesión de replay** y **528 de espectador** con siete paneles. El registro final no contiene errores de compilación ni excepciones de pruebas. `git diff --check` aprobado.
- Se corrigieron descripciones que excedían sus tarjetas, composiciones que mezclaban idiomas y la ambigüedad entre el botón físico B y un número de productor. Las pruebas cubren hints completos y ambas habilidades de cada facción.

No se generó APK ni ejecutable desktop. Quedan pendientes la detección del idioma y la legibilidad física en Quest; las comprobaciones de sistema del Editor usan valores controlados de `SystemLanguage`.

## Identidad visual de Little Legion en los menús — 21 de octubre de 2026

Se aplica el concepto 05 al inicio, pausa, configuración, guías, historial, replay y paneles de juego: logo geométrico blanco/turquesa, iconos, superficies redondeadas con borde fino y degradado suave. Nueva partida ocupa la fila principal; las cuatro opciones secundarias forman una cuadrícula de dos columnas. El HUD usa una marca compacta y conserva espacio para recursos, selección y producción.

- `MenuStylePreview.Render`: **984 comprobaciones / 22 paneles**, en inglés y español y con las dos facciones. Incluye navegación real por rayos sobre tarjetas e iconos, inicio/pausa/reanudación, persistencia de selección y simulación, geometría visible del logo y textos dentro de sus límites. Capturas en `TestResults/MenuStyle/`.
- Regresión de `MenuStylePreview.Validate`: menús **366 / 61 paneles**, localización **328 / 0 desbordamientos**, replay **30**, espectador **528 / 7**, selección **177 / 8** y producción **776 / 26**, registradas en `TestResults/unity-menu-style-validation.log`. El batch se detuvo después por dos expectativas antiguas de nombres de mapas en `HarvestingPreview`; se actualizaron a Forest Watch y Neon Loop manteniendo las mismas comprobaciones.
- Repetición dirigida `MenuStylePreview.ValidateGameplay`: **salida 0** en `TestResults/unity-menu-style-gameplay.log`, con producción **776 / 26**, recolección **18 paneles**, menús **366 / 61** y rueda unificada **724 / 22** aprobados.
- Inspeccionados inicio, pausa, HUD, ajustes visuales, historial y replay. Se corrigieron bandas diagonales del degradado del panel mediante filas de vértices a la misma altura. `git diff --check` aprobado.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**, Burst desactivado solo para las ejecuciones de validación. Capturas y entrada sintética en Editor; sin APK nuevo ni validación física de legibilidad o rendimiento en Quest.

## Acabado de menús y rueda de controles — 21 de octubre de 2026

Segunda pasada sobre el concepto 05: marco doble con canal oscuro y luz superior, botones con bisel y sombra contenida, paneles informativos hundidos, logo con borde suavizado y sombreado mínimo, iconos de peso uniforme y texto dinámico a doble resolución. La barra de mover/restablecer tiene menor peso visual. Se unifican pestañas y selecciones de ajustes, guías, atajos, referencias y replay; audio incorpora segmentos con llenado parcial. El foco ilumina gradualmente los botones conservando sus límites. La rueda comparte el acabado y conserva sus radios y ángulos de selección.

- `MenuStylePreview.Render`: **1.540 comprobaciones / 44 capturas**, incluidas entrada/salida de foco por rayos sobre iconos, retorno exacto al estado original, navegación y paneles de las dos facciones en inglés y español. Aprobado en `TestResults/unity-menu-polish-validation.log` antes de la fase de localización.
- La nueva resolución de fuente detectó dos etiquetas demasiado estrechas en Selección rápida en español. Se amplió su caja de 146 a 260 unidades dentro de los semicírculos, conservando fuente y posición. `TestResults/unity-menu-polish-final.log` registra localización **328 / 0 desbordamientos**, menús **366 / 61 paneles**, replay **30**, espectador **527 / 7** y selección **177 / 8** aprobados.
- La fase de producción detectó una actualización de estilo sobre un panel de preview ya destruido. Se conserva la salida temprana de reconstrucción cuando el panel ha desaparecido y se omiten referencias Unity destruidas antes de actualizar el acabado. Repetición dirigida con **salida 0** en `TestResults/unity-menu-polish-gameplay-final.log`: producción **776 / 26**, recolección **18 paneles**, menús **366 / 61** y rueda **724 / 22** aprobados.
- Inspeccionados inicio normal y con foco, nueva partida, ajustes de audio, guías, atajos, referencia de estructuras, HUD y selección rápida. Corregidos separación del pie, representación exacta del volumen y restauración de los controles Mover/Restablecer al cancelar interacción.
- Última captura de contraste de opciones no disponibles y revisión estricta del español: **salida 0**, **328 comprobaciones / 0 desbordamientos** en `TestResults/unity-menu-polish-contrast-render.log`, con menús, replay y espectador aprobados. `git diff --check` aprobado.
- Validación en Unity Editor **6000.3.26f1** con CLI **1.0.0-beta.14**, Burst desactivado solo durante los batches. Sin APK nuevo ni prueba de legibilidad, comodidad o rendimiento en Quest físico.

## Menús de colores planos — 21 de octubre de 2026

Se conserva la estructura del concepto 05 y se sustituye el acabado ornamental por rellenos uniformes y fondos opacos. Se retiran degradados, biseles, sombras, marcos y divisores/subrayados decorativos del panel, tarjetas, logo y rueda. El foco cambia el color completo; el suavizado de siluetas solo modifica alfa. Se mantienen la resolución de texto, los indicadores funcionales y los límites de interacción.

- `MenuStylePreview.Validate`: **salida 0** en `TestResults/unity-flat-menus-validation.log`. Estilo **1.540 comprobaciones / 44 capturas**, localización **328 / 0 desbordamientos**, menús **366 / 61 paneles**, replay **30**, espectador **527 / 7**, selección **177 / 8**, producción **776 / 26**, recolección **18 paneles** y rueda **724 / 22** aprobados.
- Inspeccionadas capturas del inicio, audio, HUD de trabajador, rueda de órdenes y selección rápida en español. La revisión comprueba rellenos uniformes, contraste, distribución y texto completo. `git diff --check` aprobado.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**, Burst desactivado solo para la validación. No se generó APK: la reducción del parpadeo y la legibilidad al mover la cabeza requieren comprobación física en Quest.


## Contornos de texto SDF para Quest — 21 de octubre de 2026

Se sustituye el texto bitmap de `LegacyRuntime.ttf` por TextMesh Pro/SDF en menús, HUD, rueda, avisos del mando y contadores del tablero. El atlas estático compartido incluye inglés, español y los símbolos usados; los avisos flotantes reemplazan las cuatro copias desplazadas de UGUI Outline por un contorno SDF compartido. Se conservan tamaños, distribución, traducciones e interacción. El proyecto ya utiliza MSAA 4×; esta corrección actúa sobre las letras.

- Migración funcional: **salida 0** en `TestResults/unity-sdf-menus.log`, con estilo **1.540 / 44 paneles**, localización **328 / 0 desbordamientos**, menús **366 / 61**, replay **30**, espectador **527 / 7**, selección **177 / 8**, producción **776 / 26**, recolección **18 paneles** y rueda **724 / 22**. La revisión de las imágenes detectó que el capturador antiguo reemplazaba el atlas TMP con una textura blanca; se corrigió la captura. También se reemplazó el símbolo de leyenda de gráficos no disponible por una raya compatible.
- Validación final y capturas con los materiales reales: **salida 0** en `TestResults/unity-sdf-ui-final.log`, con estilo **1.540 / 44**, localización **328 / 0 desbordamientos**, menús **366 / 61**, replay **30**, espectador **527 / 7**, rueda **724 / 22** y contadores del mapa **96 / 8**. Sin avisos de caracteres faltantes ni errores de shader. Inspeccionados inicio, selección rápida y avisos en español.
- `TextEdgesPreview.Render`: **48 comprobaciones / 12 renders**, salida 0 en `TestResults/unity-sdf-edges.log`. Panel y aviso con contorno, a 0°, 40° y 60°, en 48 × 48 y 192 × 192 píxeles. Verifica enlace al atlas real, cobertura parcial de píxeles en los contornos, glifo visible y hueco interior de la O. Capturas en `TestResults/TextEdges/`.
- Unity **6000.3.26f1**, CLI **1.0.0-beta.14**, Burst desactivado solo durante la validación. `git diff --check` aprobado. No se generó APK ni se verificó movimiento, estéreo o rendimiento en Quest físico; la mejora percibida en el visor queda pendiente de esa prueba.


## Prueba aislada sin contorno de letras — 21 de octubre de 2026

Se retira únicamente el contorno SDF de avisos flotantes del mando y confirmaciones. Los textos de los paneles ya tenían ancho de contorno cero; todos comparten ahora su material. Se elimina el material específico de avisos y el preparador mantiene desactivado `OUTLINE_ON`, con ancho y suavidad de contorno cero. No se cambian fondos, colores, geometría, tamaños ni transiciones del menú en esta prueba.

- `TextRenderingSetup.ValidatePlainText`: **salida 0** en `TestResults/unity-text-no-outline.log`. Localización **328 / 0 desbordamientos**, menús **366 / 61 paneles**, replay **30**, espectador **527 / 7** y bordes de glifo **24 / 6 renders** aprobados.
- Inspeccionados los avisos en español sin contorno. Atlas y suavizado SDF conservados; no quedan componentes Outline ni asignaciones de material con contorno en el texto de juego. `git diff --check` aprobado.
- Capturas y entrada sintética en Unity Editor **6000.3.26f1**, CLI **1.0.0-beta.14**. Sin APK ni prueba física en Quest; esta comparación no demuestra todavía una reducción del serrucho percibido en movimiento.
