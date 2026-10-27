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
CREATE TABLE IF NOT EXISTS ll_players (
  match_id TEXT NOT NULL REFERENCES ll_matches(id), player_id TEXT NOT NULL,
  search_id TEXT NOT NULL REFERENCES ll_searches(id), seat INTEGER NOT NULL CHECK (seat IN (0,1)),
  faction TEXT NOT NULL CHECK (faction IN ('industrial','biological')), ticket_hash TEXT NOT NULL,
  claim_request TEXT, claimed_at TIMESTAMPTZ,
  PRIMARY KEY (match_id,player_id), UNIQUE(match_id,seat), UNIQUE(search_id)
);

-- Existing matches remain unranked. Only newly formed pairs opt in explicitly.
ALTER TABLE ll_matches ADD COLUMN IF NOT EXISTS rank_version INTEGER NOT NULL DEFAULT 0;
ALTER TABLE ll_searches ADD COLUMN IF NOT EXISTS rating_at_search INTEGER NOT NULL DEFAULT 1000;
CREATE TABLE IF NOT EXISTS ll_ratings (
  player_id TEXT PRIMARY KEY, rating INTEGER NOT NULL DEFAULT 1000 CHECK (rating >= 0),
  games INTEGER NOT NULL DEFAULT 0, wins INTEGER NOT NULL DEFAULT 0,
  losses INTEGER NOT NULL DEFAULT 0, draws INTEGER NOT NULL DEFAULT 0,
  version BIGINT NOT NULL DEFAULT 0, updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CHECK (games = wins + losses + draws)
);
CREATE TABLE IF NOT EXISTS ll_rating_results (
  match_id TEXT NOT NULL, player_id TEXT NOT NULL,
  rating_before INTEGER NOT NULL, rating_after INTEGER NOT NULL,
  delta INTEGER NOT NULL, opponent_rating INTEGER NOT NULL,
  PRIMARY KEY (match_id, player_id),
  FOREIGN KEY (match_id, player_id) REFERENCES ll_players(match_id, player_id)
);
-- A coalescing durable projection queue; the canonical rating and the terminal
-- match commit atomically. Nakama's public leaderboard is an absolute SET view.
CREATE TABLE IF NOT EXISTS ll_rating_outbox (
  player_id TEXT PRIMARY KEY REFERENCES ll_ratings(player_id),
  version BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
