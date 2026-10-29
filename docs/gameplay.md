# Guía de juego

Los controles indicados a continuación son los predeterminados y se pueden personalizar desde **Settings → Controls → Hotkeys**. **Settings → Controls → Hand: Left/Right** elige un único mando activo (derecho inicialmente); con el izquierdo, X/Y reemplazan A/B y el puntero, gatillo, grip y stick pasan a esa mano. La experiencia de juego está orientada a Quest 3. La rueda unificada se usa únicamente en VR. Su navegación es fija: joystick para elegir y gatillo para confirmar. La versión desktop de Unity queda para desarrollo y prueba de mecánicas: mouse y teclado, acciones desde el panel, teclas **1–4** para recuperar grupos y **Ctrl+1–4** para guardarlos. Comparte las reglas del juego y conserva WASD, rueda del mouse y F para navegar cómodamente durante las pruebas. La versión para navegadores se compila desde las mismas fuentes con Unity WebGL y usa una interfaz desktop propia; sus controles están en [cliente Unity WebGL](web-client.md#controles). El juego cruzado con Quest requiere un servidor con WebSocket/WSS compatible.

## Empezar una partida

En la app móvil y en la web táctil se usa un tablero de pantalla, en vertical u
horizontal y sin colocación en una habitación. Tocar **Nueva partida**, elegir
facción/mapa/dificultad y comenzar. Arrastrar con un dedo mueve la cámara.
Tocar un aliado cambia la selección a ese aliado, también después de mover otra
unidad. Con unidades seleccionadas, tocar terreno, enemigos o recursos envía
una orden. La unidad tocada se conserva hasta soltar el dedo aunque se mueva;
arrastrar o pellizcar cancela la selección pendiente. Un toque lento sin selección
previa también selecciona. **Cancelar / limpiar** quita la selección. Mantener 0,55 segundos
y soltar conserva la orden contextual explícita, como reparar o reanudar un
edificio aliado con trabajadores. Los modos de apuntado explícitos mantienen
su prioridad sobre el cambio de selección.
**Shift** se activa/desactiva con un toque o se mantiene con otro dedo:
**Shift + arrastrar** rodea aliados con un lazo y los selecciona al soltar;
**Shift + toque** con unidades seleccionadas ordena **Avanzar atacando**.
El modificador se toma al empezar el gesto. Dos dedos también mueven la cámara
y pellizcar ajusta el zoom. Las acciones aparecen según la selección;
**Agrupar** la guarda en uno de cuatro iconos visibles del HUD, y tocar un icono
recupera ese grupo. Si los espacios están llenos se elige cuál reemplazar.
Los grupos conservan sólo sus integrantes vivos y se reinician con cada partida.
El HUD también ofrece cancelación y minimapa. Controles completos y
requisitos en [app/README.md](../app/README.md).

Al abrir el juego en Quest, el menú principal aparece frente a tu posición actual, a unos 1,25 m, con **New Match**, **Guides** y **Settings**, sin pedir colocar el tablero ni permiso de superficies. Su posición es independiente del ancla guardada. Si lo movés, conserva esa ubicación al terminar una partida, volver desde pausa o resultado y recuperar el foco o seguimiento del visor. Girar la cabeza o moverse cerca no lo recoloca. Solo vuelve automáticamente frente a vos si queda a más de 3,25 m; **Reset position** permite recuperarlo manualmente. Al cerrar y abrir de nuevo la app, el menú aparece frente a la posición actual. Si hay un ancla guardada, se intenta restaurar en segundo plano: tras estabilizarse el seguimiento, se conserva si está cerca y se olvida automáticamente si el tablero queda a más de 3 m en horizontal o en otro nivel con más de 2,5 m de diferencia vertical. En ese caso, se pide colocar el mapa al iniciar la partida. **Settings → Play space → Reposition board** sigue disponible para ajustar una posición cercana. El tablero permanece oculto hasta entrar a la preparación de partida o iniciar su colocación.

**New Match** reúne cuatro selectores: facción, rival, dificultad y mapa, junto con **Back**, **Position board** y **START MATCH**. **Position board**, al lado de comenzar, permite colocar o reajustar el mapa directamente; confirmar deja el tablero listo sin iniciar la partida y cancelar conserva su posición anterior si existía. Las preferencias y los ajustes avanzados del espacio siguen en **Settings**. **START MATCH** inicia directamente si el tablero ya está colocado; si falta, abre la colocación y la partida comienza al confirmar con A (X en la mano izquierda). Cancelar vuelve a la preparación sin iniciar. Si todavía se está restaurando el ancla o comprobando su distancia al visor, espera ese resultado antes de iniciar o pedir una nueva ubicación.

**Settings → Play space → Reposition board** permite ajustar la ubicación antes de empezar o desde pausa. Durante la colocación, mantener **grip achica** el tablero y mantener **gatillo agranda**. El **stick arriba/abajo sube/baja la altura** y a izquierda/derecha gira el tablero. **A confirma** y **B cancela** (**X/Y** con la mano izquierda). El mando muestra hints específicos; pulsar el stick los oculta o muestra. Estos controles de colocación son fijos aunque se cambien los hotkeys de juego. **Detect surfaces** solicita el acceso a la habitación cuando se elige; sin datos de habitación se usa el plano manual y se ajusta la altura con el mismo stick. En desktop, **Page Up/Page Down** cambia la altura, **clic izquierdo** confirma y **Escape** cancela.

Al comenzar se muestra el panel de acciones de unidades y edificios. Los seis trabajadores iniciales comienzan a recolectar. Seleccionar uno permite construir; seleccionar un edificio permite producir o investigar. Un extractor debe cubrir un depósito violeta. Se gana destruyendo todos los edificios enemigos.

### Mapas

El selector **Map** ofrece **Neon Loop** (48 × 48), **Forest Watch** (128 × 128) y **Desser Alert** (160 × 160). Neon Loop tiene cuatro zonas (dos iniciales y dos expansiones), Forest Watch seis (dos iniciales y cuatro expansiones) y Desser Alert ocho. Todas las tarjetas usan el mismo formato: tamaño, zonas, 1v1 y cantidades reales de depósitos `m/g`, seguidas de la temática. Neon Loop muestra `16m/4g`, Forest Watch `24m/6g` y Desser Alert `48m/16g`.

**Neon Loop** combina un suelo tecnológico oscuro con circuitos y anillos de neón. **Forest Watch** usa suelo de bosque y arboledas. **Desser Alert** conserva la arena, mesetas y areniscas, con cactus. Los elementos de vegetación permanecen en terreno bloqueado y respetan la niebla de guerra. Cada mapa reproduce en bucle la pista adjunta con su mismo nombre, y cambiar de mapa cambia también la música; el volumen sigue controlándose desde Settings → Audio.

**Desser Alert** es un desierto con un 56,25 % más de superficie que Forest Watch. Tiene **ocho zonas de recursos en total: dos iniciales y seis expansiones**, cada una con **seis depósitos de minerales de 1800** y **dos de gas de 2400**. Las zonas dejan espacio plano para una base de 4 × 4 y extractores de 3 × 3. Cinco mesetas, veinte rampas de doce celdas de ancho y afloramientos de arenisca ofrecen rutas altas y bajas; las rocas decorativas permanecen dentro de terreno bloqueado y no cierran caminos transitables. El terreno, las rampas, los recursos y las posiciones iniciales conservan simetría de 180° para ambos jugadores.

### Dificultad de la IA

El botón **AI** permite elegir **Easy**, **Normal** o **Hard** antes de iniciar. La elección se conserva al volver al menú para otra partida y al jugar revancha; no se guarda entre aperturas de la aplicación.

Todas las dificultades usan **el catálogo completo** de su facción: trabajadores, exploradores, infantería cuerpo a cuerpo, infantería a distancia, artillería y casters; también construyen tecnología, academias, centros de mejoras, defensas e investigan ataque y resistencia. Los casters usan las mismas habilidades, energía, recarga y visión que los del jugador.

| Modalidad | Reacción | Ataque principal desde | Ejército enviado | Pausa de recuperación | Objetivo de trabajadores / ejército |
| --- | --- | --- | --- | --- | --- |
| **Easy** | 3 s | 5 min, con al menos 6 soldados | 65 % disponible, hasta 22 | 65 s | 16 / 28 |
| **Normal** | 2 s | 3 min 30 s, con al menos 9 soldados | 80 % disponible, hasta 32 | 35 s | 20 / 36 |
| **Hard** | 1 s | 2 min 30 s, con al menos 10 soldados | 95 % disponible, hasta 38 | 20 s | 22 / 38 |

Los tres niveles producen más rápido, exploran antes y refuerzan sus ataques. Easy conserva mayor tiempo de preparación; Normal moviliza una parte mayor de su ejército; Hard desarrolla antes su economía y tecnología, utiliza hasta cuatro barracas y mantiene la presión con casi todas las tropas disponibles. Si su economía necesita menos trabajadores, emplea esos espacios en más soldados, respetando siempre el límite de 60 unidades. Estos tiempos indican cuándo puede comenzar la ofensiva principal; necesita reunir el mínimo de unidades. Los exploradores pueden adelantarse y la defensa de la base funciona desde el inicio.

La producción aprovecha varios edificios a la vez y sigue entrenando unidades disponibles mientras desbloquea artillería y casters. La economía considera las asignaciones de recolección, las colas de producción y el presupuesto restante de cada decisión. La IA usa únicamente su visión, edificios recordados y datos públicos del mapa; puede defender sus bases, retirarse y reagruparse antes de volver a atacar.

En los tres mapas busca y ocupa depósitos de otras zonas: Easy y Normal planean hasta dos bases y Hard hasta tres. Reserva recursos para construirlas y traslada trabajadores a las bases terminadas. Neon Loop ofrece dos expansiones neutrales y mantiene sus dos campamentos iniciales. La recolección automática del jugador sigue limitada a los minerales de su base; los traslados de la IA son órdenes deliberadas.

**Todas las partidas empiezan con las mismas condiciones para ambos jugadores:** 400 minerales, cero gas, una base terminada, seis trabajadores, capacidad inicial de diez y ninguna mejora. La dificultad no cambia ingresos, costos, daño, salud ni los límites de las reglas. En los tres mapas, las posiciones de los trabajadores y sus depósitos iniciales también están reflejadas, para igualar la distancia de salida. Ambas facciones tienen las mismas reglas y estadísticas por rol; los objetivos de población de la tabla son decisiones de la IA, no límites impuestos al jugador.

### Configuración, pausa y regreso al inicio

Los menús reservan el **turquesa LEGION** para selección, hover y acciones principales. El **lila del tutorial** se usa como acento fuerte en **New Match** y en los elementos destacados de la guía; **Settings** mantiene el fondo normal de los botones, sin resaltado. Los textos de navegación, los hints —incluidos los de mover ventana— y los números de grupos son blancos. Estos últimos se muestran más grandes para facilitar su lectura. En partida se conservan los colores funcionales de construcción, entrenamiento, órdenes, investigación, recursos, energía, salud, errores, advertencias y colas; la paleta de marca no sustituye esos significados. Los fondos siguen planos, sin contornos de texto ni efectos adicionales.

**Settings** está disponible desde el menú principal y la pausa, con las mismas pestañas en ambos lugares:

| Pestaña | Opciones |
| --- | --- |
| **Controls** | Mano activa, **Hotkeys** y **Selection: Bodies / Bases**. |
| **Audio** | Volumen de **Music** y **Effects** por separado. |
| **Visuals** | **Iluminación: Actual / Mejorada (experimental)** y densidad de niebla: 0 %, 25 %, 50 %, 75 % o 100 %. 0 % quita la neblina visual; el terreno conserva su opacidad en todos los niveles. |
| **Play space** | **Reposition board** y **Detect surfaces**. La altura se ajusta durante la colocación. |
| **Language / Idioma** | **Sistema**, **English** o **Español**. Se aplica al instante y se guarda en el dispositivo. |

El idioma inicial sigue al sistema: español si está disponible y, para idiomas aún no incluidos, inglés. **Configuración → Idioma → Sistema** vuelve a seguir el idioma del dispositivo al abrir la app. El selector también está disponible en pausa y conserva la partida y la selección actuales.

**Settings → Controls → Hotkeys** abre **Edit bindings** únicamente para el dispositivo activo: el mando Touch elegido en Quest o mouse/teclado en desktop. Elegí **Change**, soltá los controles y pulsá la nueva tecla o botón. Se guarda automáticamente en este dispositivo y se conserva al reiniciar el juego. Si la entrada ya está asignada, **Swap bindings** intercambia ambas funciones después de confirmar.

**Cancel change**, Escape durante la captura o esperar 12 segundos cancelan el cambio. **Restore device defaults** pide confirmación y restaura solamente el dispositivo seleccionado. Los modificadores de teclado aceptan Shift, Ctrl o Alt, de cualquiera de los dos lados. Los gestos de arrastrar, mantener y doble pulsación siguen al botón de su función; el joystick y la rueda del mouse conservan sus ejes. El mando que no está elegido queda sin asignación.

La navegación de los menús conserva **gatillo/A y B** con el mando derecho, **gatillo/X y Y** con el izquierdo, y **clic izquierdo y Escape** en desktop, incluso después de editar los controles de juego. Esto permite recuperar la configuración. Durante la captura no se ejecutan órdenes ni se reanuda la partida. **Back** retrocede un nivel: de Hotkeys a Settings, de cada guía a Guides y desde esos menús al inicio o pausa donde se abrieron. Si se abrió desde pausa, la partida sigue pausada.

Dentro de una partida, **Pause**, **B** con la rueda cerrada y sin una acción pendiente, o **Espacio** abren la pausa. Esta pantalla reúne **Resume match**, **Guides**, **Settings** y **Return to main menu**, con la misma presentación y jerarquía de acciones que el inicio. **Resume match** continúa; consultar guías o cambiar preferencias mantiene la simulación detenida. **Return to main menu → Leave match** termina la partida y devuelve al inicio; **Keep playing** cancela la salida y vuelve al menú de pausa sin reanudar la partida. Al finalizar por victoria o derrota, aparecen **Rematch** y **Main menu**, sin opción de reanudar una partida terminada.

**Settings → Controls → Selection: Bodies / Bases** cambia el modo de apuntado antes o durante una partida. **Bodies** añade el volumen de los modelos al área del suelo y es el valor inicial; **Bases** usa solamente las áreas del suelo. En ambos modos, una unidad tiene prioridad sobre edificios, minerales y gas mientras el puntero permanezca dentro de su área de selección (radio de 1,6 celdas). Los edificios y los dos tipos de recursos comparten un radio mínimo de ayuda: `clamp(0,03 m / escala de celda, 2,5, 4)` celdas. Los edificios grandes conservan su área mayor. Fuera del área de la unidad se puede apuntar al edificio o depósito; las ayudas superpuestas eligen el centro más cercano y la huella de un edificio conserva prioridad sobre el recurso que ocupa. El resaltado y las órdenes contextuales usan el mismo objetivo. La elección se guarda en el dispositivo y se puede revertir desde ese botón.

La selección con rectángulo incluye unidades y edificios propios, también en construcción, cuando su centro está dentro del área. Permite selecciones mixtas y aditivas, y respeta la porción del mapa visible al hacer zoom. No incluye enemigos, edificios recordados por la niebla ni entidades destruidas. Si hay unidades y edificios seleccionados juntos, **las unidades tienen prioridad**: el mapa, Opciones y la rueda actúan sobre ellas, manteniendo todas las tarjetas seleccionadas. Las órdenes de mover, atacar, detener, mantener posición y las acciones de trabajadores/casters no incluyen edificios; sus puntos de reunión y colas de producción se conservan.

**Guides** está disponible desde el inicio y la pausa, y reúne **Control guide**, **Units** y **Structures**. **Control guide** tiene seis categorías: **Selection**, **Orders**, **Menus**, **Groups**, **Production** y **Board**. Cada tarjeta explica el comando, el botón/gesto y su efecto. Se incluyen selección múltiple, colas, Attack Move, Stop/Hold, rueda unificada, asignación con clic de stick dentro de Groups, producción, cancelaciones, Rally Points y colocación. Cambiar de categoría con gatillo/A no ejecuta comandos ni reanuda la partida; **Back/B** vuelve a Guides. **Hotkeys** comparte el catálogo de esta guía en sus seis categorías de consulta y vuelve a Settings. La lista completa para revisión está en [hotkeys predeterminados](hotkeys.md). La guía y las categorías de referencia muestran las asignaciones actuales; **Edit bindings** vuelve al editor.

**Units** y **Structures** permiten consultar ambas facciones mediante pestañas, incluso antes de iniciar una partida. Cubren los **28 arquetipos**: seis unidades y ocho estructuras por facción. Cada ficha incluye retrato del modelo, rol, descripción, costos, tiempo de producción o construcción, vida, suministro y visión. Las unidades muestran velocidad; las estructuras, su huella. Cuando corresponde, se incluyen daño por golpe, alcance, intervalo de ataque, daño por segundo, energía y regeneración. La ficha también indica dónde se produce o quién construye, requisitos, habilidades y sus costos/recargas, o producción, desbloqueos y mejoras disponibles. Son **datos base del catálogo**, sin mejoras de la partida ni información del ejército rival.

Volver al inicio limpia selección, grupos, órdenes pendientes y estado de la partida, conservando facciones/mapa elegidos y la colocación física del tablero. Las pausas por seguimiento, foco o visor continúan vigentes hasta que el sistema las libere. Mientras hay un menú de partida abierto no se pueden dar órdenes al tablero.

**B** o **Escape** cancelan la acción actual, retroceden en los menús o abren la pausa. Durante la colocación, cancelan el ajuste y recuperan la posición previa del tablero. Si no había una posición confirmada, sigue siendo necesario colocar el tablero para empezar.

### Recolección y ocupación de depósitos

El reparto automático usa un objetivo de **2 trabajadores por depósito de mineral** y **3 por depósito de gas/catalizador con extractor terminado**. Al dar una orden a un grupo, cada trabajador reserva su puesto antes de asignar el siguiente. Si el depósito indicado ya está al nivel óptimo, buscan otro cercano del mismo recurso, explorado y accesible. Los trabajadores que viajan o entregan carga conservan su puesto.

Los minerales se reconocen por grupos de cristales azules facetados de distintas alturas sobre una base rocosa. El gas aparece como una abertura irregular con interior violeta y vapor ascendente; el extractor se construye encima. El vapor natural desaparece mientras el extractor ocupa el depósito y se detiene al pausar la partida.

Al colocar una **Refinería** o su equivalente biológico, la previsualización se ajusta al centro del gas observado al apuntar sobre su modelo o a menos de 2,5 celdas de su centro. Confirmar o encolar la construcción conserva exactamente esa ubicación. Al alejarse vuelve a seguir la grilla; sin gas o sobre un depósito ocupado, la previsualización es inválida. Los demás edificios conservan su colocación habitual.

Al apuntar para colocar **cualquier edificio**, el terreno visible muestra **verde donde cabe su huella completa y rojo donde está bloqueada**, con un borde que separa ambas zonas. La guía se concentra alrededor del edificio aún sin confirmar, sigue su posición y **se difumina gradualmente hasta desaparecer al alejarse**; el resto del mapa conserva sus colores. Se actualiza al cambiar de edificio o de estado del terreno observado. Considera minerales/gas, edificios y obras, enemigos visibles, terreno, desniveles, rampas y visión de toda la huella; las unidades propias pueden apartarse. Las zonas representan dónde puede estar el origen al que apunta el mando, y comparten la validación de la previsualización; para refinerías se tiene en cuenta el ajuste existente al gas. No atraen ni recolocan la base u otros edificios. La guía desaparece al sacar el puntero del mapa, cancelar, pausar, ocultar el tablero o finalizar la colocación/partida. Los costos, requisitos, acceso del constructor y posibilidad de evacuar aliados se siguen verificando al confirmar.

Si todos los depósitos cercanos están al nivel óptimo, los trabajadores adicionales siguen recolectando y el indicador muestra **excess**; estos valores son objetivos de reparto, no un límite rígido de asignación ni un cambio de la velocidad de extracción. Al terminar una entrega, los excedentes aprovechan los puestos que se hayan liberado. Los depósitos agotados y los extractores destruidos provocan una nueva búsqueda dentro del mismo grupo local de recursos; se conserva el tipo de cualquier carga pendiente de entrega. Cuando se agotan todos los minerales de esa zona, entregan la última carga y quedan inactivos allí. Ir a los minerales de otra zona requiere una orden explícita, aunque ya estén explorados. Los trabajadores recién producidos en una base agotada también esperan órdenes.

Las entregas usan el centro principal terminado más cercano al trabajador. Una expansión en construcción todavía no recibe recursos. Al quedar libre después de construir en otra zona, el constructor busca minerales locales y deja de priorizar el depósito lejano donde trabajaba antes.

**Ambas facciones requieren un trabajador construyendo durante toda la obra.** Si se retira, recibe otra orden o muere, el progreso se detiene; otro trabajador puede reanudarlo mediante la orden contextual sobre el edificio incompleto. Ninguna estructura se termina sola. Los trabajadores de ambas facciones también reparan unidades y edificios aliados gastando minerales. Ninguna facción regenera vida automáticamente.

Las estructuras en construcción se muestran **en gris**, con **UNDER CONSTRUCTION**, porcentaje y barra de progreso sobre el edificio, sin necesidad de seleccionarlo. Las bases también muestran **NO RESOURCE DELIVERY YET**. Al terminar recuperan sus colores normales y desaparece el aviso; los centros y extractores operativos pasan a mostrar sus contadores de trabajadores. Los indicadores no muestran progreso actualizado de edificios recordados bajo niebla.

Podés colocar una construcción sobre unidades propias: se apartan automáticamente, incluido el constructor y las unidades en **Hold**, y luego retoman sus órdenes y colas. La zona queda reservada y muestra **CLEARING AREA** hasta quedar libre; recién entonces empieza la obra. Se conservan las restricciones de terreno, recursos, edificios y enemigos. Si no existe una salida accesible, la orden se rechaza sin cobrar.

Apuntar o seleccionar un depósito muestra **Workers asignados/óptimo** y la cantidad restante en el panel. Seleccionar el centro principal muestra los totales de minerales y gas de su zona; seleccionar un extractor muestra su ocupación de gas. Se puede dar la orden de gas sobre toda la superficie del extractor. Los indicadores cuentan tus trabajadores, no los del rival.

Sobre cada centro principal terminado aparece **MINERAL WORKERS** con el total asignado a sus minerales: por ejemplo, **5/8** para cinco trabajadores y cuatro depósitos activos. Sobre cada extractor terminado aparece **GAS WORKERS**, por ejemplo **3/3**. No hace falta seleccionar el edificio. Al superar el óptimo, el número se vuelve rojo (**9/8**, **4/3**); vuelve a blanco al retirar el exceso. Los trabajadores en viaje o entregando carga siguen incluidos y la capacidad baja cuando se agota un depósito. Las etiquetas miran al jugador, conservan su tamaño físico en tableros pequeños y se separan si se superponen.

### Panel de partida

La franja superior separa **MINERALS**, **GAS** y **SUPPLY / CAPACITY**, con cantidades grandes y colores propios. Gas corresponde al catalizador del catálogo. La capacidad muestra usados/total y lugares disponibles; **FULL** aparece en naranja cuando está completa. Los costos de las acciones usan **M** para minerales y **G** para gas.

La ficha de selección muestra el nombre de la unidad o edificio, su rol, facción, vida actual/máxima y orden o estado. Las construcciones y colas muestran progreso; seleccionar varias entidades muestra su cantidad y composición por nombre. Sin selección, apuntar a una entidad visible muestra su ficha. Los depósitos seleccionados muestran el tipo y la cantidad restante conocida.

**Selección** resume por tipo únicamente cuando hay distintos tipos de unidades o edificios: cada tarjeta muestra silueta, nombre, cantidad y vida media. Si todos son del mismo tipo, muestra una tarjeta por individuo con su propia vida y energía cuando corresponde, sin repetir ×1. **Trigger / A** conserva el individuo o los miembros del tipo pulsado que ya estaban seleccionados, sin sumar aliados cercanos. **Grip + Trigger** los quita de la selección; siguen vivos y mantienen sus órdenes. En escritorio se usa clic izquierdo para seleccionar y **Shift + clic** para quitar. La acción se aplica al soltar sobre la misma tarjeta. El modificador se toma al comenzar la pulsación; salir de la tarjeta, pausar o cambiar la selección cancela el gesto. La sección conserva 136 px de contenido, sin paginación: hasta seis tarjetas grandes, dos filas compactas hasta catorce y retratos con vida individual para grupos mayores, hasta las 60 unidades del límite. El tamaño del menú permanece fijo.

**ACTIONS** contiene únicamente las órdenes de la selección: construcción para trabajadores, órdenes militares para unidades, habilidades cuando hay casters y producción o investigación para edificios compatibles. **Cancel / refund** aparece mientras hay una construcción o cola que cancelar. Los edificios automáticos y las selecciones incompatibles muestran una explicación.

Las acciones se agrupan con encabezados y colores: **BUILD STRUCTURES** en ámbar, **TRAIN UNITS** en verde, **RESEARCH** y **CASTER ABILITIES** en violeta, **UNIT ORDERS** en azul y **CANCEL / REFUND** en coral. Construcción, habilidades y órdenes de unidades ocupan filas separadas.

Las tarjetas de construcción y entrenamiento incluyen una miniatura del modelo real, su nombre y el costo en minerales/gas. La cola también muestra el modelo de cada unidad, tanto activa como pendiente, incluidas las colas de varios edificios. Las miniaturas cambian al avanzar o cancelar los pedidos; investigaciones y lugares vacíos conservan sus textos sin mostrar una unidad anterior.

Al elegir una estructura, su botón conserva un borde ámbar y **SELECTED** aunque se retire el puntero. **PLACING · nombre** indica si se está colocando una barraca, relay u otro edificio. El resaltado cambia al elegir otra estructura y se limpia al colocarla o cancelar, tanto desde el panel como desde la rueda.

Mantener el cursor sobre una acción durante **1,5 segundos** muestra su **FIELD GUIDE** en la tarjeta lateral: nombre, función del edificio o unidad, costo, tiempo y requisitos cuando corresponda. También explica órdenes, mejoras y cancelaciones. La ayuda reemplaza temporalmente la ficha de selección, sin tapar botones, recursos ni producción; desaparece al cambiar de acción, retirar el cursor, pulsar, cambiar la selección o salir del panel. Su texto sigue el idioma elegido en Configuración.

En los menús de Quest, **A funciona como el gatillo**: pulsar y soltar sobre un botón lo activa; soltar afuera cancela. Mantener A permite guardar grupos y arrastrar el menú. Si se usan ambos botones juntos, solo se ejecuta una acción. Las pulsaciones iniciadas en el menú se consumen allí, incluso si el cursor termina sobre el mapa.

Al seleccionar un edificio productor terminado, **PRODUCTION QUEUE** muestra una tarjeta grande para la unidad o investigación activa, su barra de progreso, porcentaje y segundos restantes. A su derecha aparecen los cuatro lugares siguientes, numerados en orden de salida y con nombre/rol. El contador incluye el elemento activo; **5 / 5 · FULL** indica que no entran más órdenes. **Queue empty** permanece visible antes de producir para que los botones no cambien de posición al agregar unidades. **Cancel / refund** elimina el último elemento y devuelve su costo.

Para cancelar un pedido concreto, apuntar directamente a su tarjeta en **PRODUCTION QUEUE**: se vuelve **roja** y muestra **Cancel + refund**. Un clic con **A**, **gatillo** o **mouse izquierdo** cancela solo ese elemento, **sin confirmación**, devuelve su costo completo y libera su reserva de suministro. También se puede cancelar la producción activa, una investigación o una unidad lista que espera salida. Los demás pedidos conservan su orden; si se cancela el activo, empieza el siguiente. Los lugares vacíos no se pueden cancelar. Si la cola avanza durante la pulsación, nunca se aplica la cancelación a una unidad distinta.

Al seleccionar individualmente un edificio en construcción, **Opciones** y la sección **Selección** de la rueda muestran **Cancelar construcción**, con el reembolso del **75 %** de sus costos de minerales y gas. Su tooltip explica el efecto. El área de **Producción** permanece vacía hasta que el edificio esté terminado. Terminar o cancelar la construcción conserva el tamaño del menú; si termina mientras se mantiene el botón o llega la orden, esa cancelación no puede afectar una producción posterior.

Para una selección sin unidades, **Opciones** ofrece **Set Rally Point / Set Rally Points** si ningún productor elegible tiene destino: al pulsarlo se apunta al terreno y se confirma con gatillo. Si alguno ya tiene destino, ese mismo espacio fijo de la columna derecha muestra **Clear Rally Point / Clear Rally Points**, que quita los destinos del grupo sin cancelar producción. Solo se aplican a productores completos; los demás edificios seleccionados se ignoran. Ninguna de las dos acciones aparece en la rueda. **A sobre el mapa** sigue colocando o reemplazando directamente el destino, sin pasar por el menú.

Una producción terminada que aún no puede salir muestra **WAITING TO DEPLOY**, con **Needs more supply** si faltan lugares para las unidades existentes o **Clear the building exit** si la salida está bloqueada. Las pausas por seguimiento/visor muestran **PAUSED** y conservan el progreso. La sección también distingue investigaciones de unidades y se oculta al seleccionar unidades o edificios terminados que no pueden producir.

Al seleccionar varias barracas u otros edificios, el panel muestra sus acciones compatibles. La rueda ofrece entrenamiento; las mejoras se investigan en el panel del edificio de mejoras. **Cada pulsación encarga una sola unidad o mejora**, usando el productor terminado con menos tiempo pendiente de trabajo; en empate, la cola más corta y luego el ID. Si un edificio tiene una unidad terminada bloqueando su cola porque no puede salir, se prefiere otro productor que pueda avanzar. Los edificios incompatibles, incompletos o con cinco pedidos no reciben el encargo. Se mantienen los mismos costos, requisitos y reservas de suministro. El panel llena sus **cinco tarjetas con pedidos reales**: primero los que se producen en paralelo y después los pendientes, intercalando los edificios y conservando el orden de cada cola. Cada tarjeta indica su edificio (**B1**, **B2**, etc.); los pedidos activos muestran su progreso. Si hay más de cinco, la última conserva el quinto pedido y añade **+N MORE** con la cantidad adicional. Para cancelar un pedido hay que seleccionar su edificio individualmente.

**CONTROL GROUPS** tiene su propia franja: cada grupo muestra **Empty** o su cantidad asignada. Pulsar recupera el grupo; mantener guarda la selección. **MATCH** contiene el reloj y **Pause**, separados de las acciones de unidades y edificios. **Full map** aparece únicamente en el modo de prueba de escritorio. La ocupación de depósitos y el aviso **excess** permanecen en otra franja, sin reemplazar los recursos globales.

### Edificio de mejoras

**Centro de mejoras** (Upgrade Center, industrial) y **Cámara evolutiva** (Evolution Chamber, biológico) concentran **Firepower I–III** y **Armor I–III**. Los construye un trabajador después de terminar el Cuartel / Nido: **150 minerales + 50 gas, 30 segundos, 3 × 3 celdas, 650 de vida**. Ambas facciones tienen las mismas estadísticas.

Cada rama avanza de forma independiente: completar Firepower I habilita II y luego III, sin exigir ningún nivel de Armor, y viceversa. Los costos de cada mejora son **150 minerales / 100 gas** en I, **225 / 150** en II y **300 / 200** en III; cada nivel tarda **35 segundos**. Cada nivel de Firepower añade un 20 % del daño base, y cada nivel de Armor resta 2 al daño recibido (con un mínimo de 1), para toda la facción. No se pueden saltar niveles ni investigar dos niveles de la misma rama a la vez.

Se investigan desde **Opciones** del edificio; la rueda continúa sin mejoras. Al terminar una investigación, su botón se reemplaza por el siguiente nivel en el mismo lugar, incluso si se completó en otro edificio. La cola de Producción muestra el nivel activo y pendiente, permite cancelar cualquiera con reembolso completo de su costo y funciona también al agrupar edificios. Este edificio no entrena unidades ni admite punto de reunión. Arsenal / Crisálida conservan la artillería y el requisito de la academia, sin investigación.

El nuevo botón de construcción ocupa el octavo espacio, sin desplazar los siete anteriores del HUD. La IA de todas las dificultades construye el edificio con recursos reales y accede a los mismos tres niveles por rama. Los informes antiguos siguen disponibles; sus replays requieren reglas anteriores (`ReplayRulesVersion=4`).

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

**Attack Move** permite manejar el ejército con una orden de grupo: avanza hasta el destino, combate y retoma el avance cuando termina el enfrentamiento. La selección automática aprovecha primero los enemigos que ya puede atacar. Entre ellos prioriza soldados y torres terminadas, después trabajadores y otras unidades, y por último edificios sin ataque. Los trabajadores que disparan pasan a ser amenazas. Si un defensor entra en alcance mientras tus unidades atacan automáticamente un edificio, cambian al defensor.

Al elegir **Attack Move** desde el panel o la rueda, la mira del tablero y el rayo del mando se ponen **rojos** mientras elegís el destino. La orden se confirma con una pulsación separada de A o del gatillo sobre el mapa; B cancela. Un destino rechazado conserva el modo rojo para reintentar. Al confirmar, el cursor vuelve a su color normal y queda un pulso rojo en el punto ordenado. **Doble A** apuntando al terreno da la orden directamente, sin gatillo adicional. Las unidades combaten los enemigos y estructuras que encuentran, adquieren otro objetivo al eliminar el anterior y retoman la marcha cuando despejan el camino.

Mientras pueden disparar conservan un objetivo válido para no cambiar de enemigo a cada instante. Al tener que acercarse, consideran la distancia y cuántos aliados de alcance similar ya van hacia cada enemigo: pueden dejar de perseguir a uno que se aleja para entrar en combate con otro más cercano. Buscan puestos separados, con espacio para su cuerpo y línea de tiro, y reemplazan un puesto si se ocupa. Esto evita amontonarse para atacar al mismo enemigo; varias unidades pueden seguir concentrando fuego cuando ya están en posición. Descartan temporalmente objetivos inaccesibles, sin borrar el destino de Attack Move. Ceder el paso a un aliado no impide disparar a un enemigo al alcance.

Una **orden directa sobre un enemigo** mantiene el foco que elegiste, incluso si es un edificio. Si muere, continúa la próxima orden encolada; si no hay ninguna, adquiere otro enemigo cercano. Las unidades que todavía están lejos continúan hacia la última zona observada en Attack Move: una baja lograda por la primera línea no deja detenida a la retaguardia. Al perder visión, busca el foco durante un máximo de tres segundos, sin seguir posiciones ocultas; después puede completar el avance hacia esa zona conocida. Si el objetivo visible queda inaccesible, puede atacar otros enemigos mientras reintenta el foco indicado.

Al confirmar **Attack Move desde el menú**, un segundo A rápido sobre el mismo lugar conserva la orden, sin convertirla en Move ni duplicar un destino encolado. Doble A también tolera que muera el objetivo entre toques, que una baja reduzca la selección o que el puntero pase del terreno a una unidad cercana. Una orden rechazada conserva el modo de apuntado para poder elegir otro destino.

**Move** sirve para retirarse o reposicionarse y no se interrumpe para atacar. **Stop** cancela órdenes y deja que los soldados combatan automáticamente; **Hold Position** dispara sin perseguir ni ceder su posición. Ningún cambio de objetivo reinicia el tiempo de recarga. Estas reglas son iguales para el jugador y la IA.

Las unidades se distribuyen al llegar a un destino, rodean el tráfico y ceden espacio a aliados que necesitan pasar. Los empujes conservan la orden anterior; **Hold Position** impide que otros aliados desplacen esa unidad. Las construcciones y la producción respetan el tamaño de los cuerpos.

Las unidades pesadas tienen prioridad frente a las livianas. MOVE responde desde el siguiente paso de simulación y empieza a orientar el modelo inmediatamente, conservando inercia: cada tipo acelera, frena y cambia de dirección a su ritmo. **Stop** frena; **Hold Position** ancla la unidad. Una nueva orden reemplaza cualquier maniobra automática para ceder el paso.

En Quest, el tablero queda fijo al confirmar su colocación: los joysticks ya no desplazan ni amplían el mapa, y pulsar el stick principal tampoco cambia su vista. Para cambiar posición, tamaño u orientación, abrir **Settings → Play space → Reposition board** y confirmar una nueva colocación. Durante ese ajuste, el joystick derecho modifica tamaño/giro; sostener el grip derecho cambia el eje vertical a altura, sin alterar tamaño ni giro. El modo de prueba de escritorio conserva sus controles de navegación para desarrollo.

Antes de iniciar una partida en Quest, el menú queda fijo frente a la posición inicial del visor; Settings, Hotkeys, Guides y la preparación conservan esa ubicación, incluso durante la colocación del tablero. No sigue los movimientos de la cabeza. **MOVE MENU** permite arrastrarlo cerca de vos y **Reset position** vuelve a situarlo enfrente. Estos ajustes de inicio no sobrescriben la ubicación guardada de la consola de partida.

Durante una partida, la consola comienza elevada detrás del tablero o recupera su desplazamiento guardado. Para moverla, apuntar a **MOVE MENU**, mantener el gatillo o A y arrastrar; mientras se sostiene, el stick principal acerca o aleja el panel. Al soltar, queda fija respecto del tablero y guarda esa posición para la próxima partida. El panel gira para poder leerse al caminar alrededor. **Reset position** lo devuelve arriba del mapa; la tecla **M** también lo recupera en escritorio. Inclinar el joystick abre la rueda unificada únicamente durante la interacción libre con el mapa; mientras se arrastra el menú mantiene su ajuste de distancia.

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
| Mostrar / ocultar todos los hints | Clic del stick fuera de Groups | — |
| Abrir rueda unificada | Inclinar el joystick desde el centro | — |
| Elegir categoría | Arriba: Groups; derecha: Selection; izquierda: Orders | — |
| Entrar / ejecutar en la rueda | Gatillo del mando activo | — |
| Volver / cerrar la rueda | B/Y vuelve y cierra desde la raíz; abajo + gatillo vuelve desde acciones | — |
| Cerrar por inactividad | Dejar el joystick centrado durante 0,4 s | — |
| Recuperar grupo en la rueda | Entrar a Groups, inclinar hacia un grupo y gatillar | — |
| Reemplazar un grupo con la selección | Arriba → elegir grupo → pulsar stick | — |
| Cancelar / opciones | B | Escape |
| Desplazar mapa (solo escritorio) | — | WASD |
| Zoom (solo escritorio) | — | Rueda |
| Ver mapa completo (solo escritorio) | — | F |
| Mover menú | Mantener gatillo o A sobre MOVE MENU; stick principal para distancia | Arrastrar MOVE MENU; rueda para distancia |
| Recuperar menú arriba del tablero | Reset position en el panel | M |
| Pausa | B o panel | Espacio |
| Grupo de control | Cuatro botones del panel; mantener para guardar | 1–4; Ctrl+1–4 para guardar |

El juego incluye selección ampliada, contorno de selección al ras del terreno, previsualización de construcción, panel contextual, avisos, sonidos de confirmación y vibración. El rectángulo sigue el suelo y las rampas sin flotar ni dibujar paredes en los desniveles. Todos los controles del juego usan el único mando elegido en **Settings → Controls → Hand: Left/Right**. El otro no controla el juego ni participa en la captura de hotkeys. La elección se guarda junto con las asignaciones; cambiar de mano conserva las funciones configuradas y actualiza sus nombres. Al cambiar de mano se descartan los gestos pendientes y se espera que el nuevo mando quede libre y centrado antes de aceptar entrada.

El **grip derecho** funciona como Shift: mantenerlo mientras se selecciona con el gatillo principal suma unidades a la selección actual, también al arrastrar un área. Al pulsar una unidad ya seleccionada con el grip sostenido, se quita de la selección. Sin el grip, una nueva selección reemplaza la anterior. El modificador está en el mando derecho, junto con el gatillo de selección.

Mantener **grip + gatillo durante 0,5 segundos** sobre una unidad ya seleccionada la quita una sola vez; soltar no vuelve a seleccionarla. Si el puntero se arrastra, el gesto sigue siendo selección de área. **Doble gatillo** sobre una unidad o edificio selecciona los propios de esa misma definición dentro de doce celdas del objetivo; con grip conserva el resto de la selección. En escritorio funciona con doble clic izquierdo y Shift. Por ejemplo, doble clic en una barraca selecciona las barracas cercanas, incluidas las que están en construcción, sin sumar otras bases lejanas ni edificios enemigos.

### Rueda unificada

El hint muestra **tres textos al costado del mando**, unidos al stick por líneas finas, sin rueda pequeña ni fondo. Cada texto lleva una flecha de dirección. Del otro lado aparecen gatillo, A/X, B/Y y grip con líneas hacia sus botones; el clic de stick se indica debajo de las direcciones. Las etiquetas reflejan la mano activa y los hotkeys configurados. La columna queda a la derecha del mando derecho y a la izquierda del izquierdo. Al abrir, aparece la rueda completa **unos 2,5 cm sobre el stick**, con una inclinación de hasta 35° hacia los ojos para facilitar la lectura. Su posición sigue anclada a la mano. El mando activo muestra un modelo **Touch Plus** con botones y stick animados; el láser nace de su emisor. Se oculta al perder seguimiento o foco y no aparece en desktop.

El mando activo da un pulso suave al cambiar de opción y uno más marcado al abrir la rueda o cambiar de nivel, incluido retroceder. Mantener la misma opción o centrar el stick no repite la vibración.

Al confirmar con gatillo, el sector da un pulso verde claro y aparece una marca de confirmación durante **0,35 s**. Si la orden cierra la rueda, queda brevemente la marca junto al nombre de la acción, siguiendo el mando. Cada nueva confirmación renueva el pulso; mantener el gatillo no lo repite y las órdenes rechazadas no muestran éxito.

Durante el juego aparecen los **hints laterales del mando activo**, incluso con la rueda cerrada: **↑ Groups**, **→ Selection**, **← Unit orders**, **↓ Quick select**. Es el nivel 0. Al inclinar el joystick desde el centro se abre **directamente la sección indicada**, con la opción de esa dirección ya resaltada. **No hace falta centrar entre niveles**: mantener o desplazar el stick elige la opción, y **una nueva pulsación del gatillo confirma cada paso**. Mantener una dirección conserva la rueda abierta. Centrar el joystick durante **0,4 s sin acción** la cierra. El hint de juego se oculta al pausar o perder el seguimiento; durante la colocación lo reemplazan los hints de ajuste del tablero.

**Groups** permite recuperar con gatillo y asignar con clic del stick; **Selection** reúne las acciones de la selección, como Build, Train, Research y habilidades de casters; **Unit orders** ofrece Attack Move, Stop y Hold Position. Con uno o varios trabajadores seleccionados, **Selection muestra directamente los siete edificios**, sin un paso intermedio Build/Back. Inclinar a la derecha ya resalta una opción; el primer gatillazo elige el edificio y después se apunta su ubicación en el mapa. **Abajo + gatillo** retrocede desde Selection y Unit orders. En la raíz abajo entra a Quick select; dentro de Groups corresponde al grupo 3. **B/Y** retrocede y cierra desde la raíz. Entrar o retroceder conserva la dirección del stick; mantener el gatillo no confirma otra opción hasta soltarlo y pulsarlo de nuevo. La rueda muestra el nombre de la selección, la acción y su costo o efecto; las órdenes rechazadas indican el motivo.

La navegación usa siempre el gatillo físico y B/Y del mando activo. A/X y grip no ejecutan opciones ni cambian de nivel; sus funciones del mapa se conservan al cerrar. Fuera de Groups, pulsar el joystick alterna todos los hints; el doble grip no abre la rueda. **Selection permanece abierta después de confirmar**: cada nueva pulsación de Trigger ejecuta otra orden, sin tener que soltar el stick ni reabrirla. Se puede repetir la opción o cambiar a otra; sostener Trigger no repite. Al soltar el stick se cierra tras 0,4 s. Las construcciones y habilidades esperan entonces un destino nuevo en el mapa. Attack Move, en Unit orders, cierra su rueda al confirmar. **Cancel / refund solo está en el tablero principal**: ya no aparece ni desplaza las opciones cuando cambia la cola de producción. Mientras está abierta consume la entrada para que la misma pulsación no seleccione ni ordene sobre el tablero. Cambiar la selección, perder una unidad seleccionada, pausar, perder foco/seguimiento o terminar la partida cierra la rueda. La consola permanece disponible para recursos, colas y las mismas acciones.

**Quick select** se abre con el stick hacia abajo, ya apuntando a **Idle workers**: gatillo selecciona todos los trabajadores propios inactivos del mapa. Se incluyen Stop/Hold sin tareas pendientes; se excluyen los que caminan, recolectan, entregan, construyen o reparan. Desde esa rueda, mover el stick arriba elige **All army**: gatillo selecciona todas las unidades militares propias, incluidos scouts y casters, sin trabajadores ni edificios. Cada opción muestra su cantidad actual y se vuelve a comprobar al confirmar. Reemplaza la selección y cancela el modo de apuntado pendiente, pero conserva las órdenes de las unidades. Si no hay candidatos, conserva la selección anterior y muestra el motivo. Una selección aceptada cierra la rueda con confirmación visual; B/Y vuelve y centrar durante 0,4 s la oculta.

### Rueda de grupos

La rueda unificada se abre inclinando el joystick desde el centro. **Arriba** entra directamente a **Groups** con el **grupo 1 ya resaltado**; basta gatillar para recuperarlo. Se puede girar directamente hacia otro grupo sin centrar: **1 arriba, 2 a la derecha, 3 abajo y 4 a la izquierda**. Inclinar solo resalta; **gatillar recupera**. Volver al centro no selecciona nada; tras **0,4 s** sin acción la rueda se cierra. **B** vuelve al nivel principal conservando la dirección.

Para asignar, seleccionar unidades o edificios e inclinar **arriba para abrir Groups**. Elegir cualquiera de las cuatro direcciones y **pulsar el stick** reemplaza los miembros del grupo con la selección actual inmediatamente. No hace falta mantener ni soltar el botón. El gatillo sigue recuperando el grupo. B/Y cancela; centrar durante 0,4 s cierra sin asignar. Asignar una selección vacía limpia el grupo elegido.

**Fuera de Groups, pulsar el stick muestra u oculta todos los hints**. Dentro de Groups asigna y conserva la visibilidad de la ayuda. La rueda sigue abriendo por inclinación aunque la ayuda esté oculta. **Abajo desde el hint ya no abre ninguna rueda**; los accesos son arriba Groups, derecha Selection e izquierda Unit orders.

Los contadores excluyen unidades y edificios destruidos. Se pueden guardar unidades y edificios propios, incluidos grupos de varias barracas; recuperar un grupo conserva sus colas y puntos de reunión. Los grupos se reinician al comenzar otra partida. Los cuatro botones del panel conservan clic para recuperar y pulsación larga para guardar. El modificador de guardado de la rueda se puede reasignar desde Hotkeys; las direcciones y el gatillo de navegación son fijos.

Durante colocación o arrastre del menú, el eje conserva su función de ajuste. Tras esas operaciones, pausa, pérdida de foco o cierre de rueda, hay que volver al centro antes de abrirla otra vez. El grip mantiene la selección aditiva y las colas; su doble pulsación no abre menús.

### Punto de reunión de edificios

Seleccionar un centro principal, barraca u otro productor terminado y pulsar **A sobre el terreno** establece su **Rally Point** (clic derecho en escritorio). También funciona con varios productores propios seleccionados y otros edificios que no producen: solo se modifican los productores completos. Si hay unidades en la selección, A da órdenes a las unidades y no cambia ningún rally. Cada edificio conserva un único destino; volver a pulsar A lo cambia, incluso con grip o doble A. El indicador cian con bandera y línea punteada conecta el edificio con ese punto.

Las unidades nuevas reciben una orden Move al desplegarse, usando el destino vigente en ese momento y las reglas normales de separación y movimiento. Cambiar el punto no altera unidades que ya salieron ni la cola de producción. Los trabajadores también van al punto elegido; este ajuste es un destino de movimiento, no una orden automática de recolección.

**Set Rally Point** inicia el apuntado desde Opciones; gatillo confirma y B cancela. Cambiar la selección cancela ese apuntado. **Clear Rally Point**, disponible solo en Opciones, borra los destinos asignados sin cancelar producción. Así se recupera el comportamiento predeterminado: los trabajadores buscan recursos y las unidades militares quedan junto a la salida. Los destinos fuera del mapa, bloqueados o sin camino según la información conocida se rechazan conservando el punto anterior. Si el terreno cambia después y el punto queda inaccesible, la unidad sale en un espacio seguro; se muestra un aviso cuando el bloqueo también es conocido. Si la propia salida está ocupada, la producción espera como antes.

### Cola de acciones de unidades

**Grip + A** agrega una orden al final del plan de cada unidad seleccionada. Si está quieta, empieza de inmediato; si está ocupada, conserva su trabajo actual. Repetir A en otro lugar agrega otro destino. Una orden válida sin grip reemplaza el plan; **Stop** y **Hold Position** lo limpian inmediatamente.

**Doble A** hace Attack Move sobre terreno o ataque directo sobre un enemigo visible, inmediatamente en el punto del cursor y sin confirmar con gatillo. Con grip también se encola. El primer A responde inmediatamente; el segundo convierte esa misma orden en ataque, sin dejar un Move extra. Las pulsaciones admiten desde toques muy rápidos hasta un máximo de **0,55 segundos**, sobre el mismo objetivo o puntos cercanos. En Quest se tolera un desplazamiento de **3,5 cm sobre el tablero**, conservando como mínimo las 1,5 celdas anteriores; así alejar el mapa no vuelve más difícil el gesto. Pasar entre unidades amigas, o entre ellas y terreno cercano, no rompe el doble toque. Dos destinos fuera de ese margen siguen siendo dos órdenes. **Cada ataque requiere dos toques**: el tercero comienza otro gesto con Move y el cuarto lo convierte en ataque al nuevo destino; no queda un modo de ataque persistente. Cambiar selección, soltar grip, entrar al menú/rueda o pausar descarta el doble clic pendiente.

Cada orden aceptada de movimiento o ataque deja un **pulso animado de 1,2 segundos en el punto ordenado**: cian para Move y rojo para Attack/Attack Move. Permanece allí aunque muevas el cursor o apuntes fuera del tablero; al desaparecer, las unidades siguen ejecutando la orden. Una nueva orden reemplaza esa confirmación. El cursor y el rayo rojos se reservan para elegir un destino pendiente desde el panel o la rueda.

Cada unidad seleccionada muestra una **línea punteada hacia su destino activo**, aunque tenga una sola orden: **cian para Move**, **rojo para Attack y Attack Move** y **verde para trabajo** (recolección, construcción y reparación). Las líneas siguen el terreno y las rampas y desaparecen al completar o detener la acción, o al deseleccionar la unidad. Sus destinos pueden estar separados para conservar la formación.

La sección **UNIT ORDER PLAN** distingue las acciones pendientes de la producción de edificios. Muestra cuántas quedan y los próximos pasos. Para una unidad representativa identificada en el panel, el mapa añade hasta ocho destinos numerados y sus enlaces, con el número 1 para la acción actual. Las líneas conectan objetivos en el orden de ejecución; la ruta real se adapta a obstáculos y unidades.

Cada unidad admite hasta **32 órdenes pendientes**. Move/Attack Move avanzan al llegar; Attack al desaparecer el objetivo o perderlo de vista; Gather después de entregar una carga; Repair al terminar la reparación. En ambas facciones, Build mantiene al trabajador en la obra y avanza a la siguiente orden solo al terminar el edificio. Las órdenes se revalidan al comenzar y se omiten con aviso si ya no se pueden ejecutar. Las construcciones encoladas se pagan al activarse, no al agregarlas. Detalles técnicos y pruebas: [colas de acciones](order-queues.md).

La selección por área puede **empezar o terminar fuera del tablero**: mantener el gatillo (o clic izquierdo), arrastrar sobre las unidades y edificios propios y soltar, incluso fuera del borde opuesto. El rectángulo y la selección se recortan a la parte visible del mapa; con el zoom de escritorio no se seleccionan entidades cuyo centro quede fuera del tablero. Se conserva el modificador para agregar unidades y edificios. Un clic o arrastre completamente externo no cambia la selección; apuntar al menú o perder una proyección válida cancela el arrastre. Las órdenes contextuales y la confirmación de construcción siguen requiriendo apuntar al terreno.

Los **minerales** se pueden apuntar directamente sobre el cristal, incluso mirando el tablero de costado, o cerca de su base gracias a una zona de interacción más amplia. El depósito bajo el cursor se ilumina y recibe un aro sobre el terreno antes de pulsar. El aro y el panel usan el mismo objetivo que recibirá la orden de recolección. Los trabajadores cercanos no ocultan el mineral con su margen de selección; apuntar directamente al cuerpo del trabajador sigue seleccionándolo. Una pulsación con un pequeño movimiento de la mano conserva el depósito elegido al presionar; arrastrar más lejos mantiene la selección por área. La asignación automática a depósitos disponibles sigue funcionando igual.

Al apuntar al mapa aparece un **punto blanco con líneas de mira turquesa** en la intersección del láser con el terreno. Está al ras del suelo y las rampas, mantiene su tamaño con el zoom de escritorio y se recorta en el borde del tablero. También funciona con el mouse. Sigue visible sobre niebla como referencia del cursor, sin revelar información; desaparece al salir del tablero, apuntar al panel o pausar.

La **niebla de guerra** tiene tres capas de bruma amplia, con movimiento lento y un tono oscuro acorde al mapa. Al 100 % forma una nube mucho más densa y oscura; la opacidad de cada capa disminuye proporcionalmente al ajuste de densidad hasta desaparecer al 0 %. Los valores intermedios permiten recuperar progresivamente la lectura del relieve y los detalles del suelo. Es más densa en zonas nunca exploradas y más tenue donde ya hubo visión; las celdas visibles quedan completamente despejadas. Los edificios recordados conservan su última información conocida, mientras las unidades enemigas fuera de visión permanecen ocultas. El oscurecimiento del terreno y la decoración fuera de visión disminuye a medida que aumenta la densidad de bruma, sin desaparecer por completo. Con densidad 0 %, conserva la intensidad anterior: 42 % de oscurecimiento en zonas nunca exploradas y 22 % en exploradas; con densidad 100 %, baja a 10 % y 5 %, respectivamente, con transición proporcional entre ambos extremos. Así la bruma y la capa oscura no ocultan juntas el relieve. Las zonas con visión actual mantienen su iluminación normal. Este ajuste no revela unidades enemigas ni depósitos todavía ocultos. La bruma no cambia las reglas de exploración y se detiene durante la pausa.

Las unidades enemigas aparecen con un **fade-in suave de 0,3 segundos** al entrar o volver a entrar en visión. Cuerpo, armas, patas, barra de vida y sombra de contacto comparten esa aparición. Al salir de visión desaparecen inmediatamente. Las unidades propias y los replays conservan la presentación inmediata; la pausa local detiene una transición en curso. No cambia la selección ni las reglas de visión.

**Configuración → Visuales → Iluminación** permite alternar al instante entre **Actual** (predeterminada) y **Mejorada (experimental)**, antes de la partida o desde pausa. La mejorada combina luz suave, relleno ambiental, sombras de contacto bajo objetos y reflejos moderados en unidades, edificios y recursos. El suelo mantiene un acabado mate y cada mapa conserva su tono. La elección se guarda en el dispositivo; cambiarla conserva la partida, la selección y el ajuste de niebla. Las sombras de unidades y recursos solo aparecen en zonas con visión actual.

Al apuntar a una unidad o edificio, el modelo se ilumina y aparece un contorno sobre el suelo: circular para unidades y cuadrado para edificios, amarillo en aliados y rojo en enemigos. Se muestra antes de seleccionar y se apaga al retirar el puntero, apuntar al menú o perder el foco. El disco de selección permanece en los objetos seleccionados. El contorno de hover sigue el terreno y las rampas, respeta el borde del tablero y no aparece sobre recuerdos enemigos bajo niebla de guerra.

Al **recibir daño**, unidades y edificios visibles muestran un destello claro en el modelo durante unos 0,2 segundos y un aro naranja que se expande sobre ellos durante unos 0,6 segundos. La barra de vida se vuelve naranja durante ese intervalo. Varios impactos renuevan la señal sin acumular aros; también se indica el daño de área. El efecto no necesita selección y es independiente del destello del atacante. Los colores de equipo y rol vuelven al finalizar el pulso; los recuerdos enemigos bajo niebla no muestran impactos.

Al **morir**, las unidades caen al suelo en aproximadamente medio segundo desde su posición y orientación visibles. Los soldados industriales se vencen hacia atrás apoyándose en la parte posterior; los vehículos se asientan sobre el chasis y desprenden pequeñas chispas y fragmentos. Las criaturas biológicas bajan el cuerpo y pliegan las patas. Los tres perfiles ajustan su apoyo al suelo y las rampas. El cuerpo queda visible hasta los 3,5 segundos y después se desvanece gradualmente, desapareciendo a los 6 segundos. Los restos no bloquean movimiento, selección ni ataques y solo se muestran dentro de la visión actual. Una unidad que simplemente entra en la niebla no deja un cadáver. El tiempo del efecto se detiene durante pausa.

Al **disparar**, las unidades se orientan hacia el objetivo real y aparece un destello en el arma, un proyectil con estela y chispas al llegar. Las armas industriales usan trazadoras doradas; las biológicas, proyectiles verdes más gruesos; la artillería dibuja un arco elevado y un impacto más amplio. Trabajadores y combatientes cercanos muestran un golpe corto. Las torres también muestran sus disparos. Una estela tenue conserva brevemente la conexión entre atacante y destino, incluso si el golpe destruyó al objetivo. Son efectos visuales: no cambian daño, cadencia ni tiempo de resolución del combate. Se recortan al tablero y a la visión actual, y se detienen durante la pausa.

La interfaz está disponible en inglés y español: menús, ayuda, controles, avisos, tutorial, nombres visibles del contenido, historial y espectador. El idioma se elige en **Configuración → Idioma**; por defecto sigue al sistema. Los catálogos admiten idiomas adicionales sin cambiar la simulación ni las partidas guardadas; ver [idiomas](localization.md).

## Contenido

- **The Consortium**: identidad industrial.
- **Symbiosis**: identidad orgánica. Su biomasa y animaciones son visuales, sin ventajas de juego.
- Ambas facciones tienen exactamente los mismos costos, tiempos, vida, movimiento, combate, suministro, visión y efectos de habilidades por rol. Comparten construcción atendida y reparación con trabajadores; no tienen regeneración pasiva de vida. Las diferencias de reglas quedan para una etapa de diseño y balance posterior.
- Cada facción: trabajador, explorador, combate cercano, tirador, asedio y caster; siete edificios; dos mejoras y dos habilidades de caster.
- Hasta 60 unidades móviles y 20 edificios por jugador.
- **Forest Watch**, bosque simétrico de 128 × 128, con dos bases, cuatro expansiones, alturas y rampas.
- **Neon Loop**, mapa futurista de 48 × 48 con cuatro zonas de recursos e instrucciones contextuales.
- **Desser Alert**, desierto simétrico de 160 × 160, con ocho zonas de seis minerales y dos gases, cinco mesetas y veinte rampas anchas.
- IA **Easy**, **Normal** y **Hard**; admite los cuatro cruces de facciones. No usa un modelo de lenguaje ni acceso a internet.

Las unidades tienen siluetas y símbolos de rol compartidos por ambas facciones: **herramienta amarilla** para trabajadores, **flechas verdes** para exploradores, **escudo crema** para combate cercano, **mira blanca** para tiradores, **tres proyectiles violetas** para artillería y **estrella violeta** para casters. Los trabajadores llevan carga y pinzas; la infantería cercana lleva escudo, los tiradores un arma larga y la artillería dos cañones o espinas. Los casters tienen una silueta erguida con corona abierta y un orbe. El casco y el borde del símbolo conservan el azul aliado y el rojo enemigo.

El **Hammer**, tanque producido en el **Arsenal**, y el **Bombardier**, artillería biológica de **Chrysalis**, tienen modelos un **80 % mayores** para destacar frente a la infantería. Armas, extremidades y restos mantienen esa proporción; sus estadísticas y colisiones conservan los valores del catálogo.

El panel muestra el rol junto al nombre al apuntar sin selección, seleccionar o producir. Las selecciones mixtas muestran cantidades por nombre de unidad o edificio. Las barras de vida sobre las unidades aparecen al seleccionar, apuntar o recibir daño, para despejar la vista del ejército.

Los edificios también se identifican por **silueta, altura y símbolo**, además del nombre al apuntar o seleccionar. El casco conserva el azul aliado o rojo enemigo; los acentos indican el rol y son compartidos entre facciones:

| Rol | Industrial / biológico | Rasgo reconocible | Símbolo |
|---|---|---|---|
| Base central | Command Core / Heart | Ciudadela escalonada o núcleo con corona de órganos | Corona dorada |
| Barraca | Barracks / Brood Nest | Hangar bajo con entrada ancha o nido con tres huevos | Dos chevrones naranjas |
| Gas | Refinery / Siphon | Dos chimeneas o conductos abiertos | Gota violeta |
| Suministro | Relay / Node | Depósitos bajos o sacos conectados | Signo más verde |
| Tecnología | Arsenal / Chrysalis | Reactor vertical o capullo rodeado de anillos | Rombo celeste |
| Mejoras | Upgrade Center / Evolution Chamber | Laboratorio de dos cámaras o dos órganos neurales | Flecha ascendente ámbar |
| Academia | Resonance Lab / Spore Sanctum | Tres pilares alrededor de una cámara suspendida | Estrella violeta |
| Defensa | Sentinel / Stinger | Torre estrecha con cañón o aguijón | Mira clara |

Los símbolos forman parte del modelo y se leen desde arriba, sin añadir carteles flotantes a la partida. Las torres se orientan hacia donde disparan. Los recuerdos bajo niebla conservan la silueta y el símbolo en tonos apagados, sin colores activos ni señales de impacto.

## Sonido

La música de fondo es una composición electrónica instrumental suave, con un bucle estéreo de 40 segundos. Baja durante la pausa y acompaña también los menús. **Settings → Audio → Music / Effects** ajusta ambos volúmenes por separado, incluido silencio; se guardan en el dispositivo.

Hay sonidos distintos para selección, movimiento, ataque, rechazo, navegación y confirmación de rueda/menú, comienzo y resultado de partida, unidades/construcciones listas e investigación terminada. El combate usa efectos de armas industriales, orgánicas, cuerpo a cuerpo, artillería y habilidades, situados en el tablero. Solo se oyen eventos disponibles en la visión del jugador; las batallas grandes limitan las voces para evitar saturación. Quitar el visor o perder foco silencia la reproducción; pausar detiene los efectos de combate sin repetirlos al volver.
