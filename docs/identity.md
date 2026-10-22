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
