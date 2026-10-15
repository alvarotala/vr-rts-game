# Frontier Command — RTS para Quest 3

Prototipo nativo de estrategia en realidad mixta: un tablero sobre una mesa o el piso, dos facciones, economía, construcción, producción, combate terrestre y una IA local. Funciona sin servicios de juego externos. El nombre y el arte geométrico son provisionales.

## Abrir y compilar

Requiere **Unity 6000.3.26f1**, Android Build Support, SDK/NDK y OpenJDK instalados desde Unity Hub. Los paquetes están fijados en `Packages/manifest.json` y `packages-lock.json`. Usar la versión indicada: Unity 6.5 produjo errores de compatibilidad en Meta XR 203 durante la validación inicial.

1. Agregar esta carpeta a Unity Hub y abrirla con 6000.3.26f1.
2. Ejecutar **Frontier → Configure project** una vez, o `bash scripts/unity.sh configure`.
3. Abrir `Assets/Frontier/Scenes/Frontier.unity` y pulsar Play. La escena crea sus componentes en ejecución mediante `Bootstrap`.
4. Para Android: **Frontier → Build Quest APK**, o `bash scripts/unity.sh quest`.
5. El APK se genera en `Builds/Quest/FrontierCommand.apk`.

Para instalarlo en un Quest con modo desarrollador y depuración USB autorizada:

```sh
adb devices
adb install -r Builds/Quest/FrontierCommand.apk
adb shell monkey -p com.alvaro.frontiercommand 1
```

El APK usa el firmado de desarrollo local. La publicación en una tienda, sus cuentas y su firmado de distribución no están configurados.

La versión de prueba para macOS con Apple Silicon se genera con `bash scripts/unity.sh desktop`, en `Builds/Desktop/FrontierCommand.app`. Se puede definir `UNITY_EDITOR` si el editor está instalado en otra ruta.

## Jugar

Elegir facción, rival, dificultad y mapa. En Quest, usar **Ubicar tablero** y confirmar con el gatillo. Si la habitación no está registrada o se deniega el permiso, usar **Mesa** o **Piso** y ajustar el plano manualmente. Durante la colocación, el stick secundario modifica la altura y el principal modifica tamaño y orientación.

Luego iniciar la partida. Los seis trabajadores iniciales comienzan a recolectar. Seleccionar uno permite construir; seleccionar un edificio permite producir o investigar. Un extractor debe cubrir un depósito violeta. Se gana destruyendo todos los edificios enemigos.

| Acción | Mandos Quest | Prueba de escritorio |
|---|---|---|
| Seleccionar / seleccionar área | Gatillo principal / arrastrar | Clic izquierdo / arrastrar |
| Agregar a selección | Gatillo secundario | Shift |
| Orden contextual | A, o X en modo zurdo | Clic derecho |
| Cancelar / opciones | B, o Y en modo zurdo | Escape |
| Desplazar mapa | Stick secundario | WASD |
| Zoom | Stick principal vertical | Rueda |
| Ver mapa completo | Pulsar stick principal | F |
| Pausa | Menú o panel | Espacio |
| Grupo de control | Botón del panel; mantener para guardar | 1–3; Ctrl+1–3 para guardar |

El juego incluye selección ampliada, contorno de selección, previsualización de construcción, panel contextual, avisos, sonidos de confirmación y vibración. Los controles de la mano principal se invierten desde el panel.

## Contenido

- **Consorcio**: unidades resistentes, construcción atendida por trabajadores y reparación con recursos.
- **Simbiosis**: unidades más móviles, edificios que crecen y regeneración fuera del combate.
- Cada facción: trabajador, explorador, combate cercano, tirador y asedio; seis edificios; dos mejoras.
- Hasta 50 unidades móviles y 20 edificios por jugador.
- **La Fractura**, mapa simétrico de 128 × 128, con dos bases, cuatro expansiones, alturas y rampas.
- **Campo de entrenamiento**, mapa de 48 × 48 con instrucciones contextuales.
- IA fácil y normal; admite los cuatro cruces de facciones. No usa un modelo de lenguaje ni acceso a internet.

## Comprobaciones

```sh
bash scripts/check-core.sh
bash scripts/check-core.sh --matches --count=20
bash scripts/unity.sh validate
```

El núcleo se prueba con .NET 8 sin dependencias NuGet. El script busca `dotnet` o el SDK incluido en la instalación local de Unity 6.5; se puede indicar otro ejecutable mediante `DOTNET`.

Prueba gráfica automatizada, después de generar el ejecutable de escritorio:

```sh
bash scripts/smoke-desktop.sh
```

Esta prueba acciona el botón de inicio mediante su superficie real, comprueba movimiento efectivo y guarda capturas. También revisa el registro completo para detectar fallos anteriores al inicio de los scripts. No sustituye una prueba humana ni una prueba de Quest.

Ver [arquitectura](docs/architecture.md), [formato de mapas](docs/maps.md) y [validación y pendientes](docs/validation.md).

## Próximas etapas

El multijugador por internet y el editor de computadora son etapas posteriores. El núcleo ya separa órdenes, estado autoritativo, información visible y colocación física. No se han conectado servicios de Relay ni creado cuentas de infraestructura.

