# Colas de acciones de unidades

## Entrada y respuesta inmediata

El modificador del mando derecho sigue siendo grip. Grip + A agrega acciones; A sin modificador sustituye el plan. El primer A se envía inmediatamente para conservar la respuesta del micro. Un segundo A dentro de 0,35 segundos, sobre el mismo objetivo o a menos de 1,5 celdas, convierte la orden anterior en Attack Move o Attack. Con grip, la conversión usa `replacePendingSequence`: sustituye el primer toque en su posición, tanto si ya se está ejecutando como si espera. Si terminó entre ambos toques, comienza el ataque o lo agrega al final del plan restante.

Un cambio deliberado de selección, partida, modificador, pausa, foco o contexto UI invalida los toques pendientes. Las bajas de miembros de la selección no anulan el doble toque. La desaparición del primer enemigo o una pequeña variación del rayo entre terreno y aliados tampoco lo convierten accidentalmente en Move; dos enemigos vivos distintos siguen pudiendo encolarse como objetivos separados. Una orden directa sobre un enemigo vivo seguida de un clic rápido en el suelo conserva Move para permitir retirada y micro. Los menús y la rueda conservan la propiedad de sus botones y liberaciones; sus acciones no producen órdenes sobre el mapa. Doble trigger se aplica únicamente a clics sobre una unidad propia, con coincidencia de tipo y proximidad de doce celdas. El arrastre sigue permitiendo empezar fuera del mapa.

Confirmar Attack Move del menú sobre el mapa ejecuta la orden inmediatamente. Un segundo A dentro de la ventana y sobre el mismo lugar se consume como parte de esa confirmación: no reemplaza el ataque por Move ni duplica una acción con grip. Una pulsación posterior o en otro lugar permite dar una nueva orden. Si el destino se rechaza, el modo Attack Move permanece activo hasta corregirlo o cancelarlo.

`GameSession.ChooseAttackMove` arma el mismo modo desde el panel y la rueda antes de notificar el cambio. `AttackPointerActive` comparte el estado de la mira del tablero y el rayo del mando: rojo mientras se elige destino y durante 0,8 segundos tras aceptar Attack/Attack Move, incluido doble A o una orden encolada. La pausa y el resultado ocultan esa señal; cancelar, construir, salir de la partida o aceptar otra clase de orden limpian la confirmación temporal. El temporizador visual no modifica órdenes ni destinos de la simulación.

Grip + mantener trigger quieto durante 0,5 segundos quita la unidad seleccionada bajo el cursor. Inclinar el stick desde el centro abre la rueda unificada, incluso sosteniendo grip; se entra a cada opción con el gatillo. Clic del stick y doble grip no la abren. El clic sostenido se usa para guardar grupos únicamente dentro de Groups.

## Simulación

- `GameCommand.append` conserva la acción actual y agrega una intención a cada unidad aplicable. `pendingOrders` admite 32 elementos por unidad, además de la acción activa. Producción sigue usando su cola independiente de cinco pedidos.
- `activeOrderSequence` y `UnitOrder.sourceSequence` vinculan los toques de una misma intención. `replacePendingSequence` convierte una orden concreta sin borrar las demás.
- Al encolar movimiento se asignan destinos de formación únicos y la misma identidad/prioridad de tráfico para todo el grupo. Al activar se recalcula el camino desde la posición real, conservando velocidad, masa y reglas de colisión. Las órdenes inmediatas reutilizan el camino calculado al planificar.
- La ejecución revalida objetivos, visión, ocupación, requisitos y recursos. Una acción que dejó de ser válida se descarta con un aviso al propietario y se intenta la siguiente. Una orden inválida recibida no borra el plan válido existente.
- La construcción encolada no reserva ni consume recursos. Cobra una sola vez al activarse; puede omitirse si faltan recursos o requisitos entonces. Gather de gas requiere un extractor operativo al encolarse.
- Move y Attack Move terminan al llegar; Attack finaliza su foco cuando muere/desaparece el objetivo o acaba la búsqueda de hasta tres segundos sin visión; Repair cuando completa la reparación; Gather tras entregar una carga. Al terminar Attack, una orden pendiente tiene prioridad. Sin pendientes, una unidad lejana continúa en Attack Move hacia la última zona conocida, conservando la secuencia original y buscando objetivos al avanzar. Build espera la finalización del edificio en ambas facciones y mantiene al trabajador asignado durante toda la obra. Sin órdenes posteriores se conservan los comportamientos económicos previos.
- Stop y Hold son inmediatos y vacían pendientes. Stop conserva frenado; Hold ancla. No hay persistencia de partidas ni edición individual de estas acciones en esta entrega.

## Presentación y privacidad

`PlayerView` copia secuencia activa, destino y órdenes pendientes únicamente para entidades propias. Los enemigos visibles o recordados no exponen sus planes. La presentación resuelve entidades y recursos desde esa vista; no consulta posiciones enemigas ocultas.

`UNIT ORDER PLAN` muestra el total pendiente y los próximos tres pedidos de una unidad representativa, indicando si hay más. Con grupos se agregan los conteos por unidad. El mapa dibuja hasta ocho destinos, incluida la acción actual, para el menor ID seleccionado que tenga pendientes. Cada destino tiene un punto y un número, con enlaces punteados desde la unidad y entre los siguientes objetivos. El color de cada punto y del enlace que llega a él indica su acción: cian para Move, rojo para Attack/Attack Move y verde para Gather/Build/Repair. Los puntos, números y conexiones siguen terreno y rampas, se recortan al tablero y no tienen colliders. Las líneas indican la secuencia de objetivos, no un camino A* garantizado.

Se limpia al deseleccionar, morir, detenerse, salir de partida o esconder el tablero. Las órdenes futuras no giran anticipadamente el modelo: el evento de orientación inmediata se emite al cambiar la secuencia activa, tanto al recibir una orden como al avanzar la cola en simulación.

## Comprobaciones

`CommandQueueChecks` cubre la simulación con ambas facciones. `QueuedInputChecks` comprueba los gestos y el enrutamiento con entradas sintéticas; `OrderQueuePreview` verifica el HUD y los destinos, y produce imágenes para inspección. Resultados concretos y límites: [registro de validación](validation.md).
