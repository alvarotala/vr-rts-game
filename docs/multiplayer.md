# NGO dedicated multiplayer

The first multiplayer deliverable uses Unity Netcode for GameObjects and Unity Transport, with one authoritative server and two player clients. The existing offline game continues to run its local simulation and AI.

The server runs the existing simulation at 20 ticks/second. Clients send orders and receive their filtered `PlayerView`; they never receive the active opponent's private economy, production queues or complete authoritative simulation. Seat ownership comes from the server's connection mapping. Reliable command acknowledgements preserve the original outcome for duplicate sequences. State frames are compressed, bounded, fragmented and published only after complete assembly. Final statistics are sent after the game ends. If that payload exceeds the final-message limits (8 MiB expanded or 2 MiB compressed), the server delivers the terminal player view with an explicit report-unavailable flag. The outcome remains readable even when statistics cannot be delivered.

This deliverable connects directly to a server address. Public matchmaking, accounts, ranking, cloud allocation and reconnecting into a running match are subsequent work. A disconnected player concedes; the other player receives the result. Opening a client's menu does not pause the server.

## Build

Use the project's Unity version from `ProjectSettings/ProjectVersion.txt`, currently **6000.3.26f1**. The desktop client uses the configured project. The Linux server builder stages only Core, Networking and map/catalog resources in a separate generated project because Meta XR SDK 203's build preprocessor rejects Linux targets. Both builds use the same simulation and protocol sources, **NGO 2.13.3** and **Unity Transport 2.7.4**. The server generator copies the actual resolved client package graph from `Packages/packages-lock.json`; it does not substitute NGO's older minimum transport dependency.

```sh
unity build . --target StandaloneOSX --execute-method Frontier.Editor.ProjectBuilder.BuildDesktop --output-path Builds/Desktop/FrontierCommand.app --log-file TestResults/multiplayer-desktop-build.log --format json
python3 scripts/build-server.py
```

The Linux build requires **Linux Dedicated Server Build Support** for the same Editor. The current generator explicitly supports **macOS ARM64 build hosts** and produces a **Linux amd64 IL2CPP** server. It pins `com.unity.toolchain.macos-arm64-linux`, `com.unity.sdk.linux-x86_64` and `com.unity.sysroot.base` to **1.1.0** in the generated project only; the main client manifest is unchanged by these cross-compilation dependencies. Building on a different host requires an explicit generator/toolchain adaptation.

The script automatically fetches these public compiler archives from Unity's official package registry, verifies each archive against the registry's integrity hashes, and caches it under `.cache/server-packages/`. The generated manifest references the verified local archives with `file:` dependencies. Cached archives are verified again before reuse; checking the current registry metadata still requires network access. This package-download path does not require a Unity account login. The Unity Editor's normal build license remains required.

`build-server.py` regenerates `.cache/dedicated-server`, explicitly chooses Unity's Server subtarget, and writes `Builds/Server/FrontierServer`. Its staging project excludes XR, Meta and presentation dependencies; source edits still happen in the main project. `--prepare-only` includes downloading/verifying the compiler archives and preparing the project, but does not start Unity. `--output` changes the build destination. Build logs are `TestResults/dedicated-server-build.log`, `.cli.json` and `.stderr.log`. Successful completion must include `FRONTIER_DEDICATED_BUILD passed` and the Editor's `FRONTIER_SERVER_BUILT ... backend=IL2CPP` marker.

A native macOS dedicated server uses **Mac Dedicated Server Build Support** and the Server subtarget:

```sh
unity build . --target StandaloneOSX --execute-method Frontier.Editor.MultiplayerValidation.BuildMacServer --output-path Builds/ServerMac/FrontierServer.app --log-file TestResults/multiplayer-server-mac-build.log --format json
```

If that module is unavailable, `BuildMacHeadlessServer` builds a normal macOS player in `Builds/HeadlessMac/FrontierServer.app`, launched with `-batchmode -nographics -frontier-server`. This fallback runs only the dedicated server role but does not validate Unity's Server subtarget. A macOS process check does not validate Linux execution.

## Docker server

Build the Linux server above first, then package and start it:

```sh
docker compose -f compose.multiplayer.yml up --build -d
docker compose -f compose.multiplayer.yml logs -f server
```

Wait for `FRONTIER_SERVER Listening on`. Compose publishes UDP 7777 on all host interfaces (`0.0.0.0`) by default. Connect desktop clients on this Mac to `127.0.0.1:7777`. From another machine or Quest on the same LAN, connect to the Mac's LAN IP; `0.0.0.0` is a listening address, not a destination. To restrict access to this Mac instead:

```sh
FRONTIER_BIND_ADDRESS=127.0.0.1 docker compose -f compose.multiplayer.yml up -d
```

`FRONTIER_PORT` changes the UDP port (default `7777`), `FRONTIER_MAP` chooses `rift`, `training` or `desert`, and `FRONTIER_SERVER_IMAGE` changes the local image tag (default `frontier-command-server:ngo-local`). Use the same variables when recreating the service. One server process runs one match; use `docker compose -f compose.multiplayer.yml restart server` for another match after both players have left. `docker compose -f compose.multiplayer.yml down` removes this local deployment.

The image contains the compiled Linux server and runtime libraries, runs as user `frontier` (UID 10001), writes logs to standard output, and exposes UDP rather than TCP. It contains no Unity Editor or license. Ubuntu 22.04 is within Unity 6.3's supported Linux server OS list. [Unity server system requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html#server)

Compose pins `linux/amd64`: Docker Desktop supports this platform through emulation on Apple Silicon, and an amd64 Linux machine can run the same image natively. Emulated performance is not a production capacity measurement. Preserve the image verified by the runtime checks for later promotion. No production deployment or registry push is part of these commands. [Docker platform and emulation documentation](https://docs.docker.com/build/building/multi-platform/)

The initial Mono server build crashed during startup under QEMU on this Mac. The generated Linux server uses IL2CPP to remove the Mono JIT from that execution path. The final IL2CPP image completed both the healthy and impaired-network matches with two independent macOS clients; both clients agreed on each complete report, as recorded below.

### Promote the same image later

After the local runtime checks pass, export the tested image and record its identity:

```sh
docker image inspect frontier-command-server:ngo-local --format '{{.Id}} {{.Os}}/{{.Architecture}}'
docker save --output Builds/frontier-command-server.tar frontier-command-server:ngo-local
```

Copy that archive, the recorded image identity and `compose.multiplayer.yml` to the chosen Linux amd64 host. Loading restores the saved image and tag; it does not rebuild the application. [Docker save](https://docs.docker.com/reference/cli/docker/image/save/), [Docker load](https://docs.docker.com/reference/cli/docker/image/load/)

```sh
docker load --input frontier-command-server.tar
docker image inspect frontier-command-server:ngo-local --format '{{.Id}} {{.Os}}/{{.Architecture}}'
```

Confirm that the image ID and `linux/amd64` platform match the tested artifact, then start it with the normal Compose file:

```sh
FRONTIER_BIND_ADDRESS=0.0.0.0 docker compose -f compose.multiplayer.yml up -d --no-build --pull never
docker compose -f compose.multiplayer.yml logs -f server
```

Allow inbound **UDP 7777** on that host's firewall and network rules, or set `FRONTIER_PORT` consistently if selecting another port. `--no-build --pull never` uses the loaded artifact. The normal Compose file runs real-time matches; omit the smoke override. Each process serves one match, so restart the service after completion for the next match. This is a future deployment procedure; no production host has been deployed or validated. [Compose startup options](https://docs.docker.com/reference/cli/docker/compose/up/)

## Run a match

Start the server, then connect two clients through the multiplayer menu. Use the server machine's LAN address from another machine; `127.0.0.1` only connects to a server on the same machine. Allow the selected UDP port through the server's firewall.

The Linux executable below runs directly on a Linux host. On the authoring Mac, run that executable inside Docker using the preceding instructions.

```sh
Builds/Server/FrontierServer -batchmode -nographics -frontier-server -frontier-port 7777 -logFile server.log
```

For the macOS server substitute `Builds/ServerMac/FrontierServer.app/Contents/MacOS/Frontier Command` (check the executable's actual name inside the app). Desktop clients also accept command-line connection parameters:

```sh
"Builds/Desktop/FrontierCommand.app/Contents/MacOS/Frontier Command" -frontier-client 127.0.0.1 -frontier-port 7777 -frontier-faction industrial
```

Both clients must connect before the match starts. A normal server has no AI filling an empty seat. The match ends through the existing victory rules or concession, and both clients receive the same authoritative result.

## Reproducible process check

For three native macOS processes, build the optional macOS server and run:

```sh
python3 scripts/check-multiplayer.py --server Builds/ServerMac/FrontierServer.app
```

Use the external-server commands below to test the Linux Docker image with the macOS clients. `--server` starts a local executable directly and cannot run a Linux ELF binary on macOS.

The runner starts and cleans up its own three processes. It launches its server with `-frontier-server -frontier-smoke -frontier-port PORT`, waits up to 30 seconds for the log substring `FRONTIER_SERVER Listening on`, then launches the clients. The readiness match accepts the address/port and any other suffix after that text. Its clients use `Strategist` on their own filtered views and send every action through NGO. The opt-in `-frontier-smoke` mode accelerates the server simulation for this check; normal matches always use real-time pacing. A scripted concession bounds a strategic stalemate after economy, construction and combat have occurred.

To validate the Linux image running in Docker, enable acceleration with a Compose override, then use the external-server mode. The runner owns only the two client processes in this mode and leaves the container running. External mode does not read container logs or perform a UDP readiness probe: confirm a fresh `Listening on` line before launching the clients.

```sh
docker compose -f compose.multiplayer.yml -f compose.multiplayer.smoke.yml up --build -d --force-recreate
docker compose -f compose.multiplayer.yml logs server
python3 scripts/check-multiplayer.py --external-server 127.0.0.1 --port 7777
```

After the healthy baseline, restart the finished server and run a delayed, lossy connection through the harness's local UDP proxy:

```sh
docker compose -f compose.multiplayer.yml -f compose.multiplayer.smoke.yml restart server
python3 scripts/check-multiplayer.py --external-server 127.0.0.1 --port 7777 --latency-ms 60 --jitter-ms 20 --loss-percent 1 --output TestResults/ngo-multiplayer-impaired
```

Wait for the server's new `Listening on` line before starting clients. The proxy adds 60 ms in each direction (roughly 120 ms base RTT), uniform ±20 ms jitter and 1% datagram loss in each direction. It uses a fixed random seed, bounded queues, one upstream socket per client and no system firewall/network changes. The report records observed datagram counts and the requested profile; this is synthetic adversity, not a claim about every real internet connection. Restore normal pacing for human play with `docker compose -f compose.multiplayer.yml up -d --force-recreate`.

The check requires distinct seats, monotonic filtered views, accepted construction/training orders, idempotent duplicate acknowledgements, completed production/buildings and army casualties in the final report. It deliberately supplies the wrong player field in commands to verify that the server uses connection ownership. Both processes must exit successfully and agree on winner, final tick and the SHA-256 of the full report. Evidence goes to `TestResults/ngo-multiplayer/`, including `result.json`; success prints `FRONTIER_MULTIPLAYER_PASSED`.

Passing this check proves the local process path for the binaries used. It does not prove Quest hardware behavior, Linux execution when the macOS server is used, or responsiveness over a degraded internet connection. `Frontier.Editor.MultiplayerValidation.RunSessionChecks` separately validates remote player-one selection, preview visibility, interpolation, asynchronous feedback, menu pauses, and switching between offline play, remote views and saved offline replay. Those adapter checks inject decoded views and do not imply network or player-build evidence.

`Frontier.Editor.MultiplayerValidation.RunRegressionChecks` combines these adapter checks with online placement checks and the existing hotkey, menu, archive and replay validations. Run it in a graphics-capable Editor process because the existing UI checks render previews.

## Validation evidence

Local validation on 2026-10-21 used image `sha256:d1c5db345f01265ad4e41fed7ef8e561bb54ab909647058532460db1c212ca8c` (`linux/amd64`). The same image completed both matches. Afterwards the container was recreated with normal pacing. Compose now defaults to `0.0.0.0:7777/udp` for LAN/Quest access; the original two-client checks used the loopback endpoint.

| Check | Status | Evidence |
| --- | --- | --- |
| macOS desktop client build | Passed; Unity CLI exited 0 with no reported Editor errors | `TestResults/ngo-desktop-cli.log`, `TestResults/ngo-desktop-build.log` |
| Offline desktop player smoke | Passed; zero errors, menu start, worker movement, board picking and menu placement | `TestResults/Desktop/smoke-result.json`, `TestResults/desktop-player.log`: `FRONTIER_DESKTOP_SMOKE 0 errors` |
| Quest Android APK build | Passed; APK compiled, no physical headset check implied | `TestResults/ngo-quest-cli.log`, `FRONTIER_APK_BUILT` in `TestResults/ngo-quest-build.log` |
| Linux amd64 IL2CPP dedicated build | Passed; Unity Server subtarget, IL2CPP, x86-64 ELF | `TestResults/dedicated-server-build.log`: `FRONTIER_SERVER_BUILT ... backend=IL2CPP` |
| Docker image and server startup | Passed; Linux amd64 IL2CPP server listening on UDP 7777 | `TestResults/ngo-docker-image.txt`, `TestResults/ngo-docker-startup.log` |
| Mac LAN port publication | Passed; host binding `0.0.0.0:7777/udp` and real NGO handshake from the Mac to its Wi-Fi IP; Quest traffic not yet checked | `TestResults/ngo-lan-binding.json`, `TestResults/ngo-lan-client.log` |
| Docker server + two clients, healthy connection | Passed; winner seat 0, tick 9740; matching full report SHA-256 `359b99d6c60fe13cad7618d6102c1292a00de4bad1d1dbc8584a8fe91832d33e` | `TestResults/ngo-multiplayer/result.json` |
| Same image, 60 ms one-way delay, ±20 ms jitter, 1% loss | Passed; winner seat 1, tick 13473; matching full report SHA-256 `902e69ab0f3d55daaaac53738b42f8237c17bef26d1a132e19da77ba62e0fff4`; 3973 received datagrams, 38 dropped, zero queue overflow | `TestResults/ngo-multiplayer-impaired/result.json` |
| Core simulation regression suite | Passed: 2103 checks | `TestResults/ngo-core.log` |
| Wire framing, compression and command boundary checks | Passed: 2147 total checks, including 2123 wire checks | `TestResults/ngo-network-core.log` |
| Session, placement, offline input and replay regressions | Passed: full regression suite; 14 placement checks, 134 hotkey checks, menu/archive/replay checks | `TestResults/ngo-regressions.log`: `FRONTIER_MULTIPLAYER_REGRESSION passed` |
| Unity result fallback and final session adapter | Passed: 31 session checks including 4 result-fallback checks using Unity's actual JSON decoder and the client's result restoration | `TestResults/multiplayer-result-fallback-final.log`: `FRONTIER_MULTIPLAYER_RESULT_FALLBACK passed` and `FRONTIER_MULTIPLAYER_SESSION passed` |

Quest hardware validation and production deployment are separate from these local checks.
