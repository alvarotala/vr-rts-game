# Registro de validación

## Leyenda permanente del mando — fuentes, sin nuevo APK — 19 de octubre de 2026

Una tarjeta junto al mando muestra gatillo para seleccionar, A para mover/dar órdenes, Grip para agregar/encolar, B para pausa y stick para la rueda. Las etiquetas siguen la mano activa y los botones configurados; cambian al apuntar ataques, construir, lanzar habilidades, abrir menús, guardar grupos o colocar el tablero. La tarjeta permanece con la rueda abierta y la pausa de usuario, y se oculta al perder foco o durante interrupciones del sistema.

- **367 comprobaciones de Editor aprobadas**: 177 de la leyenda y 190 de regresión de la rueda. Unity **6000.3.26f1**, salida 0; registro `TestResults/unity-controller-hints-final.log`.
- **14 vistas generadas**, diez de la leyenda y cuatro de la rueda. Se comprueban botones por mano, remaps, cambios de contexto, cancelación, guardado de grupos, visibilidad y texto completo. Inspeccionadas `ControllerHints/hand-context.png` y `wheel-context.png`: leyenda y rueda separadas, texto legible. `git diff --check` pasa.

No se generó APK ni ejecutable desktop. Las entradas y capturas son de Editor; la comodidad y legibilidad sobre el mando físico quedan pendientes en Quest.

## Rueda unificada Touch — fuentes, sin nuevo APK — 19 de octubre de 2026

El mando activo muestra un hint compacto durante la partida. Inclinar el joystick desde el centro abre la raíz: arriba Groups, derecha Selection, izquierda Unit orders y abajo Close. El gatillo entra o confirma; mantener la dirección conserva la rueda y centrar durante 0,8 segundos sin acción la oculta. Al cambiar de nivel se requiere centrar antes de elegir. Clic del joystick y doble grip dejan de abrir menús; volver al centro no recupera grupos. Dentro de Groups, gatillo recupera y clic sostenido + dirección + soltar guarda; B/Y vuelve a la raíz. Patrullar sigue pendiente.

La rueda comparte comandos con la consola: construcción, producción, investigación, rally, órdenes y habilidades. Cambiar selección, pausar, perder foco/seguimiento, mover el panel, recolocar el tablero o cambiar de mano invalida los gestos pendientes. Se consume la confirmación hasta liberar los botones antes de aceptar destinos sobre el terreno. Los perfiles de hotkeys mantienen los remaps compatibles; si guardar usaba gatillo o B/Y, ese par se intercambia con Stick click para reservar la navegación física.

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
