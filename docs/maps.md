# Formato de mapas v1

Los ejemplos ejecutables están en `Assets/Frontier/Resources/Maps`. La carga actual usa `JsonUtility`; las pruebas independientes usan `System.Text.Json` con `IncludeFields`.

| Campo | Significado |
|---|---|
| `schemaVersion` | Debe ser 1. Versiones desconocidas se rechazan. |
| `id`, `title` | Identidad estable y título visible. |
| `width`, `height` | Dimensiones enteras, entre 16 y 256. |
| `terrain` | Regiones rectangulares aplicadas en orden sobre terreno inicial transitable y construible. |
| `spawns` | Exactamente dos bases, jugadores 0 y 1, coordenadas de esquina inferior del edificio. |
| `resources` | ID único, tipo `mineral` o `catalyst`, coordenadas de celda y cantidad finita. |
| `decoration` | Elementos visuales del catálogo. v1 admite `rock`; la obstrucción se define en `terrain`. |

Una región define `x`, `y`, `width`, `height`, `elevation` (0 o 1), `walkable`, `buildable` y `ramp`. Las rampas además definen `rampAxis` (`x` o `y`) y `rampDirection` (1 o -1). Deben ser suficientemente largas para conectar alturas por incrementos máximos de 0,3 por celda; los mapas incluidos usan cuatro celdas.

Las unidades usan posiciones continuas en celdas; recursos se centran en `(x + 0.5, y + 0.5)`. Los edificios ocupan un cuadrado desde su esquina inferior. No hay coordenadas físicas, UUID de anclas, prefabs ni rutas de una computadora en el formato.

El validador comprueba tamaño, regiones, posiciones iniciales, IDs, depósitos de ambos tipos, decoración y conexión entre bases. Detecta recursos inaccesibles, depósitos superpuestos y recursos colocados sobre una base inicial. Las posiciones iniciales requieren un área plana construible de 4 × 4 y espacio exterior para generar trabajadores. El catálogo comprueba roles, productores, requisitos y costos antes de iniciar.

Los archivos del catálogo y los mapas son la fuente de verdad. Editarlos y ejecutar `bash scripts/check-core.sh` antes de regenerar un APK. El editor visual y la importación de archivos del usuario se incorporarán en su etapa posterior.
