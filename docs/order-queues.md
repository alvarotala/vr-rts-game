# Colas de acciones de unidades

## Entrada y respuesta inmediata

El modificador del mando derecho sigue siendo grip. Grip + A agrega acciones; A sin modificador sustituye el plan. El primer A se envía inmediatamente para conservar la respuesta del micro. Un segundo A dentro de 0,35 segundos, sobre el mismo objetivo o a menos de 1,5 celdas, convierte la orden anterior en Attack Move o Attack. Con grip, la conversión usa `replacePendingSequence`: sustituye el primer toque en su posición, tanto si ya se está ejecutando como si espera. Si terminó entre ambos toques, comienza el ataque o lo agrega al final del plan restante.

Un cambio de selección, partida, modificador, pausa, foco o contexto UI invalida los toques pendientes. Los menús y la rueda conservan la propiedad de sus botones y liberaciones; sus acciones no producen órdenes sobre el mapa. Doble trigger se aplica únicamente a clics sobre una unidad propia, con coincidencia de tipo y proximidad de doce celdas. El arrastre sigue permitiendo empezar fuera del mapa.

Grip + mantener trigger quieto durante 0,5 segundos quita la unidad seleccionada bajo el cursor. Una pulsación del stick sigue abriendo/cerrando la rueda, incluso sosteniendo grip. El doble grip conserva su apertura alternativa.

## Simulación

- `GameCommand.append` conserva la acción actual y agrega una intención a cada unidad aplicable. `pendingOrders` admite 32 elementos por unidad, además de la acción activa. Producción sigue usando su cola independiente de cinco pedidos.
- `activeOrderSequence` y `UnitOrder.sourceSequence` vinculan los toques de una misma intención. `replacePendingSequence` convierte una orden concreta sin borrar las demás.
- Al encolar movimiento se asignan destinos de formación únicos y la misma identidad/prioridad de tráfico para todo el grupo. Al activar se recalcula el camino desde la posición real, conservando velocidad, masa y reglas de colisión. Las órdenes inmediatas reutilizan el camino calculado al planificar.
- La ejecución revalida objetivos, visión, ocupación, requisitos y recursos. Una acción que dejó de ser válida se descarta con un aviso al propietario y se intenta la siguiente. Una orden inválida recibida no borra el plan válido existente.
- La construcción encolada no reserva ni consume recursos. Cobra una sola vez al activarse; puede omitirse si faltan recursos o requisitos entonces. Gather de gas requiere un extractor operativo al encolarse.
- Move y Attack Move terminan al llegar; Attack cuando muere/desaparece el objetivo o se pierde visión; Repair cuando completa la reparación; Gather tras entregar una carga. Build industrial espera la finalización; Build biológico inicia el crecimiento y continúa el plan. Sin órdenes posteriores se conservan los comportamientos económicos previos.
- Stop y Hold son inmediatos y vacían pendientes. Stop conserva frenado; Hold ancla. No hay persistencia de partidas ni edición individual de estas acciones en esta entrega.

## Presentación y privacidad

`PlayerView` copia secuencia activa, destino y órdenes pendientes únicamente para entidades propias. Los enemigos visibles o recordados no exponen sus planes. La presentación resuelve entidades y recursos desde esa vista; no consulta posiciones enemigas ocultas.

`UNIT ORDER PLAN` muestra el total pendiente y los próximos tres pedidos de una unidad representativa, indicando si hay más. Con grupos se agregan los conteos por unidad. El mapa dibuja hasta ocho destinos, incluida la acción actual, para el menor ID seleccionado que tenga pendientes. Los números y conexiones siguen terreno y rampas, se recortan al tablero y no tienen colliders. Las líneas indican la secuencia de objetivos, no un camino A* garantizado.

Se limpia al deseleccionar, morir, detenerse, salir de partida o esconder el tablero. Las órdenes futuras no giran anticipadamente el modelo: el evento de orientación inmediata se emite al cambiar la secuencia activa, tanto al recibir una orden como al avanzar la cola en simulación.

## Comprobaciones

`CommandQueueChecks` cubre la simulación con ambas facciones. `QueuedInputChecks` comprueba los gestos y el enrutamiento con entradas sintéticas; `OrderQueuePreview` verifica el HUD y los destinos, y produce imágenes para inspección. Resultados concretos y límites: [registro de validación](validation.md).
