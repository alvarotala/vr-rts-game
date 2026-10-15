# Registro de validación

## Alcance de la evidencia

La simulación, la compilación Unity, la ejecución gráfica desktop y el funcionamiento en Quest son comprobaciones diferentes. Este archivo registra cada una; no considera una compilación como prueba física de realidad mixta.

## Colisiones y navegación — 0.1.2 — 15 de octubre de 2026

Las unidades ahora usan cuerpos circulares ajustados desde el catálogo. Evitan tráfico, empujan gradualmente a aliados, ceden el paso en cruces y recalculan rutas congestionadas. Las formaciones reservan destinos separados, también cerca de las esquinas. Los enemigos y las unidades en **Mantener posición** permanecen sólidos; los edificios nuevos y las salidas de producción respetan los cuerpos completos. La geometría del cuerpo visible se ajustó a esa huella.

- **89 comprobaciones del núcleo aprobadas**, incluidas 24 regresiones de navegación y construcción. Cubren movimiento dentro de una celda, empuje a un aliado quieto en una fila, mantener posición, bloqueo enemigo, privacidad del destino enemigo, grupos mixtos de 50 unidades de ambas facciones, esquina del mapa, cruces en sentidos opuestos, corredor de una celda, edificio que corta una ruta, rampas y doce recolectores sobre un mismo depósito. En cada paso de esos escenarios se comprueban separación entre cuerpos, terreno transitable y velocidad máxima, incluso al empujar.
- **8/8 partidas entre IA terminadas**, cuatro cruces de facciones × dos dificultades del rival: 97 comprobaciones totales. Duración de 983 a 1226 segundos simulados (16,4–20,4 minutos). La posición 0 ganó 4 escenarios y la posición 1 ganó 4; estas ocho partidas deterministas confirman finalización, no equilibrio competitivo.
- **Build desktop y prueba gráfica aprobados**, con movimiento efectivo, interacción del panel y cero errores registrados. Captura `TestResults/Desktop/03-command-panel.png` inspeccionada para comprobar los cuerpos actualizados.
- **APK Android ARM64/IL2CPP compilado**, versión **0.1.2 (3)**, 58.776.225 bytes. Unity CLI no reportó errores del Editor; firma APK v2 verificada y permisos MR conservados.

APK actual: `Builds/Quest/FrontierCommand.apk`. SHA-256: `9043ba8eff06bd573e2794f6e5a13b98f7df2d8afb435ab667d1e17530f157af`.

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

Los comandos reproducibles y las rutas de resultados están en el README. Las capturas de la prueba gráfica se guardan en `TestResults/Desktop`: configuración, partida y panel contextual del trabajador.

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
