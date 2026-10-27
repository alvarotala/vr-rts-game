# Self-hosted online allocation contract

Nakama owns account sessions, the native two-player skill matchmaker and the authoritative public leaderboard. A small Go runtime module owns an explicit pool of NGO dedicated servers, admission, match lifecycle and canonical Elo ratings in PostgreSQL. Unity simulation, offline play and direct LAN mode remain separate and do not change ranking.

All JSON fields below use camelCase. Player RPCs require Nakama JWT authentication. Server RPCs require Nakama's private runtime HTTP key and reject user-authenticated requests. Server keys never enter the client builds. HTTP is for explicitly configured local development; public deployment uses HTTPS/WSS.

## Player flow

- AuthenticateDevice using a random per-installation credential, stored locally, not a hardware identifier.
- `ll_search_open`: `{faction,protocol,rules,contentHash}` -> `{searchId,rating,searchRadius,queueRevision}`. One nonterminal search per player; expire abandoned searches. Rating is snapshotted from the authenticated player's canonical profile, never supplied by the client.
- Native websocket AddMatchmaker: min/max 2. String properties: `compat` (protocol:rules:contentHash), `searchId`. The before hook replaces the query with exact compatibility plus the server's numeric rating range and sets the authoritative numeric `rating` property. Client numeric properties and widening claims are not trusted. The matched hook derives identity from presence and revalidates saved compatibility, rating and both windows.
- `ll_search_status`: `{searchId}` -> `{status,message,rating,searchRadius,queueRevision,connection?}`. Status: searching, waitingServer, preparing, ready, cancelled, failed, finished. Ready connection is `{matchId,host,port,joinTicket,faction}`. Only the owning player gets its short-lived, match-scoped join ticket. No player-account JWT goes over NGO.
- `ll_search_cancel`: `{searchId}` -> `{ok:true}`. Idempotent; invalidates a pending search and aborts a pre-start allocation, including races with native matchmaking. Never abort an already started game through this RPC.
- A search closes its websocket after handoff. A server-reported started match commits that handoff; client cancellation before successful connection still cancels its search.

The initial skill window is ±100 Elo. Each 20 seconds of PostgreSQL-measured
search age adds 100, up to ±400. `queueRevision` is 0–3 and equals
`searchRadius / 100 - 1`. While still `searching`, clients remove their previous
native ticket and add a replacement when the revision changes; they retain the
same `searchId` and original search age. If matching wins the renewal race, the
client polls the assigned search rather than opening another one. Both windows
must permit the difference. Configure `matchmaker.rev_precision=true` and
`rev_threshold=0`; waiting for capacity does not widen an already formed pair.

## Player ranking reads

All three RPCs require a player JWT, derive identity from that session and reject
client-supplied rating or account identity. They never write match results.

- `ll_rank_profile`: `{}` -> `{playerId,displayName,rating,games,wins,losses,draws,rank,leaderboardId}`. New accounts have 1000 Elo, zero games and `rank=0`; only accounts with a rated match enter the table.
- `ll_rank_top`: `{}` -> `{entries:[{playerId,displayName,rating,rank,games,wins,losses,draws,isSelf}]}`. Returns at most ten records from Nakama's authoritative `ll_ranked_1v1_v1` leaderboard.
- `ll_rank_result`: `{matchId}` -> `{matchId,status,ratingBefore,ratingAfter,ratingDelta,opponentRating}`. Only participants may read their own immutable change. Status is `pending`, `rated` or `unrated`; numeric deltas are meaningful only when `rated`.

New public matches use `rank_version=1`, Elo K=32 and a floor of zero. Existing
matches retain `rank_version=0` and are never backfilled. A trusted `finished`
event commits both Elo changes, counters and per-player ledger rows atomically
with the terminal match result. Duplicate terminal events cannot score twice;
`aborted`, cancellation and no-show never score. See [ranking rules](ranking.md).

Nakama's leaderboard is an eventual projection of canonical `ll_ratings`.
`ll_rating_outbox` persists pending publication, workers serialize across
processes and write the latest absolute score using `set`. `subscore` is the
monotonic profile version (rated games), allowing draw statistics to update even
without a rating change and breaking equal-Elo ties by games played. A retry
after a crash or an old queued version cannot apply a stale increment. Clients
cannot write or delete leaderboard records; matchmaking reads the canonical Elo.

## Dedicated server flow

Server RPCs use `/v2/rpc/NAME?http_key=PRIVATE_KEY&unwrap` with a JSON body.

- `ll_server_register`: `{serverId,bootId,host,port,protocol,rules,contentHash}` -> `{leaseId}`. A new process incarnation invalidates old leases. Endpoint comes only from trusted server configuration.
- `ll_server_poll`: `{serverId,bootId,leaseId}` -> `{assignment?}`. Poll every 2 seconds. Assignment: `{matchId,mapId,status,expiresAtUnix,players:[{playerId,seat,faction}]}`. Two distinct seats, exactly two accounts. Status includes preparing, ready, playing, finished, aborted. Before-start arrival deadline 60 seconds; stale server lease 20 seconds.
- `ll_server_admit`: `{serverId,bootId,leaseId,matchId,joinTicket,requestId}` -> `{playerId,seat,faction}`. Verify ticket against active assignment; claim once with idempotent retries using the same requestId. Server reserves no NGO seat until this succeeds and the pending connection is still current.
- `ll_server_event`: `{serverId,bootId,leaseId,matchId,event,winner,tick,reason}` -> `{ok:true}`. Events: ready, started, finished, aborted, released. Result is server-only, idempotent, winner 0/1/2 and simulation tick. A newly ranked match applies rating in the same `finished` transaction; the DTO does not accept rating values. Ready enables endpoint delivery. Release is separate from finish so a server cannot be assigned while players still read their result. Do not release until a terminal event has been acknowledged.

An allocated NGO server refuses all unassigned connections. Hello adds optional `matchId` and `joinTicket`; direct LAN calls retain their existing signature and admission path. NGO approval has a 10-second transport deadline, so backend claim attempts are bounded to 5 seconds. Pending requests are invalidated on disconnect/reset/assignment changes. Allocated mode aborts pre-start departures/no-shows and closes terminal connections after a bounded result-reading period, then releases and returns to polling.

Pool allocation is transactional and compatibility-matched. No Docker socket is exposed to Nakama. The initial Compose stack has one NGO slot; extra pairs wait for a free slot. PostgreSQL data is persistent and migrations are non-destructive.
