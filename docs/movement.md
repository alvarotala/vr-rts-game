# Movimiento, cuerpos y respuesta a órdenes

La física de juego se calcula en el núcleo C# a 20 Hz, en coordenadas del mapa. El tablero y su escala física no modifican las velocidades, el peso ni las colisiones. No se utilizan Rigidbody de Unity para resolver la navegación.

## Respuesta y movimiento

Una orden MOVE aceptada sustituye inmediatamente la ruta y cualquier cesión automática de paso. Conserva la velocidad del cuerpo: el siguiente paso de simulación empieza a acelerar o frenar hacia la nueva orden. No hay una espera artificial para completar la ruta anterior. La presentación empieza a girar en el mismo cuadro de la orden, antes del siguiente paso de simulación.

La velocidad cambia progresivamente, con aceleración y desaceleración ajustables. Al acercarse al destino, se calcula la distancia de frenado. STOP frena; HOLD POSITION ancla inmediatamente el cuerpo y evita que los aliados lo empujen. Los contactos con terreno pueden detener el movimiento antes de completar una frenada.

La orientación sigue las posiciones de la simulación, no el retraso de interpolación del modelo. Filtra pequeñas correcciones de evasión y limita la velocidad angular a 360 grados por segundo. Los destinos enemigos no se publican para orientar unidades visibles.

## Parámetros iniciales

En `catalog.json`, cada unidad define `collisionRadius`, `collisionMass`, `acceleration` y `deceleration`. El radio se expresa en celdas; la masa es relativa; aceleración y desaceleración se expresan en celdas/s². Los campos nuevos tienen valores positivos por defecto para catálogos existentes y el validador rechaza valores no finitos o no positivos.

| Rol | Masa | Aceleración | Desaceleración |
|---|---:|---:|---:|
| Trabajador | 1 | 18 | 26 |
| Explorador | 0,8 | 30 | 40 |
| Combate cercano | 1,6 | 14 | 22 |
| Tirador | 1,2 | 16 | 24 |
| Artillería | 4 | 8 | 12 |

Ambas facciones parten de estos valores; conservan sus velocidades y estadísticas propias. Son parámetros iniciales de jugabilidad, pendientes de ajustar con micro en el visor.

## Tráfico y contactos

Cada lote de órdenes tiene una identidad propia, incluso al reseleccionar parte de un grupo. Los grupos conservan destinos individuales y pueden aprovechar tramos rectos libres del camino sin seguir cada centro de celda. Cada atajo comprueba el radio del cuerpo, terreno y tráfico visible.

En un encuentro, el grupo más pesado tiene prioridad; los grupos de igual peso usan un orden estable. La masa del grupo corresponde a su miembro más pesado al emitir la orden. Una unidad liviana no puede desplazar físicamente a una pesada que mantiene su recorrido. En cadenas de empuje se conserva la masa del empujador original. Las unidades que ya se apartaron aceptan desplazamientos seguros sin intentar recuperar inmediatamente el mismo punto de espera.

Una búsqueda acotada encuentra sitios transitables fuera de la ruta que debe quedar libre para ceder el paso, incluso retrocediendo hasta la entrada de un corredor. La unidad conserva su destino y vuelve a su orden cuando termina de pasar el flujo prioritario; deja de buscar el apartadero si ese flujo ya no viaja. Una nueva orden del jugador cancela esa cesión. Los aliados que ya llegaron a su formación dejan pasar a los que todavía están viajando.

Todo desplazamiento comprueba terreno, cuerpos y presupuesto de velocidad. Un empuje en cadena que no cabe se revierte completo. Los enemigos permanecen sólidos y no se empujan; HOLD POSITION tampoco se desplaza.

## Validación

`NavigationChecks`, `PathfindingChecks` y `CollisionChecks` prueban el movimiento completo, no solamente si A* devuelve un camino. Cubren ambas facciones, pasillos de una y dos celdas, grupos cruzados, selecciones solapadas, unidades estacionadas, masas distintas, rampas, edificios, recolección y respuesta a nuevas órdenes. Cada paso comprueba separación, terreno y límites de velocidad; las cesiones deben conservar los destinos. `CombatMotionChecks` cubre frenado al entrar en alcance, continuidad durante el cooldown y revalidación de alcance y línea de visión después de desplazarse.

`Frontier.Editor.UnitFacingPreview.Render` comprueba orientación visual, cambios de rumbo, filtrado de zigzag, pausa, reutilización de modelos y entrega inmediata de órdenes aceptadas. Los resultados concretos se registran en [validación](validation.md). Ninguna de estas comprobaciones sustituye una prueba de comodidad y rendimiento en Quest 3.
