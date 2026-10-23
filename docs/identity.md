# Little Legion: identidad y dirección de producto

Decisión del 21 de octubre de 2026.

## Nombre y lema

El nombre del juego es **Little Legion**. Sustituye al nombre provisional Frontier Command.

**Pequeños ejércitos. Grandes batallas.**

La versión inglesa del lema es **Little armies. Big battles.** El nombre permanece igual en ambos idiomas; el menú muestra el lema en el idioma elegido.

La identidad combina ejércitos en miniatura, estrategia y personajes con personalidad. El tablero sobre una mesa o el piso permite que culturas y criaturas muy distintas compartan el mismo campo de batalla.

## Juego gratis y skins

La dirección acordada es ofrecer el juego gratis y monetizar mediante la venta de skins. Las compras son cosméticas: no modifican estadísticas, recursos, tiempos de producción, habilidades ni ventajas competitivas.

Una colección puede transformar la apariencia de un ejército completo: unidades, trabajadores, edificios y efectos visuales coherentes con su temática. Ese alcance es conceptual y se concretará al diseñar cada colección.

Los roles deben seguir siendo reconocibles con cualquier skin. El tamaño jugable, las señales de equipo, las acciones y la información de combate deben mantenerse claros aunque cambien los modelos y materiales.

Todavía no se definen precios, paquetes de venta, catálogo inicial gratuito ni flujo de compra. Tampoco se implementan tienda, pagos, inventario cosmético o desbloqueos en esta etapa.

## Primeras razas: conceptos para desarrollar después

Aquí «raza» describe la identidad visual de una colección; no implica reglas ni estadísticas exclusivas.

| Colección | Idea acordada | Dirección visual inicial propuesta |
|---|---|---|
| **Pharaoh Cats** | Gatos egipcios. | Felinos con tocados y ornamentos, edificios de arenisca y acentos dorados. |
| **Gummy Brigade** | Ositos de goma. | Personajes de colores, materiales gelatinosos y construcciones inspiradas en caramelos. |
| **Cosmic Crew** | Aliens. | Criaturas extraterrestres, tecnología espacial y edificios con formas de naves. |

Las tres colecciones permanecen en fase conceptual. Los diseños de unidades, edificios, animaciones y efectos se realizarán en una etapa posterior. Las facciones Industrial y Biological del prototipo siguen disponibles como hasta ahora.

## Alcance técnico del cambio de nombre

- El menú y el nombre de producto de Unity usan **Little Legion**.
- Los próximos builds de cliente se generan como `Builds/Quest/LittleLegion.apk` y `Builds/Desktop/LittleLegion.app`. El servidor usa el nombre de producto **Little Legion Server** y la imagen Docker predeterminada `little-legion-server:ngo-local`.
- El identificador Android elegido es **`app.sadmonkey.littlelegion`**. Reemplaza al del prototipo: Android instala Little Legion como una aplicación nueva y puede conservar ambas instalaciones. No se migran preferencias ni partidas del prototipo anterior.
- Se conservan la compañía técnica `Frontier`, los namespaces, las rutas de assets, el menú de herramientas del Editor, las claves de preferencias y los argumentos de ejecución existentes. Son identificadores internos; el cambio de marca no requiere renombrarlos.
- En el reproductor de desarrollo para macOS, Unity puede usar ubicaciones de preferencias e historial asociadas al nuevo nombre de producto; esta tarea no migra esos datos ni elimina los anteriores.
- Los builds ya generados y las evidencias históricas conservan sus nombres originales. Cambiar las fuentes no renombra ni recompila esas entregas.

La disponibilidad comercial del nombre no se ha confirmado; esta decisión documenta la identidad elegida para el proyecto.

## Prueba de menús para VR — 22 de octubre de 2026

La rama `codex/vr-menu-design` parte de la restauración `5f77b66`. Explora únicamente los menús y la consola de partida; la rueda y las ayudas del mando conservan su presentación original.

- Superficies opacas azul grisáceo de luminosidad media, texto marfil y acento menta. Rellenos planos y esquinas suaves mediante el componente UI nativo; sin degradados, marcos decorativos ni efectos añadidos al texto.
- Tipografía original `LegacyRuntime.ttf` de UGUI. La jerarquía usa tamaño, peso, espacio y agrupación.
- Inicio con un logo tipográfico grande y centrado, Nueva partida a todo el ancho y una grilla de LAN, Historial, Guías y Configuración debajo. La marca sólo aparece en esta pantalla. Pausa, ajustes, catálogos y replay comparten la misma paleta y jerarquía.
- Consola de partida con recursos sin fondo propio, iconos geométricos de 64 px y cifras de 48 px, alineados con la cabecera. El HUD ya no tiene cabecera ni logo: los recursos van primero, con 44 px de aire por encima y debajo. Mapa, facciones/modo, reloj y Pausa están en la última fila; los modos de colocación y habilidades se indican allí. La selección conserva una fila de seis tarjetas anchas, con el total en la cabecera. Al seleccionar más tipos, compacta únicamente sus tarjetas dentro del mismo espacio, sin paginar el catálogo actual ni desplazar las demás secciones. «Opciones» reserva una grilla fija de tres filas: construcción/entrenamiento a la izquierda, órdenes/investigación a la derecha; permanece vacía y conserva su tamaño sin selección o con unidades y edificios mezclados. Los espacios vacíos conservan placeholders tenues sin interacción. Construcción, investigación y órdenes usan posiciones fijas; el entrenamiento compacta el catálogo del edificio seleccionado, sin huecos iniciales, y mantiene su posición ante cambios de recursos o cola. La cola sigue dependiendo de los edificios seleccionados y usa tarjetas individuales, con la activa más ancha y verde; la cancelación se conserva en cada elemento de la cola, sin botón adicional.
- Órdenes comparte la región fija de Producción: dos filas de seis tarjetas, 11 órdenes como máximo contando la actual y una casilla fija para cancelar todo. El clic quita esa orden; quitar la actual avanza a la siguiente. En Producción se puede cancelar cualquier tarjeta ocupada, también la activa y las de varios edificios, con reembolso y sin cambiar la selección.
- Los grupos de control muestran números grandes y ocupan todo el ancho inferior. Gatillo selecciona un grupo; Grip + Gatillo asigna la selección al soltar sobre la misma tarjeta. El modificador se toma al presionar, sin asignación por mantener ni loader. En escritorio se usa Ctrl + clic. La tarjeta confirma en verde durante 1,2 s y usa el evento de vibración existente. Apartar el puntero, cambiar selección o perder foco/tracking cancela el gesto pendiente. Todas las ventanas comparten una barra redondeada separada debajo del panel, que crece y muestra «Mover ventana» al apuntarla manteniendo el mismo color y una zona de agarre estable. Las instrucciones de agarre, distancia y tamaño aparecen debajo, fuera de la pastilla; no hay botones Mover ni Restablecer en sus cabeceras. Se conservan los gestos de selección, colas y grupos; Restablecer vista sigue retirado.
- La paleta plana, las esquinas y los márgenes de la consola se extienden al resto de los menús. Las instrucciones de selección, grupos, producción y órdenes se consultan pulsando «?»: una ventana centrada muestra información detallada y se cierra con Cerrar, Atrás o un clic exterior. Mientras está abierta consume las entradas del mapa y de la rueda; el box de rally y el botón de quitar puntos se retiran del HUD, conservando el comportamiento de los puntos en el mapa; las acciones conservan su ayuda al apuntarlas. Se revisa con capturas reales del menú renderizado por Unity antes de continuar con otras pantallas.
- Los boxes comparten esquinas de 10 px, padding de 24 px, separación de 16 px y títulos de 20 px. El contenido comienza a 76 px del borde superior, después del título y la ayuda. Se eliminan los subtítulos de grupos de acciones y sus bordes laterales; «Población» muestra sólo el valor/capacidad.
- Los contadores aparecen inmediatamente junto a sus títulos, también al cambiar de idioma. La escala inicial conserva el factor 1512/1636 de la versión anterior y el HUD mantiene su tamaño de 1480 × 1636 unidades, incluso con una selección grande. Esa escala es el valor inicial común, fijado al crear la consola para que la colocación del tablero no lo altere. Al agarrar una ventana, el stick horizontal ajusta su tamaño suavemente entre el 65 % y el 160 % de ese valor; el vertical conserva el ajuste de distancia. Cada página recuerda su propia escala al soltar, navegar o perder foco/tracking, sin mover el centro compartido ni cambiar las otras páginas.
- Todas las páginas comparten el centro de la ventana como ancla: navegar, pausar, comenzar/terminar partidas y cambiar la altura del contenido conserva ese centro. Mover cualquier ventana actualiza la posición común. El primer uso aparece frente al visor; se guarda un offset relativo al visor para el próximo inicio. La preferencia anterior relativa al tablero se ignora sin borrarla hasta mover o restablecer. La recuperación por relocalización lejana y el atajo de recuperar menú operan sobre esta única posición.
- Al iniciar o reiniciar una partida, una tarjeta plana muestra 3–2–1 en ese mismo centro, con un tono por número y la señal de inicio al terminar. El HUD y su agarre se ocultan durante esos tres segundos. Selección, órdenes, rueda, pausa y movimiento del menú quedan bloqueados; se exige soltar los controles mantenidos antes de aceptar otra acción. La simulación offline permanece en tick 0 y las interrupciones de visor o colocación suspenden el conteo. LAN usa el reloj del servidor para el inicio común; los replays no tienen conteo.
- Producción y Órdenes en cola comparten exactamente la misma región fija de 236 px, reservada también sin selección o al seleccionar recursos. Selección, Opciones, grupos, mensajes y pie mantienen sus rectángulos durante la partida; sólo cambia el contenido.
- Minerales, gas y población forman una fila más compacta, con 16 px entre sus áreas. Las tarjetas compactas de producción activa tienen borde verde de 4 px; la primera mantiene su relleno verde y mayor ancho. El quinto espacio también muestra borde verde cuando está ocupado, aunque esté en espera, y amarillo cuando indica «+1» o más órdenes adicionales. Al vaciarse, pierde el borde.
- Nueva partida muestra las dos facciones con retratos de sus edificios principales. Comenzar tiene mayor tamaño y relleno verde. La confirmación de abandono sólo ofrece Seguir en verde y Abandonar en rojo; Atrás del mando sigue disponible.
- El hover de los botones usa el mismo relleno menta de LEGION, con texto e iconos oscuros para mantener el contraste. Al retirar el puntero se recuperan sus colores originales. Los mensajes normales, de éxito y de error comparten posición, tipografía y espacio reservado; los errores se distinguen sólo por el texto rojo, sin fondo, icono ni borde. En partida se alinean con la línea de recolección y el margen interior de las secciones, sin sumar el desplazamiento de los iconos de recursos. Las barras siguen mostrando cantidades exactas y los colores funcionales de recursos, salud y equipos conservan su significado.

La comodidad, la lectura en movimiento y el contraste final deben compararse con la versión restaurada dentro del visor Quest; las capturas del Editor sirven para revisar distribución y contenido.
