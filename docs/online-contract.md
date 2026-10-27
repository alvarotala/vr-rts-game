# Self-hosted online allocation contract

Nakama owns account sessions and the native two-player matchmaker. A small Go runtime module owns an explicit pool of NGO dedicated servers, admission and match lifecycle in PostgreSQL. Unity simulation, offline play and direct LAN mode remain separate.

All JSON fields below use camelCase. Player RPCs require Nakama JWT authentication. Server RPCs require Nakama's private runtime HTTP key and reject user-authenticated requests. Server keys never enter the client builds. HTTP is for explicitly configured local development; public deployment uses HTTPS/WSS.

## Player flow

- AuthenticateDevice using a random per-installation credential, stored locally, not a hardware identifier.
- `ll_search_open`: `{faction,protocol,rules,contentHash,websocket?}` -> `{searchId}`. One nonterminal search per player; expire abandoned searches. Desktop (browser) clients send `websocket: true`; Quest clients omit it.
- Native websocket AddMatchmaker: min/max 2, query matching exact `compat` property. String properties: `compat` (protocol:rules:contentHash), `searchId`. Backend matched hook derives user identity from Nakama presence and faction/compatibility from the saved search, never the supplied properties alone.
- `ll_search_status`: `{searchId}` -> `{status,message,connection?}`. Status: searching, waitingServer, preparing, ready, cancelled, failed, finished. Ready connection is `{matchId,host,port,wsPort,joinTicket,faction}`. `port` is the NGO UDP port; `wsPort` is the server's websocket TCP port (0 when it has none). Only the owning player gets its short-lived, match-scoped join ticket. No player-account JWT goes over NGO.
- `ll_search_cancel`: `{searchId}` -> `{ok:true}`. Idempotent; invalidates a pending search and aborts a pre-start allocation, including races with native matchmaking. Never abort an already started game through this RPC.
- A search closes its websocket after handoff. A server-reported started match commits that handoff; client cancellation before successful connection still cancels its search.

## Dedicated server flow

Server RPCs use `/v2/rpc/NAME?http_key=PRIVATE_KEY&unwrap` with a JSON body.

- `ll_server_register`: `{serverId,bootId,host,port,wsPort?,protocol,rules,contentHash}` -> `{leaseId}`. A new process incarnation invalidates old leases. Endpoint comes only from trusted server configuration. `wsPort` (0–65535, default 0) is the websocket port advertised to browsers; an active process cannot change it.
- `ll_server_poll`: `{serverId,bootId,leaseId}` -> `{assignment?}`. Poll every 2 seconds. Assignment: `{matchId,mapId,status,expiresAtUnix,players:[{playerId,seat,faction}]}`. Two distinct seats, exactly two accounts. Status includes preparing, ready, playing, finished, aborted. Before-start arrival deadline 60 seconds; stale server lease 20 seconds.
- `ll_server_admit`: `{serverId,bootId,leaseId,matchId,joinTicket,requestId}` -> `{playerId,seat,faction}`. Verify ticket against active assignment; claim once with idempotent retries using the same requestId. Server reserves no NGO seat until this succeeds and the pending connection is still current.
- `ll_server_event`: `{serverId,bootId,leaseId,matchId,event,winner,tick,reason}` -> `{ok:true}`. Events: ready, started, finished, aborted, released. Result is server-only, idempotent, winner 0/1/2 and simulation tick. Ready enables endpoint delivery. Release is separate from finish so a server cannot be assigned while players still read their result. Do not release until a terminal event has been acknowledged.

An allocated NGO server refuses all unassigned connections. Hello adds optional `matchId` and `joinTicket`; direct LAN calls retain their existing signature and admission path. NGO approval has a 10-second transport deadline, so backend claim attempts are bounded to 5 seconds. Pending requests are invalidated on disconnect/reset/assignment changes. Allocated mode aborts pre-start departures/no-shows and closes terminal connections after a bounded result-reading period, then releases and returns to polling.

Pool allocation is transactional and compatibility-matched. A pair that includes a `websocket` search is only allocated to a server with `wsPort > 0`; Quest-only pairs may use any compatible server. Both transports use the same join tickets and admission RPC. No Docker socket is exposed to Nakama. The initial Compose stack has one NGO slot; extra pairs wait for a free slot. PostgreSQL data is persistent and migrations are non-destructive.
