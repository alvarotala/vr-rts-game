# Guía de juego

## Empezar una partida

Al abrir el juego aparece el menú principal con **New Match**, **Settings** y **Controls**. El tablero permanece oculto hasta entrar a la preparación de partida o iniciar su colocación.

Desde **New Match**, elegir facción, rival, dificultad y mapa. En Quest, usar **Place board** y confirmar con el gatillo. Si la habitación no está registrada o se deniega el permiso, usar **Table** o **Floor** y ajustar el plano manualmente. Durante la colocación, el joystick derecho modifica tamaño y orientación; sostener el grip derecho permite ajustar altura con ese mismo joystick.

Usar **START MATCH** cuando el tablero esté confirmado. Al comenzar se muestra el panel de acciones de unidades y edificios. Los seis trabajadores iniciales comienzan a recolectar. Seleccionar uno permite construir; seleccionar un edificio permite producir o investigar. Un extractor debe cubrir un depósito violeta. Se gana destruyendo todos los edificios enemigos.

### Dificultad de la IA

El botón **AI** permite elegir **Very Easy**, **Easy** o **Normal** antes de iniciar. La elección se conserva al volver al menú para otra partida y al jugar revancha; no se guarda entre aperturas de la aplicación.

**Very Easy** está pensado para aprender los controles y la economía. Reacciona cada seis segundos, mantiene como máximo ocho trabajadores y seis soldados básicos (incluidas sus colas), construye una sola barraca y recluta soldados cada 24 segundos como mínimo. Omite exploradores, artillería, mejoras, torres y expansiones. Mantiene su ejército cerca de la base y envía oleadas de tres soldados desde el minuto 18, con al menos tres minutos entre oleadas. Si atacás su base antes, se defiende. No modifica tus límites ni otorga recursos gratis a ninguno de los jugadores.

**Easy** y **Normal** conservan sus comportamientos anteriores: economía mayor, exploración, tecnología y expansiones. Normal reacciona más rápido y utiliza más artillería y mejoras.

### Configuración, pausa y regreso al inicio

**Settings** permite recolocar el tablero, elegir mesa o piso y detectar superficies. **Controls** muestra una guía completa adaptada al mando Touch derecho o a mouse y teclado. El mando izquierdo queda sin asignación. **Back** vuelve al menú desde el que se abrió esa pantalla.

Dentro de una partida, **Pause**, **B** con la rueda cerrada y sin una acción pendiente, o **Espacio** abren la pausa; **Resume match** continúa. También se puede abrir **Settings** y **Controls** sin que avance la simulación. **Return to main menu → Leave match** termina la partida y devuelve al inicio; **Keep playing** cancela la salida. Al finalizar por victoria o derrota, aparecen **Rematch** y **Main menu**, sin opción de reanudar una partida terminada.

Volver al inicio limpia selección, grupos, órdenes pendientes y estado de la partida, conservando facciones/mapa elegidos y la colocación física del tablero. Las pausas por seguimiento, foco o visor continúan vigentes hasta que el sistema las libere. Mientras hay un menú de partida abierto no se pueden dar órdenes al tablero.

**B/Y** o **Escape** cancelan la acción actual, retroceden en los menús o abren la pausa. Durante la colocación, cancelan el ajuste y recuperan la posición previa del tablero. Si no había una posición confirmada, sigue siendo necesario colocar el tablero para empezar.

### Recolección y ocupación de depósitos

El reparto automático usa un objetivo de **2 trabajadores por depósito de mineral** y **3 por depósito de gas/catalizador con extractor terminado**. Al dar una orden a un grupo, cada trabajador reserva su puesto antes de asignar el siguiente. Si el depósito indicado ya está al nivel óptimo, buscan otro cercano del mismo recurso, explorado y accesible. Los trabajadores que viajan o entregan carga conservan su puesto.

Los minerales se reconocen por grupos de cristales azules facetados de distintas alturas sobre una base rocosa. El gas aparece como una abertura irregular con interior violeta y vapor ascendente; el extractor se construye encima. El vapor natural desaparece mientras el extractor ocupa el depósito y se detiene al pausar la partida.

Si todos los depósitos cercanos están al nivel óptimo, los trabajadores adicionales siguen recolectando y el indicador muestra **excess**; estos valores son objetivos de reparto, no un límite rígido de asignación ni un cambio de la velocidad de extracción. Al terminar una entrega, los excedentes aprovechan los puestos que se hayan liberado. Los depósitos agotados y los extractores destruidos provocan una nueva búsqueda; se conserva el tipo de cualquier carga pendiente de entrega.

Apuntar o seleccionar un depósito muestra **Workers asignados/óptimo** y la cantidad restante en el panel. Seleccionar el centro principal muestra los totales de minerales y gas de su zona; seleccionar un extractor muestra su ocupación de gas. Se puede dar la orden de gas sobre toda la superficie del extractor. Los indicadores cuentan tus trabajadores, no los del rival.

Sobre cada centro principal terminado aparece **MINERAL WORKERS** con el total asignado a sus minerales: por ejemplo, **5/8** para cinco trabajadores y cuatro depósitos activos. Sobre cada extractor terminado aparece **GAS WORKERS**, por ejemplo **3/3**. No hace falta seleccionar el edificio. Al superar el óptimo, el número se vuelve rojo (**9/8**, **4/3**); vuelve a blanco al retirar el exceso. Los trabajadores en viaje o entregando carga siguen incluidos y la capacidad baja cuando se agota un depósito. Las etiquetas miran al jugador, conservan su tamaño físico en tableros pequeños y se separan si se superponen.

### Panel de partida

La franja superior separa **MINERALS**, **GAS** y **SUPPLY / CAPACITY**, con cantidades grandes y colores propios. Gas corresponde al catalizador del catálogo. La capacidad muestra usados/total y lugares disponibles; **FULL** aparece en naranja cuando está completa. Los costos de las acciones usan **M** para minerales y **G** para gas.

La ficha de selección muestra el nombre de la unidad o edificio, su rol, facción, vida actual/máxima y orden o estado. Las construcciones y colas muestran progreso; seleccionar varias entidades muestra su cantidad y composición por nombre. Sin selección, apuntar a una entidad visible muestra su ficha. Los depósitos seleccionados muestran el tipo y la cantidad restante conocida.

**ACTIONS** contiene únicamente las órdenes de la selección: construcción para trabajadores, órdenes militares para unidades y producción o investigación para edificios compatibles. **Cancel / refund** aparece mientras hay una construcción o cola que cancelar. Los edificios automáticos y las selecciones incompatibles muestran una explicación.

Las acciones se agrupan con encabezados y colores: **BUILD STRUCTURES** en ámbar, **TRAIN UNITS** en verde, **RESEARCH** en violeta, **UNIT ORDERS** en azul y **CANCEL / REFUND** en coral. Construcción y órdenes de unidades ocupan filas separadas.

Mantener el cursor sobre una acción durante **1,5 segundos** muestra su **FIELD GUIDE** en la tarjeta lateral: nombre, función del edificio o unidad, costo, tiempo y requisitos cuando corresponda. También explica órdenes, mejoras y cancelaciones. La ayuda reemplaza temporalmente la ficha de selección, sin tapar botones, recursos ni producción; desaparece al cambiar de acción, retirar el cursor, pulsar, cambiar la selección o salir del panel. Todo el texto del juego permanece en inglés.

En los menús de Quest, **A funciona como el gatillo**: pulsar y soltar sobre un botón lo activa; soltar afuera cancela. Mantener A permite guardar grupos y arrastrar el menú. Si se usan ambos botones juntos, solo se ejecuta una acción. Las pulsaciones iniciadas en el menú se consumen allí, incluso si el cursor termina sobre el mapa.

Al seleccionar un edificio productor terminado, **PRODUCTION QUEUE** muestra una tarjeta grande para la unidad o investigación activa, su barra de progreso, porcentaje y segundos restantes. A su derecha aparecen los cuatro lugares siguientes, numerados en orden de salida y con nombre/rol. El contador incluye el elemento activo; **5 / 5 · FULL** indica que no entran más órdenes. **Queue empty** permanece visible antes de producir para que los botones no cambien de posición al agregar unidades. **Cancel / refund** elimina el último elemento y devuelve su costo.

Para cancelar un pedido concreto, apuntar directamente a su tarjeta en **PRODUCTION QUEUE**: se vuelve **roja** y muestra **Cancel + refund**. Un clic con **A**, **gatillo** o **mouse izquierdo** cancela solo ese elemento, **sin confirmación**, devuelve su costo completo y libera su reserva de suministro. También se puede cancelar la producción activa, una investigación o una unidad lista que espera salida. Los demás pedidos conservan su orden; si se cancela el activo, empieza el siguiente. Los lugares vacíos no se pueden cancelar. Si la cola avanza durante la pulsación, nunca se aplica la cancelación a una unidad distinta.

Una producción terminada que aún no puede salir muestra **WAITING TO DEPLOY**, con **Needs more supply** si faltan lugares para las unidades existentes o **Clear the building exit** si la salida está bloqueada. Las pausas por seguimiento/visor muestran **PAUSED** y conservan el progreso. La sección también distingue investigaciones de unidades y se oculta al seleccionar unidades, varios edificios, construcciones incompletas o edificios sin producción.

**CONTROL GROUPS** tiene su propia franja: cada grupo muestra **Empty** o su cantidad asignada. Pulsar recupera el grupo; mantener guarda la selección. **MATCH** contiene el reloj y **Pause**, separados de las acciones de unidades y edificios. **Full map** aparece únicamente en el modo de prueba de escritorio. La ocupación de depósitos y el aviso **excess** permanecen en otra franja, sin reemplazar los recursos globales.

### Movimiento e interacción

Las unidades se distribuyen al llegar a un destino, rodean el tráfico y ceden espacio a aliados que necesitan pasar. Los empujes conservan la orden anterior; **Hold Position** impide que otros aliados desplacen esa unidad. Las construcciones y la producción respetan el tamaño de los cuerpos.

Las unidades pesadas tienen prioridad frente a las livianas. MOVE responde desde el siguiente paso de simulación y empieza a orientar el modelo inmediatamente, conservando inercia: cada tipo acelera, frena y cambia de dirección a su ritmo. **Stop** frena; **Hold Position** ancla la unidad. Una nueva orden reemplaza cualquier maniobra automática para ceder el paso.

En Quest, el tablero queda fijo al confirmar su colocación: los joysticks ya no desplazan ni amplían el mapa, y pulsar el stick principal tampoco cambia su vista. Para cambiar posición, tamaño u orientación, abrir **Settings → Reposition board** y confirmar una nueva colocación. Durante ese ajuste, el joystick derecho modifica tamaño/giro; sostener el grip derecho cambia el eje vertical a altura, sin alterar tamaño ni giro. El modo de prueba de escritorio conserva sus controles de navegación para desarrollo.

El menú comienza elevado detrás del tablero. Para moverlo, apuntar a **MOVE MENU**, mantener el gatillo o A y arrastrar; mientras se sostiene, el stick principal acerca o aleja el panel. Al soltar, queda fijo respecto del tablero y guarda esa posición para la próxima apertura. El panel gira para poder leerse al caminar alrededor. **Reset position** lo devuelve arriba del mapa; la tecla **M** también lo recupera en escritorio. La pulsación del joystick derecho queda reservada para la rueda, incluso con grip sostenido.

En Quest se solicita el modo de [realidad mixta sin límite contextual de Meta](https://developers.meta.com/vr/documentation/unity/unity-boundaryless/) mientras el passthrough está activo. Permite caminar alrededor del tablero sin que el círculo estacionario interrumpa la experiencia cuando el sistema lo admite. El mapa permanece unido a su ancla espacial. La confirmación final depende de Horizon OS y debe hacerse con el APK instalado directamente en el visor.

| Acción | Mandos Quest | Prueba de escritorio |
|---|---|---|
| Seleccionar / seleccionar área | Gatillo principal / arrastrar | Clic izquierdo / arrastrar |
| Agregar a selección | Mantener grip derecho (botón lateral) | Mantener Shift |
| Seleccionar unidades cercanas del mismo tipo | Doble gatillo sobre una unidad propia | Doble clic izquierdo |
| Quitar la unidad apuntada | Grip + mantener gatillo quieto durante 0,5 s | Shift + mantener clic izquierdo durante 0,5 s |
| Activar botón del menú | Gatillo derecho o A; mantener para acciones largas | Clic izquierdo |
| Ayuda de acciones | Hover durante 1,5 segundos | Hover durante 1,5 segundos |
| Orden contextual | A sobre el mapa (con la rueda cerrada) | Clic derecho |
| Encolar orden contextual | Grip + A | Shift + clic derecho |
| Punto de reunión del edificio seleccionado | A sobre el terreno | Clic derecho |
| Attack Move / ataque directo sobre enemigo | Doble A; mantener grip para encolar | Doble clic derecho; Shift para encolar |
| Abrir / cerrar rueda de acciones | Pulsar joystick derecho; doble grip también abre | — |
| Elegir acción de la rueda | Inclinar joystick derecho | — |
| Ejecutar en la rueda | Gatillo derecho o A | — |
| Volver / cerrar la rueda | Grip derecho o B | — |
| Cancelar / opciones | B | Escape |
| Desplazar mapa (solo escritorio) | — | WASD |
| Zoom (solo escritorio) | — | Rueda |
| Ver mapa completo (solo escritorio) | — | F |
| Mover menú | Mantener gatillo o A sobre MOVE MENU; stick principal para distancia | Arrastrar MOVE MENU; rueda para distancia |
| Recuperar menú arriba del tablero | Reset position en el panel | M |
| Pausa | B o panel | Espacio |
| Grupo de control | Botón del panel; mantener para guardar | 1–3; Ctrl+1–3 para guardar |

El juego incluye selección ampliada, contorno de selección al ras del terreno, previsualización de construcción, panel contextual, avisos, sonidos de confirmación y vibración. El rectángulo sigue el suelo y las rampas sin flotar ni dibujar paredes en los desniveles. Todos los controles del juego usan el mando derecho; el izquierdo queda sin asignación por ahora. Se retiró la opción de mano dominante y no se aplica la preferencia antigua guardada.

El **grip derecho** funciona como Shift: mantenerlo mientras se selecciona con el gatillo principal suma unidades a la selección actual, también al arrastrar un área. Al pulsar una unidad ya seleccionada con el grip sostenido, se quita de la selección. Sin el grip, una nueva selección reemplaza la anterior. El modificador está en el mando derecho, junto con el gatillo de selección.

Mantener **grip + gatillo durante 0,5 segundos** sobre una unidad ya seleccionada la quita una sola vez; soltar no vuelve a seleccionarla. Si el puntero se arrastra, el gesto sigue siendo selección de área. **Doble gatillo** selecciona unidades propias de la misma definición dentro de doce celdas de la unidad apuntada; con grip conserva el resto de la selección. No incluye edificios ni unidades enemigas.

### Punto de reunión de edificios

Seleccionar un centro principal, barraca o edificio de tecnología terminado y pulsar **A sobre el terreno** establece su **Rally Point** (clic derecho en escritorio). También funciona con varios productores propios seleccionados. Cada edificio conserva un único destino; volver a pulsar A lo cambia, incluso con grip o doble A. El indicador cian con bandera y línea punteada conecta el edificio con ese punto, y la consola muestra el destino en **RALLY POINT**.

Las unidades nuevas reciben una orden Move al desplegarse, usando el destino vigente en ese momento y las reglas normales de separación y movimiento. Cambiar el punto no altera unidades que ya salieron ni la cola de producción. Los trabajadores también van al punto elegido; este ajuste es un destino de movimiento, no una orden automática de recolección.

**Clear Rally Point**, disponible en la consola y la rueda, borra el destino sin cancelar producción. Así se recupera el comportamiento predeterminado: los trabajadores buscan recursos y las unidades militares quedan junto a la salida. Los destinos fuera del mapa, bloqueados o sin camino según la información conocida se rechazan conservando el punto anterior. Si el terreno cambia después y el punto queda inaccesible, la unidad sale en un espacio seguro; se muestra un aviso cuando el bloqueo también es conocido. Si la propia salida está ocupada, la producción espera como antes.

### Cola de acciones de unidades

**Grip + A** agrega una orden al final del plan de cada unidad seleccionada. Si está quieta, empieza de inmediato; si está ocupada, conserva su trabajo actual. Repetir A en otro lugar agrega otro destino. Una orden válida sin grip reemplaza el plan; **Stop** y **Hold Position** lo limpian inmediatamente.

**Doble A** hace Attack Move sobre terreno o ataque directo sobre un enemigo visible. Con grip también se encola. El primer A responde inmediatamente; el segundo convierte esa misma orden en ataque, sin dejar un Move extra. Las pulsaciones deben estar separadas por un máximo de 0,35 segundos y apuntar al mismo objetivo o a un punto cercano (hasta 1,5 celdas). Dos ubicaciones diferentes siguen siendo dos órdenes. Cambiar selección, soltar grip, entrar al menú/rueda o pausar descarta el doble clic pendiente.

La sección **UNIT ORDER PLAN** distingue estas acciones de la producción de edificios. Muestra cuántas quedan y los próximos pasos. En el mapa aparecen hasta ocho destinos con puntos y números, unidos por líneas punteadas al ras del terreno y las rampas. El número 1 corresponde a la acción actual. Los puntos y sus enlaces usan **cian para Move**, **rojo para Attack y Attack Move** y **verde para trabajo** (recolección, construcción y reparación). Para grupos se muestra el plan de una unidad representativa identificada en el panel. Las líneas conectan objetivos en el orden de ejecución; la ruta real se adapta a obstáculos y unidades.

Cada unidad admite hasta **32 órdenes pendientes**. Move/Attack Move avanzan al llegar; Attack al desaparecer el objetivo o perderlo de vista; Gather después de entregar una carga; Repair al terminar la reparación. La construcción industrial espera a terminar el edificio; la biológica avanza después de iniciar el crecimiento. Las órdenes se revalidan al comenzar y se omiten con aviso si ya no se pueden ejecutar. Las construcciones encoladas se pagan al activarse, no al agregarlas. Detalles técnicos y pruebas: [colas de acciones](order-queues.md).

La **rueda contextual** aparece junto al mando derecho al pulsar su joystick o hacer **doble pulsación rápida del grip derecho**. Inclinar el joystick elige una opción y **el gatillo derecho o A la ejecutan**; **el grip derecho o B vuelven del submenú Build o cierran la rueda principal**. También puede cerrarse pulsando nuevamente el joystick. Al abrir o cambiar de submenú, centrar el joystick antes de elegir; luego puede soltarse, manteniendo resaltada la opción hasta confirmar o cancelar. La rueda muestra el nombre de la selección, la acción y su costo o efecto. Los trabajadores abren seis edificios desde **Build**; las unidades ofrecen Attack Move, Stop y Hold Position; los edificios ofrecen producción, investigación y cancelación según corresponda. Las órdenes rechazadas muestran el motivo en el centro de la rueda.

Mientras está abierta, sus pulsaciones se consumen en la rueda. Mantener gatillo o grip no repite acciones; el grip que acaba de abrirla no la cierra. Al elegir una construcción o Attack Move, la rueda se cierra y espera un destino nuevo en el mapa, después de soltar los botones. Fuera de la rueda, el grip sostenido conserva la selección aditiva; usar el gatillo durante ese gesto interrumpe el doble grip para evitar aperturas accidentales. Cambiar la selección, perder una unidad seleccionada, pausar, perder foco/seguimiento o terminar la partida cierra la rueda. La consola permanece disponible para recursos, colas y las mismas acciones.

La selección por área puede **empezar o terminar fuera del tablero**: mantener el gatillo (o clic izquierdo), arrastrar sobre las unidades y soltar, incluso fuera del borde opuesto. El rectángulo y la selección se recortan a la parte visible del mapa; con el zoom de escritorio no se seleccionan unidades recortadas fuera del tablero. Se conserva el modificador para agregar unidades. Un clic o arrastre completamente externo no cambia la selección; apuntar al menú o perder una proyección válida cancela el arrastre. Las órdenes contextuales y la confirmación de construcción siguen requiriendo apuntar al terreno.

Los **minerales** se pueden apuntar directamente sobre el cristal, incluso mirando el tablero de costado, o cerca de su base gracias a una zona de interacción más amplia. El depósito bajo el cursor se ilumina y recibe un aro sobre el terreno antes de pulsar. El aro y el panel usan el mismo objetivo que recibirá la orden de recolección. Los trabajadores cercanos no ocultan el mineral con su margen de selección; apuntar directamente al cuerpo del trabajador sigue seleccionándolo. Una pulsación con un pequeño movimiento de la mano conserva el depósito elegido al presionar; arrastrar más lejos mantiene la selección por área. La asignación automática a depósitos disponibles sigue funcionando igual.

Al apuntar al mapa aparece un **punto blanco con líneas de mira turquesa** en la intersección del láser con el terreno. Está al ras del suelo y las rampas, mantiene su tamaño con el zoom de escritorio y se recorta en el borde del tablero. También funciona con el mouse. Sigue visible sobre niebla como referencia del cursor, sin revelar información; desaparece al salir del tablero, apuntar al panel o pausar.

Al apuntar a una unidad o edificio, el modelo se ilumina y aparece un contorno sobre el suelo: circular para unidades y cuadrado para edificios, amarillo en aliados y rojo en enemigos. Se muestra antes de seleccionar y se apaga al retirar el puntero, apuntar al menú o perder el foco. El disco de selección permanece en los objetos seleccionados. El contorno de hover sigue el terreno y las rampas, respeta el borde del tablero y no aparece sobre recuerdos enemigos bajo niebla de guerra.

Al **recibir daño**, unidades y edificios visibles muestran un destello claro en el modelo durante unos 0,2 segundos y un aro naranja que se expande sobre ellos durante unos 0,6 segundos. La barra de vida se vuelve naranja durante ese intervalo. Varios impactos renuevan la señal sin acumular aros; también se indica el daño de área. El efecto no necesita selección y es independiente del destello del atacante. Los colores de equipo y rol vuelven al finalizar el pulso; los recuerdos enemigos bajo niebla no muestran impactos.

Al **disparar**, las unidades se orientan hacia el objetivo real y aparece un destello en el arma, un proyectil con estela y chispas al llegar. Las armas industriales usan trazadoras doradas; las biológicas, proyectiles verdes más gruesos; la artillería dibuja un arco elevado y un impacto más amplio. Trabajadores y combatientes cercanos muestran un golpe corto. Las torres también muestran sus disparos. Una estela tenue conserva brevemente la conexión entre atacante y destino, incluso si el golpe destruyó al objetivo. Son efectos visuales: no cambian daño, cadencia ni tiempo de resolución del combate. Se recortan al tablero y a la visión actual, y se detienen durante la pausa.

El juego se presenta íntegramente en inglés; la incorporación de otros idiomas queda pendiente para una etapa posterior.

## Contenido

- **The Consortium**: unidades resistentes, construcción atendida por trabajadores y reparación con recursos.
- **Symbiosis**: unidades más móviles, edificios que crecen y regeneración fuera del combate.
- Cada facción: trabajador, explorador, combate cercano, tirador y asedio; seis edificios; dos mejoras.
- Hasta 50 unidades móviles y 20 edificios por jugador.
- **The Rift**, mapa simétrico de 128 × 128, con dos bases, cuatro expansiones, alturas y rampas.
- **Training Grounds**, mapa de 48 × 48 con instrucciones contextuales.
- IA **Very Easy**, **Easy** y **Normal**; admite los cuatro cruces de facciones. No usa un modelo de lenguaje ni acceso a internet.

Las unidades tienen siluetas y símbolos de rol compartidos por ambas facciones: **herramienta amarilla** para trabajadores, **flechas verdes** para exploradores, **escudo crema** para combate cercano, **mira blanca** para tiradores y **tres proyectiles violetas** para artillería. Los trabajadores llevan carga y pinzas; la infantería cercana lleva escudo, los tiradores un arma larga y la artillería dos cañones o espinas. El casco y el borde del símbolo conservan el azul aliado y el rojo enemigo.

El panel muestra el rol junto al nombre al apuntar sin selección, seleccionar o producir. Las selecciones mixtas muestran cantidades por nombre de unidad o edificio. Las barras de vida sobre las unidades aparecen al seleccionar, apuntar o recibir daño, para despejar la vista del ejército.

Los edificios también se identifican por **silueta, altura y símbolo**, además del nombre al apuntar o seleccionar. El casco conserva el azul aliado o rojo enemigo; los acentos indican el rol y son compartidos entre facciones:

| Rol | Industrial / biológico | Rasgo reconocible | Símbolo |
|---|---|---|---|
| Base central | Command Core / Heart | Ciudadela escalonada o núcleo con corona de órganos | Corona dorada |
| Barraca | Barracks / Brood Nest | Hangar bajo con entrada ancha o nido con tres huevos | Dos chevrones naranjas |
| Gas | Refinery / Siphon | Dos chimeneas o conductos abiertos | Gota violeta |
| Suministro | Relay / Node | Depósitos bajos o sacos conectados | Signo más verde |
| Tecnología | Arsenal / Chrysalis | Reactor vertical o capullo rodeado de anillos | Rombo celeste |
| Defensa | Sentinel / Stinger | Torre estrecha con cañón o aguijón | Mira clara |

Los símbolos forman parte del modelo y se leen desde arriba, sin añadir carteles flotantes a la partida. Las torres se orientan hacia donde disparan. Los recuerdos bajo niebla conservan la silueta y el símbolo en tonos apagados, sin colores activos ni señales de impacto.
