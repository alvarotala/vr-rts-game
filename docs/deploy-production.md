# Despliegue y operación de Little Legion en producción

Esta guía reproduce la instalación de `server-us-1.littlelegion.app` y explica
cómo actualizarla conservando cuentas, partidas y ranking. Los comandos de
Ubuntu se ejecutan como `root`, en Bash, desde `/root/little-legion`. Si se usa
otro usuario o directorio, adaptar también las rutas de transferencia y Compose.

Si el servidor ya está levantado, empezar por [actualizar una instalación
existente](#7-actualizar-una-instalación-existente); no repetir la instalación ni
regenerar credenciales desde cero.

- [Instalación inicial](#1-preparar-ubuntu-y-el-acceso-al-repositorio)
- [Comando común y arranque](#4-comando-común-y-primer-arranque)
- [Verificación y prueba de clientes](#5-verificar-la-instalación)
- [Qué actualizar según el cambio](#6-qué-actualizar-según-el-cambio)
- [Actualización y backup verificado](#7-actualizar-una-instalación-existente)
- [Rollback de aplicación](#8-volver-a-la-versión-anterior)
- [Recuperación de datos](#9-recuperar-datos-desde-un-backup)
- [Diagnóstico y operación](#10-operación-y-problemas-frecuentes)

## Instalación de referencia

| Elemento | Valor |
| --- | --- |
| Servidor | Ubuntu 26.04, CPU x86_64/amd64 |
| DNS A | `server-us-1.littlelegion.app` → `50.6.197.145` |
| Backend para los clientes | `https://server-us-1.littlelegion.app` |
| Partidas públicas | `50.6.197.145:7778/UDP` |
| Servicios | Caddy, Nakama OSS 3.41.0, PostgreSQL 17, Unity/NGO |
| Capacidad inicial | Una partida simultánea; las siguientes parejas esperan |
| Proyecto Compose | `little-legion-online` |
| Base de datos | Volumen `little-legion-online_online-postgres` |
| Certificados | Volúmenes `little-legion-online_caddy-data` y `little-legion-online_caddy-config` |

Docker compila el módulo Go de Nakama y empaqueta el servidor Unity. **El build
Linux de Unity se genera en la Mac y se transfiere a Ubuntu**: `Builds/` no está
en Git. El generador actual usa el toolchain macOS ARM64 → Linux amd64 IL2CPP;
no está preparado para compilar Unity dentro del contenedor de producción.

El 27 de octubre de 2026 el operador compartió `docker compose ps -a` con
PostgreSQL y Nakama saludables, Caddy y Unity ejecutándose y la migración
terminada con código 0. Eso confirma el arranque de los contenedores. La emisión
del certificado, la admisión por UDP y una partida real desde Quest se verifican
por separado con los pasos de esta guía.

## 1. Preparar Ubuntu y el acceso al repositorio

Para una instalación nueva de Docker:

```bash
uname -m
apt update
apt install -y ca-certificates curl git python3 rsync
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
apt update
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
docker compose version
```

`uname -m` debe devolver `x86_64`. Compose debe soportar `!override` (2.24.4 o
posterior). Si Docker ya estaba instalado, comprobar versión y posibles
conflictos antes de reemplazar paquetes; no borrar su directorio de datos.
Fuentes: [Docker en Ubuntu](https://docs.docker.com/engine/install/ubuntu/) y
[reemplazo de valores Compose](https://docs.docker.com/reference/compose-file/merge/#replace-value).

Para un repositorio privado, configurar en el servidor una clave SSH de
despliegue con acceso de lectura, o el método HTTPS autorizado del proveedor.
Registrar únicamente su clave pública en el proveedor; no incluir tokens o
claves privadas en la URL del repositorio. El servidor solo necesita leer Git.

Reemplazar `URL_DEL_REPO`. La rama debe existir en el remoto: los commits locales
de la Mac no llegan al servidor con un `pull` hasta que el responsable del
repositorio los publique. Esta guía no ejecuta ni requiere hacer push desde el
servidor.

```bash
git clone --branch codex/public-matchmaking URL_DEL_REPO /root/little-legion
cd /root/little-legion
git log -1 --oneline
mkdir -p Builds/Server
```

Si el repositorio ya existe, no clonarlo otra vez. Comprobar `git status --short`
y `git branch --show-current`. Usar la rama de despliegue acordada; no cambiar a
`main` suponiendo que ya contiene multiplayer y ranking.

## 2. Generar y transferir el servidor Unity

**En la Mac**, usar el mismo commit que se va a desplegar, con el árbol de trabajo
limpio. Se requiere la versión de Unity de `ProjectSettings/ProjectVersion.txt`
(actualmente 6000.3.26f1), Linux Dedicated Server Build Support y Unity CLI.
El [flujo de build dedicado](multiplayer.md#build) describe el generador.

```bash
cd /Users/alvaro/Documents/vr-rts-game
git status --short
python3 scripts/build-server.py
```

Continuar únicamente si finaliza con `FRONTIER_DEDICATED_BUILD passed` y el log
`TestResults/dedicated-server-build.log` contiene `FRONTIER_SERVER_BUILT` con
`backend=IL2CPP`. Registrar el commit del build después de esa comprobación:

```bash
git rev-parse HEAD > Builds/Server/source-commit.txt
rsync -az --progress --delete \
  --exclude='*_BackUpThisFolder_ButDontShipItWithYourGame' \
  --exclude='*_BurstDebugInformation_DoNotShip' \
  Builds/Server/ root@50.6.197.145:/root/little-legion/Builds/Server/
```

`--delete` sincroniza exclusivamente el directorio de artefactos `Builds/Server/`;
revisar ese destino antes de ejecutar. No apuntarlo a la raíz del proyecto ni a
`.cache/online`. Se transfieren el ejecutable, `GameAssembly.so`, `UnityPlayer.so`
y los datos, no solamente `FrontierServer`. Los contenedores existentes contienen
una copia del build; esta transferencia por sí sola no los actualiza ni reinicia.

## 3. Crear la configuración privada y HTTPS

**En Ubuntu**, para una instalación nueva:

```bash
cd /root/little-legion
python3 scripts/online-stack.py init --host 50.6.197.145
```

Esto crea `.cache/online/env` y `.cache/online/config.yml` con permisos privados.
El `env` conserva las credenciales en ejecuciones posteriores de `init`.
**Si ya hay una base de datos y se perdió el env, recuperar su respaldo antes de
ejecutar init**: generar claves nuevas no recupera las credenciales anteriores.
El mensaje HTTP que imprime el helper es informativo; los jugadores usan HTTPS.

Crear los siguientes archivos solo durante la instalación inicial. En un
servidor existente, revisarlos antes de sobrescribir personalizaciones:

```bash
cat > .cache/online/Caddyfile <<'EOF'
server-us-1.littlelegion.app {
    reverse_proxy nakama:7350
}
EOF

cat > .cache/online/compose.prod.yml <<'EOF'
services:
  nakama:
    ports: !override
      - "127.0.0.1:7350:7350"
      - "127.0.0.1:7351:7351"

  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./.cache/online/Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      nakama:
        condition: service_healthy

volumes:
  caddy-data:
  caddy-config:
EOF
```

Caddy obtiene y renueva el certificado para el dominio y redirige HTTP a HTTPS.
Requiere DNS correcto, los puertos 80/443 accesibles y almacenamiento persistente
para certificados. [HTTPS automático de Caddy](https://caddyserver.com/docs/automatic-https).

Permitir en el firewall del proveedor **80/TCP, 443/TCP y 7778/UDP**, además del
puerto SSH utilizado. Comprobar también cualquier firewall del host. La API
7350 y la consola 7351 quedan vinculadas a loopback; PostgreSQL no publica puerto.
Docker puede publicar puertos eludiendo reglas UFW: no abrir 7350 globalmente
confiando solo en UFW para cerrarlo. [Docker y UFW](https://docs.docker.com/engine/network/packet-filtering-firewalls/#docker-and-ufw).

## 4. Comando común y primer arranque

Definir esta función **al abrir cada sesión SSH**. Usa rutas absolutas y ambos
archivos Compose, también para backup, logs y actualizaciones:

```bash
cd /root/little-legion
dc() {
  docker compose --project-directory /root/little-legion \
    --env-file /root/little-legion/.cache/online/env \
    -f /root/little-legion/compose.online.yml \
    -f /root/little-legion/.cache/online/compose.prod.yml \
    --profile game "$@"
}
```

No usar `python3 scripts/online-stack.py up`, `backend` o `down` en producción:
ese helper usa solamente el Compose local, sin el override de HTTPS y loopback.
Su acción `init` sí se usa aquí para generar la configuración de Nakama.

```bash
test -s .cache/online/env
test -f .cache/online/compose.prod.yml
test -f .cache/online/Caddyfile
test -f Builds/Server/FrontierServer
test -f Builds/Server/GameAssembly.so
dc config --quiet
dc build migrate server
```

Si alguna comprobación o el build falla, resolverlo antes de continuar. Después:

```bash
dc up -d --no-build
dc ps -a
dc logs --tail=100 nakama server caddy
```

`migrate` con `Exited (0)` es normal. PostgreSQL y Nakama deben estar `healthy`;
Caddy y Unity deben permanecer ejecutándose. El servidor debe registrar:

```text
FRONTIER_ALLOCATION Registered dedicated server.
```

Los servicios tienen `restart: unless-stopped`. Arrancan con Docker después de
un reinicio del host, salvo que se hayan detenido explícitamente. La migración
es un proceso de una sola ejecución, no un servicio permanente.

## 5. Verificar la instalación

En Ubuntu:

```bash
curl --fail --show-error --max-time 15 https://server-us-1.littlelegion.app/healthcheck
dc logs --since=10m --tail=100 server caddy
dc exec -T postgres psql -X -U nakama -d nakama -c \
  "SELECT id,host,port,active,match_id,now()-heartbeat_at AS heartbeat_age FROM ll_servers;"
```

El slot `local-1` debe anunciar `50.6.197.145`, puerto `7778`, estar activo y
tener heartbeat reciente. Sin partida, `match_id` debe ser nulo. No ejecutar
`docker compose config` sin `--quiet` para compartir su salida: incluye secretos.

Repetir el `curl` desde la Mac u otra red para comprobar HTTPS externo. No usar
`curl -k`: ocultaría un problema de certificado. Un healthcheck HTTP correcto
no prueba UDP, WebSocket autenticado ni una partida completa.

Para probar desde desktop en la Mac:

```bash
open -n "/Users/alvaro/Documents/vr-rts-game/Builds/Desktop/LittleLegion.app" --args \
  -frontier-backend https://server-us-1.littlelegion.app -frontier-matchmake \
  -logFile /tmp/little-legion-production.log
```

Los builds actuales de Quest y desktop ya seleccionan ese servicio HTTPS por
defecto, puerto 443; usar **Buscar partida**. Al actualizar desde el prototipo
local se ignora la preferencia antigua del backend, conservando la conexión LAN
y las identidades por servicio. Un argumento explícito `-frontier-backend`
permite seguir probando contra un backend de desarrollo.
El cliente exige HTTPS para dominios públicos. Verificar con dos
clientes una partida completa y el cambio de Elo. El endpoint UDP se recibe
automáticamente. Las cuentas locales no se migran a producción y la identidad
del cliente se guarda por endpoint: es esperable empezar sin ranking allí.

## 6. Qué actualizar según el cambio

| Cambio | Acción |
| --- | --- |
| Go en `backend/nakama`, Elo, búsquedas o SQL de la aplicación | Pull, backup verificado, build de Nakama y recreación en mantenimiento; puede reutilizarse el build Unity si su contrato sigue compatible |
| Simulación, networking, mapas, catálogo o paquetes compartidos | Build Linux nuevo en la Mac, transferencia, build Docker y despliegue; producir clientes compatibles también |
| UI, textos o presentación exclusiva del cliente | Publicar APK/desktop nuevos; no reiniciar producción por ese cambio si no afecta contenido o contrato compartido |
| Caddyfile o configuración Compose | Revisar y aplicar el override de producción; no requiere recompilar Unity |
| Versión mayor de PostgreSQL, versión de Nakama o protocolo incompatible | Plan específico de migración y compatibilidad; no aplicar como una actualización rutinaria |

El servidor y los clientes comprueban protocolo, reglas y hash de contenido.
Cambiar reglas o contenido puede separar colas o impedir que un cliente viejo
obtenga un servidor compatible. El pool actual tiene un solo slot: coordinar
la distribución de clientes y el mantenimiento, sin prometer actualización
sin interrupciones.

## 7. Actualizar una instalación existente

Este procedimiento conserva PostgreSQL y sus volúmenes. Programar una ventana
sin jugadores; actualmente no existe un modo de drenaje automático. No ejecutar
las pruebas de integración/smoke contra la base real: crean cuentas y partidas
de prueba, y algunas manipulan el pool.

### 7.1 Preparar la versión y guardar la anterior

Primero probar el cambio en desarrollo. Si cambió Unity, compilar y transferir el
build siguiendo el paso 2 antes de reconstruir las imágenes. Confirmar que el
commit que se quiere desplegar está disponible en el remoto.

En Ubuntu, definir `dc` como en el paso 4. Crear el respaldo de versión **antes
del pull o de sobrescribir los tags de las imágenes**:

```bash
cd /root/little-legion
git status --short
git branch --show-current
LL_RELEASE=$(date -u +%Y%m%dT%H%M%SZ)
LL_BACKUP="$PWD/.cache/online/backups/$LL_RELEASE"
(
  set -euo pipefail
  umask 077
  mkdir -p "$LL_BACKUP"
  git rev-parse HEAD > "$LL_BACKUP/revision.txt"
  git branch --show-current > "$LL_BACKUP/branch.txt"
  cp .cache/online/env .cache/online/config.yml .cache/online/Caddyfile \
    .cache/online/compose.prod.yml compose.online.yml "$LL_BACKUP/"
  docker image tag "$(docker inspect --format '{{.Image}}' "$(dc ps -q nakama)")" \
    "little-legion-nakama:rollback-$LL_RELEASE"
  docker image tag "$(docker inspect --format '{{.Image}}' "$(dc ps -q server)")" \
    "little-legion-server:rollback-$LL_RELEASE"
)
```

Continuar solo si `git status --short` está vacío y el bloque termina sin errores.
No descartar cambios locales para forzar un pull. Los tags guardan las imágenes
que realmente ejecutan los contenedores, aunque su tag original se hubiera
reutilizado. No ejecutar `docker image prune -a` durante esta ventana.

### 7.2 Entrar en mantenimiento

Cerrar el acceso público de Caddy para que no entren nuevas conexiones y revisar
las asignaciones ya existentes:

```bash
dc stop caddy
dc exec -T postgres psql -X -U nakama -d nakama -c \
  "SELECT s.id,s.match_id,m.status FROM ll_servers s LEFT JOIN ll_matches m ON m.id=s.match_id;"
dc exec -T postgres psql -X -U nakama -d nakama -c \
  "SELECT status,count(*) FROM ll_matches WHERE status NOT IN ('finished','aborted') GROUP BY status;"
```

Con Caddy detenido, el backend público está en mantenimiento; la comunicación
interna Unity→Nakama sigue disponible. Esperar hasta que no haya asignaciones ni
partidas pendientes. Una partida terminada puede conservar el slot durante la
pantalla de resultados; esperar también a su liberación. Si aún hay jugadores
o colas que no se vacían, reabrir con `dc start caddy` y reprogramar, en lugar de
forzar su desconexión. Entonces:

```bash
dc stop server nakama
```

### 7.3 Respaldar y comprobar restauración

Con los procesos que escriben detenidos, PostgreSQL permanece funcionando.
El siguiente bloque respalda la base completa, restaura en una base temporal,
compara conteos de todas las tablas públicas y valores de secuencias, y elimina
solo esa base temporal. Si falla, **no continuar con la actualización**.

```bash
(
  set -euo pipefail
  umask 077
  test -d "$LL_BACKUP"
  dc exec -T postgres pg_dump -U nakama -d nakama -Fc > "$LL_BACKUP/nakama.dump"
  test -s "$LL_BACKUP/nakama.dump"
  cat > "$LL_BACKUP/verify.sql" <<'SQL'
SELECT format('SELECT %L AS relation, count(*) AS rows FROM %I.%I;',
              schemaname || '.' || tablename, schemaname, tablename)
FROM pg_tables WHERE schemaname='public' ORDER BY tablename
\gexec
SELECT schemaname,sequencename,last_value FROM pg_sequences
WHERE schemaname='public' ORDER BY sequencename;
SQL
  dc exec -T postgres psql -X -A -t -v ON_ERROR_STOP=1 -U nakama -d nakama \
    < "$LL_BACKUP/verify.sql" > "$LL_BACKUP/before.txt"
  LL_VERIFY_DB="ll_restore_$(date +%s)_$$"
  dc exec -T postgres createdb -U nakama "$LL_VERIFY_DB"
  trap 'dc exec -T postgres dropdb -U nakama "$LL_VERIFY_DB"' EXIT
  dc exec -T postgres pg_restore --exit-on-error --no-owner --no-privileges \
    -U nakama -d "$LL_VERIFY_DB" < "$LL_BACKUP/nakama.dump"
  dc exec -T postgres psql -X -A -t -v ON_ERROR_STOP=1 -U nakama -d "$LL_VERIFY_DB" \
    < "$LL_BACKUP/verify.sql" > "$LL_BACKUP/restored.txt"
  diff -u "$LL_BACKUP/before.txt" "$LL_BACKUP/restored.txt"
  (cd "$LL_BACKUP" && sha256sum nakama.dump > SHA256SUMS)
  touch "$LL_BACKUP/restore-verified"
)
```

La restauración ejecutada con `--exit-on-error` comprueba que el dump es
restaurable; `pg_restore --list` por sí solo no lo demuestra. La comparación
añade un control de tablas y secuencias, no una prueba funcional del juego.
[Referencia de pg_restore](https://www.postgresql.org/docs/17/app-pgrestore.html).

El respaldo contiene datos privados y credenciales. Copiar su directorio a un
destino de respaldo fuera del VPS, con acceso restringido. Los tags locales
permiten rollback en este host; para recuperación tras perder el host, conservar
además las imágenes en un archivo privado o los builds y el código de esa versión.

### 7.4 Actualizar y levantar

En la misma sesión, con `LL_BACKUP` todavía definido:

```bash
(
  set -euo pipefail
  test -f "$LL_BACKUP/restore-verified"
  test -s .cache/online/env
  git pull --ff-only
  python3 scripts/online-stack.py init
  dc config --quiet
  dc build migrate server
  dc run --rm --no-deps migrate
  dc up -d --no-build --no-deps --force-recreate --wait --wait-timeout 120 nakama
  dc up -d --no-build --no-deps --force-recreate server caddy
  git rev-parse HEAD > "$LL_BACKUP/deployed-revision.txt"
)
```

Antes de ejecutar este bloque, si hay cambios de Unity, comprobar que
`Builds/Server/source-commit.txt` corresponde al commit objetivo del pull. Si
solo cambia el backend y el contrato sigue compatible, se puede reutilizar el
build previo. `init` conserva claves y host, pero regenera `config.yml`: incorporar
explícitamente cualquier personalización adicional de Nakama antes de arrancar.

El procedimiento ejecuta la migración oficial explícitamente y espera al
healthcheck de Nakama antes de abrir Unity y Caddy. No recrea PostgreSQL ni usa
`down`. La tabla `ll_*` se actualiza al cargar el módulo de Nakama. Si falla la
migración, compilación o healthcheck, detenerse y revisar logs; no encadenar
comandos ignorando el error ni borrar volúmenes para resolverlo.

Repetir la verificación del paso 5 y una partida controlada. Conservar el backup
y los tags de rollback hasta confirmar la versión. Guardar también el commit
desplegado, fecha y resultado de la comprobación operativa.

## 8. Volver a la versión anterior

El rollback de aplicación **no restaura ni borra la base de datos**. Sirve solo
si el esquema actual sigue siendo compatible con el código anterior. Para
cambios incompatibles de esquema, PostgreSQL o Nakama, evaluar la recuperación
de datos antes de arrancar una versión antigua. No ejecutar migraciones `down`
automáticamente.

Entrar en mantenimiento como en el paso 7.2. Definir `LL_RELEASE` con la fecha
exacta del respaldo, no generar una fecha nueva:

```bash
LL_RELEASE=REEMPLAZAR_POR_FECHA_DEL_RESPALDO
LL_BACKUP="/root/little-legion/.cache/online/backups/$LL_RELEASE"
```

Comprobar que existen `revision.txt`, los archivos privados y ambas imágenes
`rollback-$LL_RELEASE`. Con el árbol Git limpio y los procesos detenidos:

```bash
(
  set -euo pipefail
  test -s "$LL_BACKUP/revision.txt"
  docker image inspect "little-legion-nakama:rollback-$LL_RELEASE" >/dev/null
  docker image inspect "little-legion-server:rollback-$LL_RELEASE" >/dev/null
  git switch --detach "$(cat "$LL_BACKUP/revision.txt")"
  cp "$LL_BACKUP/env" "$LL_BACKUP/config.yml" "$LL_BACKUP/Caddyfile" \
    "$LL_BACKUP/compose.prod.yml" .cache/online/
  cat > .cache/online/compose.rollback.yml <<EOF
services:
  nakama:
    image: little-legion-nakama:rollback-$LL_RELEASE
  server:
    image: little-legion-server:rollback-$LL_RELEASE
EOF
)
dcr() {
  docker compose --project-directory /root/little-legion \
    --env-file /root/little-legion/.cache/online/env \
    -f /root/little-legion/compose.online.yml \
    -f /root/little-legion/.cache/online/compose.prod.yml \
    -f /root/little-legion/.cache/online/compose.rollback.yml \
    --profile game "$@"
}
```

Si ese bloque terminó sin errores y se confirmó compatibilidad de esquema:

```bash
dcr up -d --no-build --no-deps --force-recreate --wait --wait-timeout 120 nakama
```

Solo después del healthcheck correcto:

```bash
dcr up -d --no-build --no-deps --force-recreate server caddy
dcr ps -a
```

Usar `dcr` mientras esté activa esta recuperación; un `dc up` podría volver a
seleccionar las imágenes nuevas. No reconstruir durante el rollback. Para una
nueva actualización, volver explícitamente a la rama registrada en `branch.txt`
y seguir el paso 7 con `dc`. El build en `Builds/Server` puede ser de la versión
fallida: transferir el artefacto de la versión que se vaya a reconstruir.

## 9. Recuperar datos desde un backup

Una recuperación reemplaza el estado por el del respaldo y puede excluir cuentas
o resultados posteriores. Reservarla para un incidente de datos, no para resolver
un error de compilación. Conservar primero un dump del estado actual si es legible.

Con `LL_BACKUP` apuntando al respaldo elegido, restaurar primero en una base nueva:

```bash
LL_RECOVERY_DB="ll_recovery_$(date +%s)"
(
  set -euo pipefail
  (cd "$LL_BACKUP" && sha256sum -c SHA256SUMS)
  dc exec -T postgres createdb -U nakama "$LL_RECOVERY_DB"
  dc exec -T postgres pg_restore --exit-on-error --no-owner --no-privileges \
    -U nakama -d "$LL_RECOVERY_DB" < "$LL_BACKUP/nakama.dump"
  dc exec -T postgres psql -X -A -t -v ON_ERROR_STOP=1 -U nakama -d "$LL_RECOVERY_DB" \
    < "$LL_BACKUP/verify.sql" > "$LL_BACKUP/recovery-check.txt"
  diff -u "$LL_BACKUP/before.txt" "$LL_BACKUP/recovery-check.txt"
)
```

No apuntar la aplicación a esa base si falla alguna comprobación. Para activar la
recuperación, entrar en mantenimiento, detener los procesos que usan PostgreSQL
y conservar la base actual con otro nombre. El siguiente paso es una sustitución
del estado de producción: hacerlo solo después de aceptar el punto de recuperación
y preparar las imágenes y claves compatibles con el backup.

```bash
LL_OLD_DB="ll_before_recovery_$(date +%s)"
dc stop caddy server nakama
dc exec -T postgres psql -X -v ON_ERROR_STOP=1 -U nakama -d postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('nakama','$LL_RECOVERY_DB') AND pid<>pg_backend_pid();"
```

Aplicar los renombres solo si las dos bases existen y se completó la restauración:

```bash
(
  set -euo pipefail
  dc exec -T postgres psql -X -v ON_ERROR_STOP=1 -U nakama -d postgres -c \
    "ALTER DATABASE nakama RENAME TO $LL_OLD_DB;"
  dc exec -T postgres psql -X -v ON_ERROR_STOP=1 -U nakama -d postgres -c \
    "ALTER DATABASE $LL_RECOVERY_DB RENAME TO nakama;"
)
```

Si el segundo renombre falla, mantener mantenimiento y recuperar el nombre
`nakama` desde `LL_OLD_DB`; no borrar ninguna base. Arrancar las imágenes elegidas
con `--no-deps --no-build`, esperando a Nakama antes de Unity/Caddy como en el
rollback, y repetir las verificaciones. Conservar la base anterior hasta cerrar
el incidente. Si se perdió todo el VPS, restaurar además `.cache/online` antes
de iniciar servicios y recuperar los artefactos de esa versión.

## 10. Operación y problemas frecuentes

```bash
dc ps -a
dc logs --since=15m --tail=200 nakama server caddy
docker stats --no-stream
df -h
docker system df
```

| Síntoma | Comprobación y acción |
| --- | --- |
| `couldn't find env file` | Instalación nueva: ejecutar `init`. Instalación con datos: recuperar el env existente, no inventar nuevas credenciales |
| `compose.prod.yml: no such file` | Crear ambos archivos del paso 3; comprobar el directorio de trabajo |
| `unknown tag !override` | Actualizar Docker Compose a una versión compatible; no eliminar el override porque podría dejar 7350 expuesto |
| `FrontierServer` ausente durante el build | Transferir el directorio Linux completo desde la Mac antes del build Docker |
| `exec format error` | Confirmar servidor Ubuntu amd64 y artefacto ELF Linux x86-64; no copiar la app macOS |
| `migrate Exited (0)` | Estado normal de migración completada |
| `migrate Exited (1)` o Nakama no saludable | Leer logs de migración/Nakama; verificar claves y base existente, sin resetear el volumen |
| `ping: Unknown host` | Comparar DNS local y públicos; puede ser caché negativa tras crear el registro |
| HTTPS falla | Revisar DNS A/AAAA, puertos 80/443, reloj del host y logs de Caddy; no desactivar validación del certificado |
| HTTP 502 de Caddy | Revisar salud de Nakama, red Docker y destino `nakama:7350` |
| Encuentra rival pero no conecta | Confirmar host anunciado `50.6.197.145`, UDP 7778 en proveedor/host y registro del servidor; TCP/HTTPS no validan UDP |
| Espera servidor | Revisar slot, heartbeat, compatibilidad de versiones y partida asignada; inicialmente solo existe un slot |
| Parece actualizado pero usa código viejo | `git pull` no reemplaza contenedores; `restart` tampoco cambia sus imágenes. Reconstruir y recrear según el paso 7 |

Que `ps` muestre `7777/udp`, `5432/tcp` o `2019/tcp` sin una flecha de publicación
no significa que estén abiertos en el host; puede ser metadata `EXPOSE`. El puerto
de partidas publicado por este stack es `0.0.0.0:7778->7778/udp`. El stack LAN
de desarrollo usa 7777 por separado y no se necesita aquí.

Para administrar Nakama sin publicar su consola, desde la Mac:

```bash
ssh -N -L 17351:127.0.0.1:7351 root@50.6.197.145
```

Abrir `http://127.0.0.1:17351`; usuario `admin`, contraseña guardada en el env
privado del servidor. No copiar esa contraseña a tickets o logs compartidos.

Conservar backups fuera del host, vigilar disco y definir retención de logs y
respaldos según el uso. Las imágenes base de esta instalación usan tags de
versión; una actualización de esas dependencias debe probarse explícitamente.
No usar `down -v`, `docker volume prune` ni borrar `.cache/online` como parte de
una actualización. La prueba física de clientes sigue siendo independiente de
las comprobaciones de Docker y PostgreSQL.
