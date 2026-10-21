# Idiomas de la interfaz

**Configuración → Idioma** (Settings → Language en inglés) permite alternar **Sistema**, **English** y **Español**. La misma opción está disponible desde el menú inicial y desde pausa. El cambio es inmediato y se guarda en el dispositivo, sin cambiar la partida, selección, órdenes, ubicación del tablero ni preferencias de controles.

Sin preferencia explícita se consulta `Application.systemLanguage`: español usa `es`, inglés usa `en` y cualquier idioma aún no disponible usa inglés. Elegir Sistema elimina la preferencia `frontier.language.v1`; en cada arranque se vuelve a resolver el idioma del dispositivo. Una preferencia guardada que ya no exista también vuelve al sistema con respaldo en inglés.

## Catálogos

`Assets/Frontier/Resources/Localization/*.json` contiene archivos UTF-8 con `code`, `nativeName`, `culture`, `systemLanguages` y `entries`. El selector enumera los archivos cargados, sin un enum fijo de idiomas. `nativeName` se muestra en el propio idioma para poder reconocerlo y cambiarlo.

Las claves son las frases fuente en inglés. `en.json` conserva esas frases como respaldo; `es.json` agrega sus traducciones, incluidos nombres de unidades, estructuras, habilidades y mapas. Una clave ausente conserva su texto fuente. Los IDs del catálogo y de las acciones siguen siendo los originales; tampoco se traducen comandos, hashes, reglas ni archivos de partidas.

```json
{
  "code": "fr",
  "nativeName": "Français",
  "culture": "fr",
  "systemLanguages": ["French"],
  "entries": [
    { "key": "New Match", "value": "Nouvelle partie" },
    { "key": "Workers {0}/{1}", "value": "Ouvriers {0}/{1}", "template": true }
  ]
}
```

Para añadir un idioma, copiar la estructura, usar el nombre correspondiente del enum `SystemLanguage`, traducir las entradas y ejecutar la validación. Los parámetros `{0}`, `{1}`, etc. deben conservarse, aunque se pueden reordenar. Preservar las etiquetas de texto enriquecido, saltos de línea y símbolos de botones. Revisar espacio y cobertura de glifos con la fuente del juego; la validación actual cubre español e inglés.

## Integración

`LocalizedText` es un `UnityEngine.UI.Text` que conserva su fuente y actualiza el texto visible al cambiar el idioma. Los textos ocultos se actualizan al volver a activarse. Los nombres internos de GameObjects y los identificadores usados por los controles no se traducen. Las tres fábricas de texto del juego lo utilizan: panel, rueda y marcadores de recolección/construcción.

Para una etiqueta nueva, preferir `SetKey("Workers {0}/{1}", workers, capacity)`: conserva clave y argumentos y permite cambiar el idioma incluso sin regenerar el panel. Las etiquetas estáticas pueden recibir su frase inglesa en `text`. `Localization.Format` sirve cuando solo se necesita una cadena ya resuelta. Para ampliar una etiqueta existente, `Append` conserva la fuente; evitar leer el texto visible traducido para concatenarle otra frase fuente.

Las cadenas compuestas de la interfaz existente se adaptan mediante entradas `template`: coincidencias del texto completo, ordenadas por cantidad de texto literal, con traducción recursiva de parámetros. No se sustituyen palabras arbitrarias dentro de una frase. Los parámetros no absorben otras líneas; solo la composición explícita `{0}\n{1}` permite unir bloques. La traducción tiene profundidad limitada, comprobaciones de prefijo/sufijo y una caché acotada; las etiquetas no recalculan si su fuente no cambia. Las nuevas composiciones deben usar `SetKey` con argumentos y una prueba que preserve sus datos, en especial cuando contienen frases que también aparecen en otros contextos. No usar cadenas traducidas para decisiones de juego o claves persistentes.

## Validación

`Frontier.Editor.LocalizationPreview.Run` comprueba resolución de idioma, persistencia, respaldo, parámetros de todos los templates, etiquetas ocultas, cambio en vivo y estado de partida intacto. Navega con rayos por ajustes, guías de ambas facciones, controles desktop/Touch, HUD, resultados, historial y replay en español. Las capturas y auditorías quedan en `TestResults/Localization/`. También ejecuta regresiones de menús, sesión de replay y panel de espectador en inglés, restaurando al finalizar la preferencia original.

`LocalizationPreview.Scan` genera la misma auditoría para trabajar en traducciones antes de exigir que todo quepa. `rendered.txt` contiene las frases fuente y el resultado visible para revisar también composiciones parcialmente traducidas. Revisar las cadenas sin cambios: términos compartidos, nombre del juego y nombres físicos de teclas pueden coincidir en ambos idiomas. La comprobación del Editor no reemplaza una prueba de legibilidad en Quest.
