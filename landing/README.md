# Little Legion · Landing

Página estática en español para `/landing/`, sin dependencias de Node, Unity ni
servicios online. HTML y CSS adaptables a móvil, navegación por anclas sin
JavaScript y metadatos Open Graph/Twitter incluidos.

## Vista local

Desde la raíz del repositorio:

```sh
python3 -m http.server 4175 --bind 127.0.0.1 --directory landing
```

Abrir `http://localhost:4175/`. El contenedor la sirve en `/landing/` y redirige
su raíz a esa ruta. No hace falta compilar el juego para publicar esta página.

## Coolify / Docker

Configuración para una aplicación basada en este repositorio, según la
[guía de Dockerfile de Coolify](https://coolify.io/docs/applications/builds/dockerfile):

| Campo | Valor |
| --- | --- |
| Build pack / strategy | Dockerfile |
| Base Directory | `/` (raíz del repositorio) |
| Dockerfile Location | `/Dockerfile.landing` |
| Ports Exposes | `8080` |
| Health check | HTTP `GET /healthz`, puerto `8080` |
| Variable de runtime | `LANDING_URL=https://tu-dominio/landing/` |

Configurar el dominio en Coolify. `LANDING_URL` debe ser la URL pública completa
de la página, incluyendo `/landing/`: actualiza canonical, `og:url` y las URLs
absolutas de las imágenes al iniciar el contenedor. Por defecto se utiliza
`https://littlelegion.app/landing/` como URL propuesta; confirmar el dominio
antes de publicar. La variable no cambia las rutas que sirve nginx.

Prueba local del mismo contenedor:

```sh
docker build -f Dockerfile.landing -t little-legion-landing .
docker run --rm -p 127.0.0.1:4175:8080 \
  -e LANDING_URL=http://localhost:4175/landing/ little-legion-landing
```

Abrir `http://localhost:4175/landing/`. Los recursos inexistentes devuelven 404.
No hay cuentas, formularios, analytics ni acceso al backend desde esta página.

`Dockerfile.web` también incluye `/landing/` junto al build de Unity WebGL.
Esa imagen sigue requiriendo `Builds/WebGL/`; usa los metadatos públicos del
HTML original. Para servir únicamente la landing, usar `Dockerfile.landing`.

## Assets entregados

| Archivo | Uso |
| --- | --- |
| `assets/header.webp` | Hero optimizado, 1672 × 941 |
| `assets/header.png` | Header PNG para reutilizar, 1672 × 941 |
| `assets/og-image.png` | Open Graph y Twitter, 1200 × 630 |
| `assets/icon.png` | Icono, 512 × 512 |
| `assets/icon-192.png` | Icono para el manifest, 192 × 192 |
| `assets/apple-touch-icon.png` | Icono Apple, 180 × 180 |
| `favicon.ico` | Favicon raster, 16/32/48/64 px |
| `favicon.svg` | Variante vectorial simplificada para tamaños pequeños |
| `site.webmanifest` | Metadatos e iconos del sitio; no añade modo offline |

Header, icono y OG generados con `imagegen`, tomando como referencia el concepto
`art/concepts/little-legion/2026-10-21/05-geometria-actual.png`: base industrial
azul con tres barras amarillas, colmena biológica coral y miniaturas geométricas
en terreno verde. El header no contiene texto; el OG combina la marca y el lema
con el diorama. El favicon SVG simplifica la silueta del cuartel. Los PNG/WebP/ICO
se exportaron a las dimensiones indicadas; la página identifica el hero como
arte conceptual, no como captura de juego. No incorpora las skins futuras.

La disponibilidad descrita sigue el README del proyecto: prototipo y versiones
en desarrollo/pruebas. Los enlaces llevan al repositorio; no se anuncian
descargas públicas ni tiendas que aún no existen.

## Comprobaciones

El 30 de octubre de 2026 se verificó la página con Chromium headless en
1440 × 1000, 768 × 1024, 390 × 844 y 320 × 740, con JavaScript desactivado:
sin desbordamiento horizontal ni imágenes rotas; navegación por teclado y
anclas, favicon, manifest e iconos correctos. Se revisaron capturas de escritorio
y móvil y las dimensiones de los assets. `git diff --check` y `sh -n` del script
de inicio pasaron. El motor de Docker no estaba disponible en esa máquina;
la compilación del contenedor y su ejecución deben verificarse en el host de
despliegue. No se publicó la página ni se cambió el backend.
