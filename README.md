# Frontier Command — RTS para Quest 3

Prototipo nativo de estrategia en realidad mixta: un tablero sobre una mesa o el piso, dos facciones, economía, construcción, producción, combate terrestre y una IA local. Funciona sin servicios de juego externos. El nombre y el arte geométrico son provisionales.

## Requisitos y configuración

Requiere **Unity 6000.3.26f1**, Android Build Support, SDK/NDK y OpenJDK instalados desde Unity Hub. Los paquetes están fijados en `Packages/manifest.json` y `Packages/packages-lock.json`.

1. Agregar esta carpeta a Unity Hub y abrirla con 6000.3.26f1.
2. Ejecutar **Frontier → Configure project** una vez, o `bash scripts/unity.sh configure`.
3. Abrir `Assets/Frontier/Scenes/Frontier.unity` y pulsar Play.

Se puede definir `UNITY_EDITOR` si el editor está instalado en otra ruta.

## Compilar e instalar

Para generar el APK, usar **Frontier → Build Quest APK** o:

```sh
bash scripts/unity.sh quest
```

El archivo se genera en `Builds/Quest/FrontierCommand.apk`. Para instalarlo en un Quest con modo desarrollador y depuración USB autorizada:

```sh
adb devices
adb install -r Builds/Quest/FrontierCommand.apk
adb shell monkey -p com.alvaro.frontiercommand 1
```

El APK usa el firmado de desarrollo local. La publicación en una tienda, sus cuentas y su firmado de distribución no están configurados.

La versión de prueba para macOS con Apple Silicon se genera con `bash scripts/unity.sh desktop`, en `Builds/Desktop/FrontierCommand.app`.

## Uso básico

Elegir facción, rival, dificultad y mapa. En Quest, usar **Ubicar tablero** y confirmar con el gatillo; si no hay datos de la habitación, usar **Mesa** o **Piso** para colocarlo manualmente. Iniciar la partida, seleccionar unidades con el gatillo y dar órdenes contextuales con A (X en modo zurdo).

Los trabajadores recolectan y construyen; los edificios producen unidades e investigan mejoras. Se gana destruyendo todos los edificios enemigos. Consultar la [guía de juego](docs/gameplay.md) para los controles completos, el movimiento del menú y la identificación de unidades.

## Documentación

- [Desarrollo y comprobaciones](docs/development.md)
- [Arquitectura y próximas etapas](docs/architecture.md)
- [Formato de mapas](docs/maps.md)
- [Registro de validación y pendientes](docs/validation.md)
