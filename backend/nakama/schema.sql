-- Independent application tables; Nakama owns its own account/session schema.
CREATE TABLE IF NOT EXISTS ll_searches (
  id TEXT PRIMARY KEY, player_id TEXT NOT NULL, faction TEXT NOT NULL,
  protocol INTEGER NOT NULL, rules INTEGER NOT NULL, content_hash TEXT NOT NULL,
  status TEXT NOT NULL, message TEXT NOT NULL DEFAULT '', match_id TEXT,
  queue_session TEXT NOT NULL DEFAULT '', created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  seen_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ll_one_search_per_player ON ll_searches(player_id)
  WHERE status IN ('searching', 'waitingServer', 'preparing', 'ready');
CREATE TABLE IF NOT EXISTS ll_servers (
  id TEXT PRIMARY KEY, boot_id TEXT NOT NULL, lease_id TEXT NOT NULL,
  host TEXT NOT NULL, port INTEGER NOT NULL, protocol INTEGER NOT NULL,
  rules INTEGER NOT NULL, content_hash TEXT NOT NULL, active BOOLEAN NOT NULL DEFAULT TRUE,
  heartbeat_at TIMESTAMPTZ NOT NULL DEFAULT now(), match_id TEXT
);
CREATE TABLE IF NOT EXISTS ll_matches (
  id TEXT PRIMARY KEY, status TEXT NOT NULL, map_id TEXT NOT NULL DEFAULT 'rift',
  protocol INTEGER NOT NULL, rules INTEGER NOT NULL, content_hash TEXT NOT NULL,
  server_id TEXT, lease_id TEXT, reason TEXT NOT NULL DEFAULT '',
  winner INTEGER NOT NULL DEFAULT 0, tick BIGINT NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL DEFAULT (now() + interval '10 minutes')
);
CREATE INDEX IF NOT EXISTS ll_matches_pending ON ll_matches(created_at) WHERE status='waitingServer';
-- Browser clients join over websocket. Additive columns keep existing rows valid:
-- old servers advertise no websocket port; old searches never require one.
ALTER TABLE ll_servers ADD COLUMN IF NOT EXISTS ws_port INTEGER NOT NULL DEFAULT 0;
ALTER TABLE ll_searches ADD COLUMN IF NOT EXISTS websocket BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE ll_matches ADD COLUMN IF NOT EXISTS websocket BOOLEAN NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS ll_players (
  match_id TEXT NOT NULL REFERENCES ll_matches(id), player_id TEXT NOT NULL,
  search_id TEXT NOT NULL REFERENCES ll_searches(id), seat INTEGER NOT NULL CHECK (seat IN (0,1)),
  faction TEXT NOT NULL CHECK (faction IN ('industrial','biological')), ticket_hash TEXT NOT NULL,
  claim_request TEXT, claimed_at TIMESTAMPTZ,
  PRIMARY KEY (match_id,player_id), UNIQUE(match_id,seat), UNIQUE(search_id)
);
