# Guía de juego

Los controles indicados a continuación son los predeterminados y se pueden personalizar desde **Hotkeys**. **Settings → Hand: Left/Right** elige un único mando activo (derecho inicialmente); con el izquierdo, X/Y reemplazan A/B y el puntero, gatillo, grip y stick pasan a esa mano. La experiencia de juego está orientada a Quest 3. La rueda unificada se usa únicamente en VR. Su navegación es fija: joystick para elegir y gatillo para confirmar. La versión desktop queda por ahora para desarrollo y prueba de mecánicas: mouse y teclado, acciones desde el panel, teclas **1–4** para recuperar grupos y **Ctrl+1–4** para guardarlos. Comparte las reglas del juego y conserva WASD, rueda del mouse y F para navegar cómodamente durante las pruebas.

## Empezar una partida

Al abrir el juego aparece el menú principal con **New Match**, **Settings** y **Hotkeys**. El tablero permanece oculto hasta entrar a la preparación de partida o iniciar su colocación.

Desde **New Match**, elegir facción, rival, dificultad y mapa. En Quest, usar **Place board** y confirmar con el gatillo. Si la habitación no está registrada o se deniega el permiso, usar **Table** o **Floor** y ajustar el plano manualmente. Durante la colocación, el joystick derecho modifica tamaño y orientación; sostener el grip derecho permite ajustar altura con ese mismo joystick.

Usar **START MATCH** cuando el tablero esté confirmado. Al comenzar se muestra el panel de acciones de unidades y edificios. Los seis trabajadores iniciales comienzan a recolectar. Seleccionar uno permite construir; seleccionar un edificio permite producir o investigar. Un extractor debe cubrir un depósito violeta. Se gana destruyendo todos los edificios enemigos.

### Dificultad de la IA

El botón **AI** permite elegir **Easy**, **Normal** o **Hard** antes de iniciar. La elección se conserva al volver al menú para otra partida y al jugar revancha; no se guarda entre aperturas de la aplicación.

Todas las dificultades usan **el catálogo completo** de su facción: trabajadores, exploradores, infantería cuerpo a cuerpo, infantería a distancia, artillería y casters; también construyen tecnología, academias, defensas e investigan ataque y resistencia. Los casters usan las mismas habilidades, energía, recarga y visión que los del jugador.

| Modalidad | Reacción | Ataque principal desde | Ejército enviado | Pausa de recuperación | Objetivo de trabajadores / ejército |
| --- | --- | --- | --- | --- | --- |
| **Easy** | 3 s | 5 min, con al menos 6 soldados | 65 % disponible, hasta 22 | 65 s | 16 / 28 |
| **Normal** | 2 s | 3 min 30 s, con al menos 9 soldados | 80 % disponible, hasta 32 | 35 s | 20 / 36 |
| **Hard** | 1 s | 2 min 30 s, con al menos 10 soldados | 95 % disponible, hasta 38 | 20 s | 22 / 38 |

Los tres niveles producen más rápido, exploran antes y refuerzan sus ataques. Easy conserva mayor tiempo de preparación; Normal moviliza una parte mayor de su ejército; Hard desarrolla antes su economía y tecnología, utiliza hasta cuatro barracas y mantiene la presión con casi todas las tropas disponibles. Si su economía necesita menos trabajadores, emplea esos espacios en más soldados, respetando siempre el límite de 60 unidades. Estos tiempos indican cuándo puede comenzar la ofensiva principal; necesita reunir el mínimo de unidades. Los exploradores pueden adelantarse y la defensa de la base funciona desde el inicio.

La producción aprovecha varios edificios a la vez y sigue entrenando unidades disponibles mientras desbloquea artillería y casters. La economía considera las asignaciones de recolección, las colas de producción y el presupuesto restante de cada decisión. La IA usa únicamente su visión, edificios recordados y datos públicos del mapa; puede defender sus bases, retirarse y reagruparse antes de volver a atacar.

En The Rift busca y ocupa depósitos de otras zonas: Easy y Normal planean hasta dos bases y Hard hasta tres. Reserva recursos para construirlas y traslada trabajadores a las bases terminadas. Training Ground solo tiene los dos conjuntos de recursos iniciales, por lo que no ofrece una expansión neutral. La recolección automática del jugador sigue limitada a los minerales de su base; los traslados de la IA son órdenes deliberadas.

**Todas las partidas empiezan con las mismas condiciones para ambos jugadores:** 400 minerales, cero gas, una base terminada, seis trabajadores, capacidad inicial de diez y ninguna mejora. La dificultad no cambia ingresos, costos, daño, salud ni los límites de las reglas. En ambos mapas, las posiciones de los trabajadores y sus depósitos iniciales también están reflejadas, para igualar la distancia de salida. Ambas facciones tienen las mismas reglas y estadísticas por rol; los objetivos de población de la tabla son decisiones de la IA, no límites impuestos al jugador.

### Configuración, pausa y regreso al inicio

**Settings** permite recolocar el tablero, elegir mesa o piso y detectar superficies. **Hotkeys** está disponible desde el menú principal, la preparación de partida y la pausa. Abre **Edit bindings** únicamente para el dispositivo activo: el mando Touch elegido en Quest o mouse/teclado en desktop. Elegí **Change**, soltá los controles y pulsá la nueva tecla o botón. Se guarda automáticamente en este dispositivo y se conserva al reiniciar el juego. Si la entrada ya está asignada, **Swap bindings** intercambia ambas funciones después de confirmar.

**Cancel change**, Escape durante la captura o esperar 12 segundos cancelan el cambio. **Restore device defaults** pide confirmación y restaura solamente el dispositivo seleccionado. Los modificadores de teclado aceptan Shift, Ctrl o Alt, de cualquiera de los dos lados. Los gestos de arrastrar, mantener y doble pulsación siguen al botón de su función; el joystick y la rueda del mouse conservan sus ejes. El mando que no está elegido queda sin asignación.

La navegación de los menús conserva **gatillo/A y B** con el mando derecho, **gatillo/X y Y** con el izquierdo, y **clic izquierdo y Escape** en desktop, incluso después de editar los controles de juego. Esto permite recuperar la configuración. Durante la captura no se ejecutan órdenes ni se reanuda la partida. **Back** vuelve al menú de origen; si se abrió desde pausa, la partida sigue pausada.

Dentro de una partida, **Pause**, **B** con la rueda cerrada y sin una acción pendiente, o **Espacio** abren la pausa; **Resume match** continúa. También se puede abrir **Settings** y **Hotkeys** sin que avance la simulación. **Return to main menu → Leave match** termina la partida y devuelve al inicio; **Keep playing** cancela la salida. Al finalizar por victoria o derrota, aparecen **Rematch** y **Main menu**, sin opción de reanudar una partida terminada.

La pausa muestra directamente **CONTROL GUIDE**, con seis categorías: **Selection**, **Orders**, **Menus**, **Groups**, **Production** y **Board**. Cada tarjeta explica el comando, el botón/gesto y su efecto. Se incluyen selección múltiple, colas, Attack Move, Stop/Hold, rueda unificada, guardado dentro de Groups, producción, cancelaciones, Rally Points y colocación. Cambiar de categoría con gatillo/A no ejecuta comandos ni reanuda la partida. **Hotkeys** comparte el catálogo de esta guía en sus seis categorías de consulta. Abre mostrando el dispositivo activo y **Back/B** regresa al menú de origen, conservando la pausa. La lista completa para revisión está en [hotkeys predeterminados](hotkeys.md). La guía y las categorías de referencia muestran las asignaciones actuales; **Edit bindings** vuelve al editor.

Volver al inicio limpia selección, grupos, órdenes pendientes y estado de la partida, conservando facciones/mapa elegidos y la colocación física del tablero. Las pausas por seguimiento, foco o visor continúan vigentes hasta que el sistema las libere. Mientras hay un menú de partida abierto no se pueden dar órdenes al tablero.

**B** o **Escape** cancelan la acción actual, retroceden en los menús o abren la pausa. Durante la colocación, cancelan el ajuste y recuperan la posición previa del tablero. Si no había una posición confirmada, sigue siendo necesario colocar el tablero para empezar.

### Recolección y ocupación de depósitos

El reparto automático usa un objetivo de **2 trabajadores por depósito de mineral** y **3 por depósito de gas/catalizador con extractor terminado**. Al dar una orden a un grupo, cada trabajador reserva su puesto antes de asignar el siguiente. Si el depósito indicado ya está al nivel óptimo, buscan otro cercano del mismo recurso, explorado y accesible. Los trabajadores que viajan o entregan carga conservan su puesto.

Los minerales se reconocen por grupos de cristales azules facetados de distintas alturas sobre una base rocosa. El gas aparece como una abertura irregular con interior violeta y vapor ascendente; el extractor se construye encima. El vapor natural desaparece mientras el extractor ocupa el depósito y se detiene al pausar la partida.

Si todos los depósitos cercanos están al nivel óptimo, los trabajadores adicionales siguen recolectando y el indicador muestra **excess**; estos valores son objetivos de reparto, no un límite rígido de asignación ni un cambio de la velocidad de extracción. Al terminar una entrega, los excedentes aprovechan los puestos que se hayan liberado. Los depósitos agotados y los extractores destruidos provocan una nueva búsqueda dentro del mismo grupo local de recursos; se conserva el tipo de cualquier carga pendiente de entrega. Cuando se agotan todos los minerales de esa zona, entregan la última carga y quedan inactivos allí. Ir a los minerales de otra zona requiere una orden explícita, aunque ya estén explorados. Los trabajadores recién producidos en una base agotada también esperan órdenes.

Las entregas usan el centro principal terminado más cercano al trabajador. Una expansión en construcción todavía no recibe recursos. Al quedar libre después de construir en otra zona, el constructor busca minerales locales y deja de priorizar el depósito lejano donde trabajaba antes.

**Ambas facciones requieren un trabajador construyendo durante toda la obra.** Si se retira, recibe otra orden o muere, el progreso se detiene; otro trabajador puede reanudarlo mediante la orden contextual sobre el edificio incompleto. Ninguna estructura se termina sola. Los trabajadores de ambas facciones también reparan unidades y edificios aliados gastando minerales. Ninguna facción regenera vida automáticamente.

Las estructuras en construcción se muestran **en gris**, con **UNDER CONSTRUCTION**, porcentaje y barra de progreso sobre el edificio, sin necesidad de seleccionarlo. Las bases también muestran **NO RESOURCE DELIVERY YET**. Al terminar recuperan sus colores normales y desaparece el aviso; los centros y extractores operativos pasan a mostrar sus contadores de trabajadores. Los indicadores no muestran progreso actualizado de edificios recordados bajo niebla.

Apuntar o seleccionar un depósito muestra **Workers asignados/óptimo** y la cantidad restante en el panel. Seleccionar el centro principal muestra los totales de minerales y gas de su zona; seleccionar un extractor muestra su ocupación de gas. Se puede dar la orden de gas sobre toda la superficie del extractor. Los indicadores cuentan tus trabajadores, no los del rival.

Sobre cada centro principal terminado aparece **MINERAL WORKERS** con el total asignado a sus minerales: por ejemplo, **5/8** para cinco trabajadores y cuatro depósitos activos. Sobre cada extractor terminado aparece **GAS WORKERS**, por ejemplo **3/3**. No hace falta seleccionar el edificio. Al superar el óptimo, el número se vuelve rojo (**9/8**, **4/3**); vuelve a blanco al retirar el exceso. Los trabajadores en viaje o entregando carga siguen incluidos y la capacidad baja cuando se agota un depósito. Las etiquetas miran al jugador, conservan su tamaño físico en tableros pequeños y se separan si se superponen.

### Panel de partida

La franja superior separa **MINERALS**, **GAS** y **SUPPLY / CAPACITY**, con cantidades grandes y colores propios. Gas corresponde al catalizador del catálogo. La capacidad muestra usados/total y lugares disponibles; **FULL** aparece en naranja cuando está completa. Los costos de las acciones usan **M** para minerales y **G** para gas.

La ficha de selección muestra el nombre de la unidad o edificio, su rol, facción, vida actual/máxima y orden o estado. Las construcciones y colas muestran progreso; seleccionar varias entidades muestra su cantidad y composición por nombre. Sin selección, apuntar a una entidad visible muestra su ficha. Los depósitos seleccionados muestran el tipo y la cantidad restante conocida.

**SELECTED ITEMS** muestra una cuadrícula de iconos cuadrados con la silueta real, el nombre y la vida de cada unidad o edificio seleccionado. Hay hasta 20 casillas por página y flechas para recorrer el resto. **Trigger / A** sobre una casilla selecciona solo ese elemento; **doble Trigger / A** conserva todos los de su mismo tipo dentro de la selección actual, incluidas otras páginas, sin incorporar aliados cercanos que no estaban seleccionados. **Mantener Trigger / A durante 0,65 s** quita ese elemento de la selección, sin Grip: la tarjeta muestra progreso rojo y se elimina antes de soltar. En escritorio se usa el mouse izquierdo. El clic simple se aplica al terminar la ventana de doble clic de 0,35 s. Salir de la casilla mientras se mantiene cancela el gesto.

**ACTIONS** contiene únicamente las órdenes de la selección: construcción para trabajadores, órdenes militares para unidades, habilidades cuando hay casters y producción o investigación para edificios compatibles. **Cancel / refund** aparece mientras hay una construcción o cola que cancelar. Los edificios automáticos y las selecciones incompatibles muestran una explicación.

Las acciones se agrupan con encabezados y colores: **BUILD STRUCTURES** en ámbar, **TRAIN UNITS** en verde, **RESEARCH** y **CASTER ABILITIES** en violeta, **UNIT ORDERS** en azul y **CANCEL / REFUND** en coral. Construcción, habilidades y órdenes de unidades ocupan filas separadas.

Al elegir una estructura, su botón conserva un borde ámbar y **SELECTED** aunque se retire el puntero. **PLACING · nombre** indica si se está colocando una barraca, relay u otro edificio. El resaltado cambia al elegir otra estructura y se limpia al colocarla o cancelar, tanto desde el panel como desde la rueda.

Mantener el cursor sobre una acción durante **1,5 segundos** muestra su **FIELD GUIDE** en la tarjeta lateral: nombre, función del edificio o unidad, costo, tiempo y requisitos cuando corresponda. También explica órdenes, mejoras y cancelaciones. La ayuda reemplaza temporalmente la ficha de selección, sin tapar botones, recursos ni producción; desaparece al cambiar de acción, retirar el cursor, pulsar, cambiar la selección o salir del panel. Todo el texto del juego permanece en inglés.

En los menús de Quest, **A funciona como el gatillo**: pulsar y soltar sobre un botón lo activa; soltar afuera cancela. Mantener A permite guardar grupos y arrastrar el menú. Si se usan ambos botones juntos, solo se ejecuta una acción. Las pulsaciones iniciadas en el menú se consumen allí, incluso si el cursor termina sobre el mapa.

Al seleccionar un edificio productor terminado, **PRODUCTION QUEUE** muestra una tarjeta grande para la unidad o investigación activa, su barra de progreso, porcentaje y segundos restantes. A su derecha aparecen los cuatro lugares siguientes, numerados en orden de salida y con nombre/rol. El contador incluye el elemento activo; **5 / 5 · FULL** indica que no entran más órdenes. **Queue empty** permanece visible antes de producir para que los botones no cambien de posición al agregar unidades. **Cancel / refund** elimina el último elemento y devuelve su costo.

Para cancelar un pedido concreto, apuntar directamente a su tarjeta en **PRODUCTION QUEUE**: se vuelve **roja** y muestra **Cancel + refund**. Un clic con **A**, **gatillo** o **mouse izquierdo** cancela solo ese elemento, **sin confirmación**, devuelve su costo completo y libera su reserva de suministro. También se puede cancelar la producción activa, una investigación o una unidad lista que espera salida. Los demás pedidos conservan su orden; si se cancela el activo, empieza el siguiente. Los lugares vacíos no se pueden cancelar. Si la cola avanza durante la pulsación, nunca se aplica la cancelación a una unidad distinta.

Una producción terminada que aún no puede salir muestra **WAITING TO DEPLOY**, con **Needs more supply** si faltan lugares para las unidades existentes o **Clear the building exit** si la salida está bloqueada. Las pausas por seguimiento/visor muestran **PAUSED** y conservan el progreso. La sección también distingue investigaciones de unidades y se oculta al seleccionar unidades o cuando ningún edificio seleccionado puede producir.

Al seleccionar varias barracas u otros edificios, el panel y la rueda muestran sus acciones de producción compatibles. **Cada pulsación encarga una sola unidad o mejora**, usando el productor terminado con menos tiempo pendiente de trabajo; en empate, la cola más corta y luego el ID. Si un edificio tiene una unidad terminada bloqueando su cola porque no puede salir, se prefiere otro productor que pueda avanzar. Los edificios incompatibles, incompletos o con cinco pedidos no reciben el encargo. Se mantienen los mismos costos, requisitos y reservas de suministro. El panel muestra un resumen de los productores seleccionados, actividad y pedidos; para cancelar un pedido hay que seleccionar su edificio individualmente.

**CONTROL GROUPS** tiene su propia franja: cada grupo muestra **Empty** o su cantidad asignada. Pulsar recupera el grupo; mantener guarda la selección. **MATCH** contiene el reloj y **Pause**, separados de las acciones de unidades y edificios. **Full map** aparece únicamente en el modo de prueba de escritorio. La ocupación de depósitos y el aviso **excess** permanecen en otra franja, sin reemplazar los recursos globales.

### Casters y habilidades

Cada facción tiene una academia que se construye con un trabajador después de terminar su edificio de tecnología. Produce una unidad de apoyo con ataque básico a distancia y dos habilidades activas:

| Facción | Academia | Construcción | Requisito | Caster | Producción |
| --- | --- | --- | --- | --- | --- |
| The Consortium | **Resonance Lab** | 200 M + 100 G · 30 s | Arsenal terminado | **Conduit** | 150 M + 75 G · 22 s |
| Symbiosis | **Spore Sanctum** | 200 M + 100 G · 30 s | Chrysalis terminada | **Oracle** | 150 M + 75 G · 22 s |

Cada caster ocupa un lugar de suministro, aparece con **100 de energía** y recupera **2 por segundo**, hasta ese máximo. La pausa detiene la regeneración y los tiempos de recarga. Su ataque básico es automático; las habilidades del jugador se activan manualmente.

| Habilidad industrial / biológica | Efecto instantáneo | Energía | Recarga | Alcance / radio |
| --- | --- | --- | --- | --- |
| **Ion Burst / Spore Burst** | 45 de daño a cada enemigo visible dentro del área, incluidas estructuras; sin fuego amigo | 50 | 8 s | 8 / 3 celdas |
| **Repair Field / Mend** | Recupera hasta 40 de vida de cada unidad propia dentro del área, sin superar su máximo; no cura edificios | 40 | 10 s | 8 / 3 celdas |

Seleccionar uno o varios casters, solos o junto al ejército, muestra **CASTER ABILITIES** en el panel y las mismas habilidades en la rueda. La ficha muestra **ENERGY**, el total disponible de los casters seleccionados y cuántas habilidades están listas; cada acción indica su costo y cantidad de casters preparados, o la energía/recarga que falta.

Elegir una habilidad abre el apuntado sin lanzar todavía: la mira y el rayo son **violetas para daño** y **verdes para curación**. Un círculo marca el área de efecto en el destino y otro el alcance del caster preparado. Apuntar fuera de visión marca el área en rojo. Confirmar un punto visible con **A o gatillo**; **B** cancela antes de confirmar. Un rechazo conserva el modo para reintentar, sin gastar energía. Un segundo A rápido sobre el mismo lugar no reemplaza el cast por movimiento.

Cada confirmación usa **un solo caster**, dando preferencia a uno preparado que ya tenga alcance y línea de visión; los demás conservan sus órdenes. Si necesita acercarse, lo hace y lanza una sola vez al entrar en alcance. La energía y la recarga se consumen al ejecutar el efecto. Luego recupera su orden anterior —Move, Attack Move, ataque directo, Stop o Hold— y conserva los destinos que ya tenía en cola. Si estaba detenido o en Hold y agregás destinos durante el cast, al terminar empieza la primera orden encolada.

Una nueva orden manual de movimiento, ataque, Stop o Hold cancela un cast que todavía se está preparando. Si pierde la visión del punto, muere o ya no puede alcanzarlo, no ejecuta ni cobra el efecto. Las habilidades no se añaden a la cola con grip. **Repair Field** y **Mend** curan tropas; los edificios de ambas facciones se reparan con trabajadores.

### Movimiento e interacción

**Attack Move** permite manejar el ejército con una orden de grupo: avanza hasta el destino, combate y retoma el avance cuando termina el enfrentamiento. La selección automática prioriza soldados y torres terminadas, después trabajadores y otras unidades, y por último edificios sin ataque. Los trabajadores que disparan pasan a ser amenazas. Si aparece un defensor mientras tus unidades atacan automáticamente un edificio, cambian al defensor.

Al elegir **Attack Move** desde el panel o la rueda, la mira del tablero y el rayo del mando se ponen **rojos** mientras elegís el destino. La orden se confirma con una pulsación separada de A o del gatillo sobre el mapa; B cancela. Un destino rechazado conserva el modo rojo para reintentar. Al confirmar, el rojo permanece brevemente como señal de aceptación. Las unidades combaten los enemigos y estructuras que encuentran, adquieren otro objetivo al eliminar el anterior y retoman la marcha cuando despejan el camino.

Entre amenazas equivalentes prefieren una que puedan atacar desde donde están y conservan un objetivo válido para no cambiar de enemigo a cada instante. Si el objetivo se retira fuera de alcance, pueden cambiar a otro que ya esté al alcance. Buscan una posición con línea de tiro y descartan temporalmente objetivos inaccesibles, sin borrar el destino de Attack Move. Ceder el paso a un aliado no impide disparar a un enemigo al alcance.

Una **orden directa sobre un enemigo** mantiene el foco que elegiste, incluso si es un edificio. Si muere, continúa la próxima orden encolada; si no hay ninguna, adquiere otro enemigo cercano. Las unidades que todavía están lejos continúan hacia la última zona observada en Attack Move: una baja lograda por la primera línea no deja detenida a la retaguardia. Al perder visión, busca el foco durante un máximo de tres segundos, sin seguir posiciones ocultas; después puede completar el avance hacia esa zona conocida. Si el objetivo visible queda inaccesible, puede atacar otros enemigos mientras reintenta el foco indicado.

Al confirmar **Attack Move desde el menú**, un segundo A rápido sobre el mismo lugar conserva la orden, sin convertirla en Move ni duplicar un destino encolado. Doble A también tolera que muera el objetivo entre toques, que una baja reduzca la selección o que el puntero pase del terreno a una unidad cercana. Una orden rechazada conserva el modo de apuntado para poder elegir otro destino.

**Move** sirve para retirarse o reposicionarse y no se interrumpe para atacar. **Stop** cancela órdenes y deja que los soldados combatan automáticamente; **Hold Position** dispara sin perseguir ni ceder su posición. Ningún cambio de objetivo reinicia el tiempo de recarga. Estas reglas son iguales para el jugador y la IA.

Las unidades se distribuyen al llegar a un destino, rodean el tráfico y ceden espacio a aliados que necesitan pasar. Los empujes conservan la orden anterior; **Hold Position** impide que otros aliados desplacen esa unidad. Las construcciones y la producción respetan el tamaño de los cuerpos.

Las unidades pesadas tienen prioridad frente a las livianas. MOVE responde desde el siguiente paso de simulación y empieza a orientar el modelo inmediatamente, conservando inercia: cada tipo acelera, frena y cambia de dirección a su ritmo. **Stop** frena; **Hold Position** ancla la unidad. Una nueva orden reemplaza cualquier maniobra automática para ceder el paso.

En Quest, el tablero queda fijo al confirmar su colocación: los joysticks ya no desplazan ni amplían el mapa, y pulsar el stick principal tampoco cambia su vista. Para cambiar posición, tamaño u orientación, abrir **Settings → Reposition board** y confirmar una nueva colocación. Durante ese ajuste, el joystick derecho modifica tamaño/giro; sostener el grip derecho cambia el eje vertical a altura, sin alterar tamaño ni giro. El modo de prueba de escritorio conserva sus controles de navegación para desarrollo.

El menú comienza elevado detrás del tablero. Para moverlo, apuntar a **MOVE MENU**, mantener el gatillo o A y arrastrar; mientras se sostiene, el stick principal acerca o aleja el panel. Al soltar, queda fijo respecto del tablero y guarda esa posición para la próxima apertura. El panel gira para poder leerse al caminar alrededor. **Reset position** lo devuelve arriba del mapa; la tecla **M** también lo recupera en escritorio. Inclinar el joystick abre la rueda unificada únicamente durante la interacción libre con el mapa; mientras se arrastra el menú mantiene su ajuste de distancia.

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
| Abrir rueda unificada | Inclinar el joystick desde el centro | — |
| Elegir categoría | Arriba: Groups; derecha: Actions; izquierda: Orders | — |
| Entrar / ejecutar en la rueda | Gatillo del mando activo | — |
| Volver / cerrar la rueda | Abajo + gatillo o B; dentro de Groups, B | — |
| Cerrar por inactividad | Dejar el joystick centrado durante 0,8 s | — |
| Recuperar grupo en la rueda | Entrar a Groups, inclinar hacia un grupo y gatillar | — |
| Reemplazar un grupo con la selección | Dentro de Groups: mantener pulsado el joystick, inclinar y soltar la pulsación | — |
| Cancelar / opciones | B | Escape |
| Desplazar mapa (solo escritorio) | — | WASD |
| Zoom (solo escritorio) | — | Rueda |
| Ver mapa completo (solo escritorio) | — | F |
| Mover menú | Mantener gatillo o A sobre MOVE MENU; stick principal para distancia | Arrastrar MOVE MENU; rueda para distancia |
| Recuperar menú arriba del tablero | Reset position en el panel | M |
| Pausa | B o panel | Espacio |
| Grupo de control | Cuatro botones del panel; mantener para guardar | 1–4; Ctrl+1–4 para guardar |

El juego incluye selección ampliada, contorno de selección al ras del terreno, previsualización de construcción, panel contextual, avisos, sonidos de confirmación y vibración. El rectángulo sigue el suelo y las rampas sin flotar ni dibujar paredes en los desniveles. Todos los controles del juego usan el único mando elegido en **Settings → Hand: Left/Right**. El otro no controla el juego ni participa en la captura de hotkeys. La elección se guarda junto con las asignaciones; cambiar de mano conserva las funciones configuradas y actualiza sus nombres. Al cambiar de mano se descartan los gestos pendientes y se espera que el nuevo mando quede libre y centrado antes de aceptar entrada.

El **grip derecho** funciona como Shift: mantenerlo mientras se selecciona con el gatillo principal suma unidades a la selección actual, también al arrastrar un área. Al pulsar una unidad ya seleccionada con el grip sostenido, se quita de la selección. Sin el grip, una nueva selección reemplaza la anterior. El modificador está en el mando derecho, junto con el gatillo de selección.

Mantener **grip + gatillo durante 0,5 segundos** sobre una unidad ya seleccionada la quita una sola vez; soltar no vuelve a seleccionarla. Si el puntero se arrastra, el gesto sigue siendo selección de área. **Doble gatillo** sobre una unidad o edificio selecciona los propios de esa misma definición dentro de doce celdas del objetivo; con grip conserva el resto de la selección. En escritorio funciona con doble clic izquierdo y Shift. Por ejemplo, doble clic en una barraca selecciona las barracas cercanas, incluidas las que están en construcción, sin sumar otras bases lejanas ni edificios enemigos.

### Rueda unificada

Durante el juego aparece un **hint compacto junto al mando activo**, incluso con la rueda cerrada: arriba **Groups**, derecha **Selection**, izquierda **Unit orders**. Es el nivel 0. Al inclinar el joystick desde el centro se despliega la rueda normal del nivel 1 y se resalta la dirección elegida. **El gatillo entra a la opción**; mantener una dirección conserva la rueda abierta. Centrar el joystick durante **0,8 s sin acción** la cierra. El hint se oculta al pausar, colocar el tablero o perder el seguimiento.

**Groups** permite recuperar o guardar grupos; **Selection** reúne las acciones de la selección, como Build, Train, Research y habilidades de casters; **Unit orders** ofrece Attack Move, Stop y Hold Position. Los trabajadores abren los edificios disponibles desde **Build**. **Abajo + gatillo** retrocede un nivel o cierra desde la raíz; dentro de Groups, abajo corresponde al grupo 3 y **B** vuelve. B también permite retroceder en los demás niveles. Al cambiar de nivel, centrar el joystick antes de elegir otra opción evita confirmar una dirección heredada. La rueda muestra el nombre de la selección, la acción y su costo o efecto; las órdenes rechazadas indican el motivo.

La navegación usa siempre el gatillo físico y B/Y del mando activo. A/X y grip no ejecutan opciones ni cambian de nivel; sus funciones del mapa se conservan al cerrar. Las pulsaciones de joystick y el doble grip no abren la rueda. Al elegir una construcción, Attack Move o una habilidad, la rueda se cierra y espera un destino nuevo en el mapa, después de soltar los botones. Mientras está abierta consume la entrada para que la misma pulsación no seleccione ni ordene sobre el tablero. Cambiar la selección, perder una unidad seleccionada, pausar, perder foco/seguimiento o terminar la partida cierra la rueda. La consola permanece disponible para recursos, colas y las mismas acciones.

### Rueda de grupos

La rueda unificada se abre inclinando el joystick desde el centro. **Arriba → gatillo** entra a **Groups**; todavía no cambia la selección. Dentro aparecen cuatro grupos: **1 arriba, 2 a la derecha, 3 abajo y 4 a la izquierda**. Inclinar resalta un grupo y **gatillar lo recupera**. Volver al centro no selecciona nada; tras **0,8 s** sin acción la rueda se cierra. **B** vuelve al nivel principal.

Para guardar, primero entrar a **Groups**. **Mantener pulsado el joystick desde el centro, inclinar hacia el grupo y soltar la pulsación** reemplaza todos sus miembros con la selección actual. El modo amarillo **SAVE GROUP** distingue este gesto de la recuperación. Se puede girar para cambiar el destino antes de soltar; volver al centro mientras sigue apretado conserva el grupo resaltado, sin guardar todavía. B cancela. La pulsación del joystick no abre la rueda ni guarda fuera de Groups.

Los contadores excluyen unidades y edificios destruidos. Se pueden guardar unidades y edificios propios, incluidos grupos de varias barracas; recuperar un grupo conserva sus colas y puntos de reunión. Los grupos se reinician al comenzar otra partida. Los cuatro botones del panel conservan clic para recuperar y pulsación larga para guardar. El modificador de guardado de la rueda se puede reasignar desde Hotkeys; las direcciones y el gatillo de navegación son fijos.

Durante colocación o arrastre del menú, el eje conserva su función de ajuste. Tras esas operaciones, pausa, pérdida de foco o cierre de rueda, hay que volver al centro antes de abrirla otra vez. El grip mantiene la selección aditiva y las colas; su doble pulsación no abre menús.

### Punto de reunión de edificios

Seleccionar un centro principal, barraca o edificio de tecnología terminado y pulsar **A sobre el terreno** establece su **Rally Point** (clic derecho en escritorio). También funciona con varios productores propios seleccionados. Cada edificio conserva un único destino; volver a pulsar A lo cambia, incluso con grip o doble A. El indicador cian con bandera y línea punteada conecta el edificio con ese punto, y la consola muestra el destino en **RALLY POINT**.

Las unidades nuevas reciben una orden Move al desplegarse, usando el destino vigente en ese momento y las reglas normales de separación y movimiento. Cambiar el punto no altera unidades que ya salieron ni la cola de producción. Los trabajadores también van al punto elegido; este ajuste es un destino de movimiento, no una orden automática de recolección.

**Clear Rally Point**, disponible en la consola y la rueda, borra el destino sin cancelar producción. Así se recupera el comportamiento predeterminado: los trabajadores buscan recursos y las unidades militares quedan junto a la salida. Los destinos fuera del mapa, bloqueados o sin camino según la información conocida se rechazan conservando el punto anterior. Si el terreno cambia después y el punto queda inaccesible, la unidad sale en un espacio seguro; se muestra un aviso cuando el bloqueo también es conocido. Si la propia salida está ocupada, la producción espera como antes.

### Cola de acciones de unidades

**Grip + A** agrega una orden al final del plan de cada unidad seleccionada. Si está quieta, empieza de inmediato; si está ocupada, conserva su trabajo actual. Repetir A en otro lugar agrega otro destino. Una orden válida sin grip reemplaza el plan; **Stop** y **Hold Position** lo limpian inmediatamente.

**Doble A** hace Attack Move sobre terreno o ataque directo sobre un enemigo visible. Con grip también se encola. El primer A responde inmediatamente; el segundo convierte esa misma orden en ataque, sin dejar un Move extra. Las pulsaciones deben estar separadas por un máximo de 0,35 segundos y apuntar al mismo objetivo o a un punto cercano (hasta 1,5 celdas). Dos ubicaciones diferentes siguen siendo dos órdenes. Cambiar selección, soltar grip, entrar al menú/rueda o pausar descarta el doble clic pendiente.

La mira y el rayo se ponen rojos durante **0,8 segundos** al aceptar el ataque con doble A. El regreso al color normal indica que terminó esa confirmación visual; la orden de ataque de las unidades continúa. Una nueva orden de movimiento limpia la señal roja inmediatamente.

La sección **UNIT ORDER PLAN** distingue estas acciones de la producción de edificios. Muestra cuántas quedan y los próximos pasos. En el mapa aparecen hasta ocho destinos con puntos y números, unidos por líneas punteadas al ras del terreno y las rampas. El número 1 corresponde a la acción actual. Los puntos y sus enlaces usan **cian para Move**, **rojo para Attack y Attack Move** y **verde para trabajo** (recolección, construcción y reparación). Para grupos se muestra el plan de una unidad representativa identificada en el panel. Las líneas conectan objetivos en el orden de ejecución; la ruta real se adapta a obstáculos y unidades.

Cada unidad admite hasta **32 órdenes pendientes**. Move/Attack Move avanzan al llegar; Attack al desaparecer el objetivo o perderlo de vista; Gather después de entregar una carga; Repair al terminar la reparación. En ambas facciones, Build mantiene al trabajador en la obra y avanza a la siguiente orden solo al terminar el edificio. Las órdenes se revalidan al comenzar y se omiten con aviso si ya no se pueden ejecutar. Las construcciones encoladas se pagan al activarse, no al agregarlas. Detalles técnicos y pruebas: [colas de acciones](order-queues.md).

La selección por área puede **empezar o terminar fuera del tablero**: mantener el gatillo (o clic izquierdo), arrastrar sobre las unidades y soltar, incluso fuera del borde opuesto. El rectángulo y la selección se recortan a la parte visible del mapa; con el zoom de escritorio no se seleccionan unidades recortadas fuera del tablero. Se conserva el modificador para agregar unidades. Un clic o arrastre completamente externo no cambia la selección; apuntar al menú o perder una proyección válida cancela el arrastre. Las órdenes contextuales y la confirmación de construcción siguen requiriendo apuntar al terreno.

Los **minerales** se pueden apuntar directamente sobre el cristal, incluso mirando el tablero de costado, o cerca de su base gracias a una zona de interacción más amplia. El depósito bajo el cursor se ilumina y recibe un aro sobre el terreno antes de pulsar. El aro y el panel usan el mismo objetivo que recibirá la orden de recolección. Los trabajadores cercanos no ocultan el mineral con su margen de selección; apuntar directamente al cuerpo del trabajador sigue seleccionándolo. Una pulsación con un pequeño movimiento de la mano conserva el depósito elegido al presionar; arrastrar más lejos mantiene la selección por área. La asignación automática a depósitos disponibles sigue funcionando igual.

Al apuntar al mapa aparece un **punto blanco con líneas de mira turquesa** en la intersección del láser con el terreno. Está al ras del suelo y las rampas, mantiene su tamaño con el zoom de escritorio y se recorta en el borde del tablero. También funciona con el mouse. Sigue visible sobre niebla como referencia del cursor, sin revelar información; desaparece al salir del tablero, apuntar al panel o pausar.

Al apuntar a una unidad o edificio, el modelo se ilumina y aparece un contorno sobre el suelo: circular para unidades y cuadrado para edificios, amarillo en aliados y rojo en enemigos. Se muestra antes de seleccionar y se apaga al retirar el puntero, apuntar al menú o perder el foco. El disco de selección permanece en los objetos seleccionados. El contorno de hover sigue el terreno y las rampas, respeta el borde del tablero y no aparece sobre recuerdos enemigos bajo niebla de guerra.

Al **recibir daño**, unidades y edificios visibles muestran un destello claro en el modelo durante unos 0,2 segundos y un aro naranja que se expande sobre ellos durante unos 0,6 segundos. La barra de vida se vuelve naranja durante ese intervalo. Varios impactos renuevan la señal sin acumular aros; también se indica el daño de área. El efecto no necesita selección y es independiente del destello del atacante. Los colores de equipo y rol vuelven al finalizar el pulso; los recuerdos enemigos bajo niebla no muestran impactos.

Al **disparar**, las unidades se orientan hacia el objetivo real y aparece un destello en el arma, un proyectil con estela y chispas al llegar. Las armas industriales usan trazadoras doradas; las biológicas, proyectiles verdes más gruesos; la artillería dibuja un arco elevado y un impacto más amplio. Trabajadores y combatientes cercanos muestran un golpe corto. Las torres también muestran sus disparos. Una estela tenue conserva brevemente la conexión entre atacante y destino, incluso si el golpe destruyó al objetivo. Son efectos visuales: no cambian daño, cadencia ni tiempo de resolución del combate. Se recortan al tablero y a la visión actual, y se detienen durante la pausa.

El juego se presenta íntegramente en inglés; la incorporación de otros idiomas queda pendiente para una etapa posterior.

## Contenido

- **The Consortium**: identidad industrial.
- **Symbiosis**: identidad orgánica. Su biomasa y animaciones son visuales, sin ventajas de juego.
- Ambas facciones tienen exactamente los mismos costos, tiempos, vida, movimiento, combate, suministro, visión y efectos de habilidades por rol. Comparten construcción atendida y reparación con trabajadores; no tienen regeneración pasiva de vida. Las diferencias de reglas quedan para una etapa de diseño y balance posterior.
- Cada facción: trabajador, explorador, combate cercano, tirador, asedio y caster; siete edificios; dos mejoras y dos habilidades de caster.
- Hasta 60 unidades móviles y 20 edificios por jugador.
- **The Rift**, mapa simétrico de 128 × 128, con dos bases, cuatro expansiones, alturas y rampas.
- **Training Grounds**, mapa de 48 × 48 con instrucciones contextuales.
- IA **Easy**, **Normal** y **Hard**; admite los cuatro cruces de facciones. No usa un modelo de lenguaje ni acceso a internet.

Las unidades tienen siluetas y símbolos de rol compartidos por ambas facciones: **herramienta amarilla** para trabajadores, **flechas verdes** para exploradores, **escudo crema** para combate cercano, **mira blanca** para tiradores, **tres proyectiles violetas** para artillería y **estrella violeta** para casters. Los trabajadores llevan carga y pinzas; la infantería cercana lleva escudo, los tiradores un arma larga y la artillería dos cañones o espinas. Los casters tienen una silueta erguida con corona abierta y un orbe. El casco y el borde del símbolo conservan el azul aliado y el rojo enemigo.

El panel muestra el rol junto al nombre al apuntar sin selección, seleccionar o producir. Las selecciones mixtas muestran cantidades por nombre de unidad o edificio. Las barras de vida sobre las unidades aparecen al seleccionar, apuntar o recibir daño, para despejar la vista del ejército.

Los edificios también se identifican por **silueta, altura y símbolo**, además del nombre al apuntar o seleccionar. El casco conserva el azul aliado o rojo enemigo; los acentos indican el rol y son compartidos entre facciones:

| Rol | Industrial / biológico | Rasgo reconocible | Símbolo |
|---|---|---|---|
| Base central | Command Core / Heart | Ciudadela escalonada o núcleo con corona de órganos | Corona dorada |
| Barraca | Barracks / Brood Nest | Hangar bajo con entrada ancha o nido con tres huevos | Dos chevrones naranjas |
| Gas | Refinery / Siphon | Dos chimeneas o conductos abiertos | Gota violeta |
| Suministro | Relay / Node | Depósitos bajos o sacos conectados | Signo más verde |
| Tecnología | Arsenal / Chrysalis | Reactor vertical o capullo rodeado de anillos | Rombo celeste |
| Academia | Resonance Lab / Spore Sanctum | Tres pilares alrededor de una cámara suspendida | Estrella violeta |
| Defensa | Sentinel / Stinger | Torre estrecha con cañón o aguijón | Mira clara |

Los símbolos forman parte del modelo y se leen desde arriba, sin añadir carteles flotantes a la partida. Las torres se orientan hacia donde disparan. Los recuerdos bajo niebla conservan la silueta y el símbolo en tonos apagados, sin colores activos ni señales de impacto.
