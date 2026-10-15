# Guía de juego

## Empezar una partida

Elegir facción, rival, dificultad y mapa. En Quest, usar **Place board** y confirmar con el gatillo. Si la habitación no está registrada o se deniega el permiso, usar **Table** o **Floor** y ajustar el plano manualmente. Durante la colocación, el stick secundario modifica la altura y el principal modifica tamaño y orientación.

Luego iniciar la partida. Los seis trabajadores iniciales comienzan a recolectar. Seleccionar uno permite construir; seleccionar un edificio permite producir o investigar. Un extractor debe cubrir un depósito violeta. Se gana destruyendo todos los edificios enemigos.

### Recolección y ocupación de depósitos

El reparto automático usa un objetivo de **2 trabajadores por depósito de mineral** y **3 por depósito de gas/catalizador con extractor terminado**. Al dar una orden a un grupo, cada trabajador reserva su puesto antes de asignar el siguiente. Si el depósito indicado ya está al nivel óptimo, buscan otro cercano del mismo recurso, explorado y accesible. Los trabajadores que viajan o entregan carga conservan su puesto.

Si todos los depósitos cercanos están al nivel óptimo, los trabajadores adicionales siguen recolectando y el indicador muestra **excess**; estos valores son objetivos de reparto, no un límite rígido de asignación ni un cambio de la velocidad de extracción. Al terminar una entrega, los excedentes aprovechan los puestos que se hayan liberado. Los depósitos agotados y los extractores destruidos provocan una nueva búsqueda; se conserva el tipo de cualquier carga pendiente de entrega.

Apuntar o seleccionar un depósito muestra **Workers asignados/óptimo** y la cantidad restante en el panel. Seleccionar el centro principal muestra los totales de minerales y gas de su zona; seleccionar un extractor muestra su ocupación de gas. Se puede dar la orden de gas sobre toda la superficie del extractor. Los indicadores cuentan tus trabajadores, no los del rival.

### Movimiento e interacción

Las unidades se distribuyen al llegar a un destino, rodean el tráfico y ceden espacio a aliados que necesitan pasar. Los empujes conservan la orden anterior; **Hold Position** impide que otros aliados desplacen esa unidad. Las construcciones y la producción respetan el tamaño de los cuerpos.

El menú comienza elevado detrás del tablero. Para moverlo, apuntar a **MOVE MENU**, mantener el gatillo y arrastrar; mientras se sostiene, el stick principal acerca o aleja el panel. Al soltar, queda fijo respecto del tablero y guarda esa posición para la próxima apertura. El panel gira para poder leerse al caminar alrededor. **Reset position** lo devuelve arriba del mapa; pulsar el stick secundario también lo recupera si quedó fuera de vista (tecla **M** en escritorio).

En Quest se solicita el modo de [realidad mixta sin límite contextual de Meta](https://developers.meta.com/vr/documentation/unity/unity-boundaryless/) mientras el passthrough está activo. Permite caminar alrededor del tablero sin que el círculo estacionario interrumpa la experiencia cuando el sistema lo admite. El mapa permanece unido a su ancla espacial. La confirmación final depende de Horizon OS y debe hacerse con el APK instalado directamente en el visor.

| Acción | Mandos Quest | Prueba de escritorio |
|---|---|---|
| Seleccionar / seleccionar área | Gatillo principal / arrastrar | Clic izquierdo / arrastrar |
| Agregar a selección | Gatillo secundario | Shift |
| Orden contextual | A, o X en modo zurdo | Clic derecho |
| Cancelar / opciones | B, o Y en modo zurdo | Escape |
| Desplazar mapa | Stick secundario | WASD |
| Zoom | Stick principal vertical | Rueda |
| Ver mapa completo | Pulsar stick principal | F |
| Mover menú | Mantener gatillo sobre MOVE MENU; stick principal para distancia | Arrastrar MOVE MENU; rueda para distancia |
| Recuperar menú arriba del tablero | Pulsar stick secundario | M |
| Pausa | Menú o panel | Espacio |
| Grupo de control | Botón del panel; mantener para guardar | 1–3; Ctrl+1–3 para guardar |

El juego incluye selección ampliada, contorno de selección al ras del terreno, previsualización de construcción, panel contextual, avisos, sonidos de confirmación y vibración. El rectángulo sigue el suelo y las rampas sin flotar ni dibujar paredes en los desniveles. Los controles de la mano principal se invierten desde el panel.

Al apuntar a una unidad o edificio, el modelo se ilumina y aparece un contorno sobre el suelo: circular para unidades y cuadrado para edificios, amarillo en aliados y rojo en enemigos. Se muestra antes de seleccionar y se apaga al retirar el puntero, apuntar al menú o perder el foco. El disco de selección permanece en los objetos seleccionados. El contorno de hover sigue el terreno y las rampas, respeta el borde del tablero y no aparece sobre recuerdos enemigos bajo niebla de guerra.

El juego se presenta íntegramente en inglés; la incorporación de otros idiomas queda pendiente para una etapa posterior.

## Contenido

- **The Consortium**: unidades resistentes, construcción atendida por trabajadores y reparación con recursos.
- **Symbiosis**: unidades más móviles, edificios que crecen y regeneración fuera del combate.
- Cada facción: trabajador, explorador, combate cercano, tirador y asedio; seis edificios; dos mejoras.
- Hasta 50 unidades móviles y 20 edificios por jugador.
- **The Rift**, mapa simétrico de 128 × 128, con dos bases, cuatro expansiones, alturas y rampas.
- **Training Grounds**, mapa de 48 × 48 con instrucciones contextuales.
- IA fácil y normal; admite los cuatro cruces de facciones. No usa un modelo de lenguaje ni acceso a internet.

Las unidades tienen siluetas y símbolos de rol compartidos por ambas facciones: **herramienta amarilla** para trabajadores, **flechas verdes** para exploradores, **escudo crema** para combate cercano, **mira blanca** para tiradores y **tres proyectiles violetas** para artillería. Los trabajadores llevan carga y pinzas; la infantería cercana lleva escudo, los tiradores un arma larga y la artillería dos cañones o espinas. El casco y el borde del símbolo conservan el azul aliado y el rojo enemigo.

El panel muestra el rol junto al nombre al apuntar sin selección, seleccionar o producir. Las selecciones mixtas muestran cantidades por rol. Las barras de vida aparecen al seleccionar, apuntar o recibir daño, para despejar la vista del ejército.
