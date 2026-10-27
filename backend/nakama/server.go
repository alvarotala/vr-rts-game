package main

import (
	"context"
	"database/sql"
	"net"
	"strings"

	"github.com/heroiclabs/nakama-common/runtime"
)

type serverIdentity struct {
	ServerID string `json:"serverId"`
	BootID   string `json:"bootId"`
	LeaseID  string `json:"leaseId"`
}
type registerRequest struct {
	ServerID    string `json:"serverId"`
	BootID      string `json:"bootId"`
	Host        string `json:"host"`
	Port        int    `json:"port"`
	WsPort      int    `json:"wsPort"`
	Protocol    int    `json:"protocol"`
	Rules       int    `json:"rules"`
	ContentHash string `json:"contentHash"`
}
type serverState struct{ matchID string }

func serverRegister(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req registerRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	if !allowedServers[req.ServerID] || !safeID.MatchString(req.BootID) || len(req.Host) > 253 || strings.TrimSpace(req.Host) != req.Host || req.Host == "" || req.Host == "0.0.0.0" || req.Host == "::" || strings.ContainsAny(req.Host, "/\\\x00\r\n ") || req.Port < 1 || req.Port > 65535 || req.WsPort < 0 || req.WsPort > 65535 || !validCompatibility(req.Protocol, req.Rules, req.ContentHash) {
		return nil, invalid("Invalid registered server configuration")
	}
	if strings.Contains(req.Host, ":") && net.ParseIP(req.Host) == nil {
		return nil, invalid("Host must not include a port")
	}
	var boot, lease, matchID, host, hash string
	var port, wsPort, protocol, rules int
	var active bool
	err := tx.QueryRowContext(ctx, `SELECT boot_id,lease_id,COALESCE(match_id,''),active,host,port,ws_port,protocol,rules,content_hash FROM ll_servers WHERE id=$1`, req.ServerID).Scan(&boot, &lease, &matchID, &active, &host, &port, &wsPort, &protocol, &rules, &hash)
	if err != nil && err != sql.ErrNoRows {
		return nil, err
	}
	if err == nil && boot == req.BootID && active {
		if host != req.Host || port != req.Port || wsPort != req.WsPort || protocol != req.Protocol || rules != req.Rules || hash != req.ContentHash {
			return nil, conflict("An active process cannot change its endpoint or compatibility")
		}
		_, err = tx.ExecContext(ctx, `UPDATE ll_servers SET heartbeat_at=now() WHERE id=$1`, req.ServerID)
		return map[string]string{"leaseId": lease}, err
	}
	if matchID != "" {
		var state string
		if err = tx.QueryRowContext(ctx, `SELECT status FROM ll_matches WHERE id=$1`, matchID).Scan(&state); err != nil {
			return nil, err
		}
		if state != "finished" && state != "aborted" {
			if err = terminal(ctx, tx, matchID, "aborted", "Server restarted", 0, 0); err != nil {
				return nil, err
			}
		}
	}
	lease = token()
	_, err = tx.ExecContext(ctx, `INSERT INTO ll_servers(id,boot_id,lease_id,host,port,ws_port,protocol,rules,content_hash) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT(id) DO UPDATE SET boot_id=EXCLUDED.boot_id,lease_id=EXCLUDED.lease_id,host=EXCLUDED.host,port=EXCLUDED.port,ws_port=EXCLUDED.ws_port,protocol=EXCLUDED.protocol,rules=EXCLUDED.rules,content_hash=EXCLUDED.content_hash,active=true,heartbeat_at=now(),match_id=NULL`, req.ServerID, req.BootID, lease, req.Host, req.Port, req.WsPort, req.Protocol, req.Rules, req.ContentHash)
	return map[string]string{"leaseId": lease}, err
}

func authenticateServer(ctx context.Context, tx *sql.Tx, req serverIdentity) (serverState, error) {
	var state serverState
	err := tx.QueryRowContext(ctx, `UPDATE ll_servers SET heartbeat_at=now() WHERE id=$1 AND boot_id=$2 AND lease_id=$3 AND active RETURNING COALESCE(match_id,'')`, req.ServerID, req.BootID, req.LeaseID).Scan(&state.matchID)
	if err == sql.ErrNoRows {
		return state, denied("Server lease expired or replaced")
	}
	return state, err
}

type player struct {
	PlayerID string `json:"playerId"`
	Seat     int    `json:"seat"`
	Faction  string `json:"faction"`
}
type assignment struct {
	MatchID       string   `json:"matchId"`
	MapID         string   `json:"mapId"`
	Status        string   `json:"status"`
	ExpiresAtUnix int64    `json:"expiresAtUnix"`
	Players       []player `json:"players"`
}

func serverPoll(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req serverIdentity
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	state, err := authenticateServer(ctx, tx, req)
	if err != nil {
		return nil, err
	}
	if state.matchID == "" {
		if err = allocate(ctx, tx); err != nil {
			return nil, err
		}
		if err = tx.QueryRowContext(ctx, `SELECT COALESCE(match_id,'') FROM ll_servers WHERE id=$1`, req.ServerID).Scan(&state.matchID); err != nil {
			return nil, err
		}
	}
	if state.matchID == "" {
		return map[string]any{}, nil
	}
	var a assignment
	err = tx.QueryRowContext(ctx, `SELECT id,map_id,status,extract(epoch FROM expires_at)::bigint FROM ll_matches WHERE id=$1`, state.matchID).Scan(&a.MatchID, &a.MapID, &a.Status, &a.ExpiresAtUnix)
	if err != nil {
		return nil, err
	}
	rows, err := tx.QueryContext(ctx, `SELECT player_id,seat,faction FROM ll_players WHERE match_id=$1 ORDER BY seat`, a.MatchID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	for rows.Next() {
		var p player
		if err = rows.Scan(&p.PlayerID, &p.Seat, &p.Faction); err != nil {
			return nil, err
		}
		a.Players = append(a.Players, p)
	}
	if err = rows.Err(); err != nil {
		return nil, err
	}
	return map[string]any{"assignment": a}, nil
}

type admitRequest struct {
	serverIdentity
	MatchID    string `json:"matchId"`
	JoinTicket string `json:"joinTicket"`
	RequestID  string `json:"requestId"`
}

func serverAdmit(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req admitRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	if !safeID.MatchString(req.RequestID) || len(req.JoinTicket) != 43 {
		return nil, invalid("Invalid claim")
	}
	state, err := authenticateServer(ctx, tx, req.serverIdentity)
	if err != nil {
		return nil, err
	}
	if state.matchID != req.MatchID {
		return nil, denied("Ticket is not for this server assignment")
	}
	var status string
	var valid bool
	if err = tx.QueryRowContext(ctx, `SELECT status,expires_at>now() FROM ll_matches WHERE id=$1 AND lease_id=$2`, req.MatchID, req.LeaseID).Scan(&status, &valid); err != nil {
		return nil, err
	}
	var p player
	var claimed sql.NullString
	err = tx.QueryRowContext(ctx, `SELECT player_id,seat,faction,claim_request FROM ll_players WHERE match_id=$1 AND ticket_hash=$2`, req.MatchID, ticketHash(req.JoinTicket)).Scan(&p.PlayerID, &p.Seat, &p.Faction, &claimed)
	if err == sql.ErrNoRows {
		return nil, denied("Invalid join ticket")
	}
	if err != nil {
		return nil, err
	}
	if status != "ready" && status != "playing" {
		return nil, conflict("Assignment is not accepting players")
	}
	if claimed.Valid {
		if claimed.String == req.RequestID {
			return p, nil
		}
		return nil, denied("Join ticket already used")
	}
	if status != "ready" || !valid {
		return nil, denied("Join ticket expired")
	}
	_, err = tx.ExecContext(ctx, `UPDATE ll_players SET claim_request=$3,claimed_at=now() WHERE match_id=$1 AND player_id=$2`, req.MatchID, p.PlayerID, req.RequestID)
	return p, err
}

type eventRequest struct {
	serverIdentity
	MatchID string `json:"matchId"`
	Event   string `json:"event"`
	Winner  int    `json:"winner"`
	Tick    int64  `json:"tick"`
	Reason  string `json:"reason"`
}

func serverEvent(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req eventRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	if (req.Event == "finished" && (req.Winner < 0 || req.Winner > 2)) || req.Tick < 0 || len(req.Reason) > 512 {
		return nil, invalid("Invalid match result")
	}
	server, err := authenticateServer(ctx, tx, req.serverIdentity)
	if err != nil {
		return nil, err
	}
	var state, serverID, lease string
	var winner int
	var tick int64
	err = tx.QueryRowContext(ctx, `SELECT status,COALESCE(server_id,''),COALESCE(lease_id,''),winner,tick FROM ll_matches WHERE id=$1`, req.MatchID).Scan(&state, &serverID, &lease, &winner, &tick)
	if err == sql.ErrNoRows {
		return nil, denied("Unknown assignment")
	}
	if err != nil {
		return nil, err
	}
	if serverID != req.ServerID || lease != req.LeaseID {
		return nil, denied("Wrong assignment lease")
	}
	terminalState := state == "finished" || state == "aborted"
	if req.Event == "finished" && state == "finished" {
		if req.Winner != winner || req.Tick != tick {
			return nil, conflict("Result already recorded")
		}
		return ok(), nil
	}
	if req.Event == "aborted" && terminalState {
		return ok(), nil
	}
	// Retry of a released old result must not release a newly assigned match.
	if req.Event == "released" && terminalState && server.matchID != req.MatchID {
		return ok(), nil
	}
	if server.matchID != req.MatchID {
		return nil, denied("Inactive assignment")
	}
	switch req.Event {
	case "ready":
		if state == "ready" || state == "playing" {
			return ok(), nil
		}
		if state != "preparing" {
			return nil, conflict("Cannot ready this assignment")
		}
		if _, err = tx.ExecContext(ctx, `UPDATE ll_matches SET status='ready',updated_at=now() WHERE id=$1`, req.MatchID); err != nil {
			return nil, err
		}
		_, err = tx.ExecContext(ctx, `UPDATE ll_searches SET status='ready' WHERE match_id=$1`, req.MatchID)
	case "started":
		if state == "playing" {
			return ok(), nil
		}
		if state != "ready" {
			return nil, conflict("Cannot start this assignment")
		}
		var claimed int
		if err = tx.QueryRowContext(ctx, `SELECT count(*) FROM ll_players WHERE match_id=$1 AND claim_request IS NOT NULL`, req.MatchID).Scan(&claimed); err != nil {
			return nil, err
		}
		if claimed != 2 {
			return nil, conflict("Both players must be admitted before starting")
		}
		_, err = tx.ExecContext(ctx, `UPDATE ll_matches SET status='playing',updated_at=now() WHERE id=$1`, req.MatchID)
	case "finished":
		if state == "finished" {
			if req.Winner != winner || req.Tick != tick {
				return nil, conflict("Result already recorded")
			}
			return ok(), nil
		}
		if state != "playing" {
			return nil, conflict("Only a started match can finish")
		}
		err = terminal(ctx, tx, req.MatchID, "finished", req.Reason, req.Winner, req.Tick)
	case "aborted":
		if terminalState {
			return ok(), nil
		}
		err = terminal(ctx, tx, req.MatchID, "aborted", req.Reason, 0, req.Tick)
	case "released":
		if !terminalState {
			return nil, conflict("Acknowledge a terminal event before releasing")
		}
		_, err = tx.ExecContext(ctx, `UPDATE ll_servers SET match_id=NULL WHERE id=$1 AND match_id=$2`, req.ServerID, req.MatchID)
	default:
		return nil, invalid("Unknown server event")
	}
	return ok(), err
}
