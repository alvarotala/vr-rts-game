# Formato de mapas v1

Los ejemplos ejecutables están en `Assets/Frontier/Resources/Maps`. La carga actual usa `JsonUtility`; las pruebas independientes usan `System.Text.Json` con `IncludeFields`.

| Campo | Significado |
|---|---|
| `schemaVersion` | Debe ser 1. Versiones desconocidas se rechazan. |
| `id`, `title` | Identidad estable y título visible. |
| `theme` | Ambientación visual: `neon`, `forest` o `desert` en los mapas incluidos. |
| `width`, `height` | Dimensiones enteras, entre 16 y 256. |
| `terrain` | Regiones rectangulares aplicadas en orden sobre terreno inicial transitable y construible. |
| `spawns` | Exactamente dos bases, jugadores 0 y 1, coordenadas de esquina inferior del edificio. |
| `resources` | ID único, tipo `mineral` o `catalyst`, coordenadas de celda y cantidad finita. |
| `decoration` | Elementos visuales del catálogo. v1 admite `rock`; la obstrucción se define en `terrain`. |

Una región define `x`, `y`, `width`, `height`, `elevation` (0 o 1), `walkable`, `buildable` y `ramp`. Las rampas además definen `rampAxis` (`x` o `y`) y `rampDirection` (1 o -1). Deben ser suficientemente largas para conectar alturas por incrementos máximos de 0,3 por celda; los mapas incluidos usan cuatro celdas.

Las unidades usan posiciones continuas en celdas; recursos se centran en `(x + 0.5, y + 0.5)`. Los edificios ocupan un cuadrado desde su esquina inferior. No hay coordenadas físicas, UUID de anclas, prefabs ni rutas de una computadora en el formato.

El validador comprueba tamaño, regiones, posiciones iniciales, IDs, depósitos de ambos tipos, decoración y conexión entre bases. Detecta recursos inaccesibles, depósitos superpuestos y recursos colocados sobre una base inicial. Las posiciones iniciales requieren un área plana construible de 4 × 4 y espacio exterior para generar trabajadores. El catálogo comprueba roles, productores, requisitos y costos antes de iniciar.

Los archivos del catálogo y los mapas son la fuente de verdad. Editarlos y ejecutar `bash scripts/check-core.sh` antes de regenerar un APK. El editor visual y la importación de archivos del usuario se incorporarán en su etapa posterior.

Los nombres visibles conservan sus IDs originales para mantener las referencias de partidas, replays y multijugador:

| Mapa | ID | Tamaño | Campamentos | Depósitos totales | Modalidad |
|---|---|---|---|---|---|
| Neon Loop | `training` | 48 × 48 | 4 | 16 minerales / 4 gas | 1v1 |
| Forest Watch | `rift` | 128 × 128 | 6 | 24 minerales / 6 gas | 1v1 |
| Desser Alert | `desert` | 160 × 160 | 8 | 48 minerales / 16 gas | 1v1 |

Neon Loop y Forest Watch distribuyen cuatro minerales en un arco centrado en cada base, a unas 6,5 celdas de su centro. El gas ocupa un costado de ese mismo anillo, con espacio para un extractor de 3 × 3. El lado rival reproduce la distribución con una rotación de 180 grados. Se conserva un depósito de gas de 2400 y cuatro minerales de 1800 por campamento. Neon Loop tiene dos campamentos iniciales y dos expansiones con esquina de base en `(32,8)` y `(12,36)`. Forest Watch tiene cuatro expansiones con esquina de base en `(30,40)`, `(94,84)`, `(30,91)` y `(94,33)`, y arboledas simétricas en terreno bloqueado que dejan libres las rutas, rampas y bases.

Desser Alert conserva ocho campamentos, cada uno con seis minerales de 1800 y dos depósitos de gas de 2400. Sus mesetas, rampas y afloramientos se generan con `scripts/generate-desert-map.py`; `--check` comprueba que el JSON coincide con el generador. Los cactus y árboles son decoración visual dentro del terreno bloqueado: no añaden colisiones que la simulación desconozca.

Las tres pistas aportadas se asignan por ID: `training` usa `Resources/Music/neon-loop.ogg`, `rift` usa `forest-watch.ogg` y `desert` usa `desser-alert.ogg`. Esta selección también se conserva en replays. La presentación mantiene separado el audio del formato de simulación; las pistas largas se importan como Vorbis en streaming y carga en segundo plano. Para validar los mapas y la construcción/recolección de sus campamentos sin ejecutar toda la suite: `bash scripts/check-core.sh --maps-only`.

La validación de presentación se ejecuta con `unity run . --timeout 600 --log-file TestResults/unity-map-update-final.log --no-tail -- -buildTarget StandaloneOSX --burst-disable-compilation -executeMethod Frontier.Editor.MapUpdateValidation.Run`. Comprueba las tres ambientaciones, el selector en inglés/español, las pistas y cambios de replay, la niebla y el flujo de menús. Las vistas se guardan en `TestResults/MapThemes` y `TestResults/MapSelection`. La opción de Burst se limita a esa ejecución: evitó un cierre nativo del compilador durante la importación local, sin cambiar los ajustes del proyecto.

Validación local del 21/10/2026: 2143 comprobaciones core, 344 de mapas y 40 de expansión IA aprobadas; Unity aprobó 36 de ambientación, 18 del selector, 24 de música, 148 de niebla y 362 de menús. No se generó un player ni se validó rendimiento o audio en un Quest físico.
