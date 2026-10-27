package main

import (
	"context"
	"database/sql"
	"github.com/heroiclabs/nakama-common/runtime"
)

type searchRequest struct {
	Faction     string `json:"faction"`
	Protocol    int    `json:"protocol"`
	Rules       int    `json:"rules"`
	ContentHash string `json:"contentHash"`
	// Browser clients cannot use UDP; their pair needs a server with a websocket listener.
	Websocket bool `json:"websocket"`
}
type searchIDRequest struct {
	SearchID string `json:"searchId"`
}

func searchOpen(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req searchRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	if !validFaction(req.Faction) || !validCompatibility(req.Protocol, req.Rules, req.ContentHash) {
		return nil, invalid("Invalid faction or compatibility")
	}
	var active string
	err := tx.QueryRowContext(ctx, `SELECT id FROM ll_searches WHERE player_id=$1 AND status IN ('searching','waitingServer','preparing','ready')`, user).Scan(&active)
	if err == nil {
		return nil, conflict("A search or match is already active")
	}
	if err != sql.ErrNoRows {
		return nil, err
	}
	id := token()
	_, err = tx.ExecContext(ctx, `INSERT INTO ll_searches(id,player_id,faction,protocol,rules,content_hash,status,websocket) VALUES($1,$2,$3,$4,$5,$6,'searching',$7)`, id, user, req.Faction, req.Protocol, req.Rules, req.ContentHash, req.Websocket)
	return map[string]string{"searchId": id}, err
}

type connection struct {
	MatchID    string `json:"matchId"`
	Host       string `json:"host"`
	Port       int    `json:"port"`
	WsPort     int    `json:"wsPort"`
	JoinTicket string `json:"joinTicket"`
	Faction    string `json:"faction"`
}
type searchResult struct {
	Status     string      `json:"status"`
	Message    string      `json:"message"`
	Connection *connection `json:"connection,omitempty"`
}

func searchStatus(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req searchIDRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	var result searchResult
	var matchID string
	err := tx.QueryRowContext(ctx, `UPDATE ll_searches SET seen_at=now() WHERE id=$1 AND player_id=$2 RETURNING status,message,COALESCE(match_id,'')`, req.SearchID, user).Scan(&result.Status, &result.Message, &matchID)
	if err == sql.ErrNoRows {
		return nil, denied("Unknown search")
	}
	if err != nil {
		return nil, err
	}
	if result.Status == "ready" {
		var conn connection
		var state string
		err = tx.QueryRowContext(ctx, `SELECT m.id,m.status,s.host,s.port,s.ws_port,p.faction FROM ll_matches m JOIN ll_servers s ON s.id=m.server_id AND s.lease_id=m.lease_id JOIN ll_players p ON p.match_id=m.id WHERE m.id=$1 AND p.player_id=$2 AND s.active`, matchID, user).Scan(&conn.MatchID, &state, &conn.Host, &conn.Port, &conn.WsPort, &conn.Faction)
		if err != nil {
			return nil, err
		}
		if state == "ready" {
			conn.JoinTicket = joinTicket(matchID, user)
			result.Connection = &conn
		}
	}
	return result, nil
}

func searchCancel(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req searchIDRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	var state, matchID string
	err := tx.QueryRowContext(ctx, `SELECT status,COALESCE(match_id,'') FROM ll_searches WHERE id=$1 AND player_id=$2`, req.SearchID, user).Scan(&state, &matchID)
	if err == sql.ErrNoRows {
		return nil, denied("Unknown search")
	}
	if err != nil {
		return nil, err
	}
	if state == "cancelled" || state == "failed" || state == "finished" {
		return ok(), nil
	}
	if matchID != "" {
		var matchState string
		if err = tx.QueryRowContext(ctx, `SELECT status FROM ll_matches WHERE id=$1`, matchID).Scan(&matchState); err != nil {
			return nil, err
		}
		if matchState == "playing" || matchState == "finished" {
			return ok(), nil
		}
		if err = terminal(ctx, tx, matchID, "aborted", "Search cancelled", 0, 0); err != nil {
			return nil, err
		}
	}
	_, err = tx.ExecContext(ctx, `UPDATE ll_searches SET status='cancelled',message='Search cancelled' WHERE id=$1`, req.SearchID)
	return ok(), err
}
