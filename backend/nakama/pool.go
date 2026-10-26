package main

import (
	"context"
	"database/sql"
)

func terminal(ctx context.Context, tx *sql.Tx, matchID, state, reason string, winner int, tick int64) error {
	if _, err := tx.ExecContext(ctx, `UPDATE ll_matches SET status=$2,reason=$3,winner=$4,tick=$5,updated_at=now() WHERE id=$1`, matchID, state, reason, winner, tick); err != nil {
		return err
	}
	searchState := "finished"
	if state == "aborted" {
		searchState = "failed"
	}
	_, err := tx.ExecContext(ctx, `UPDATE ll_searches SET status=$2,message=$3 WHERE match_id=$1 AND status NOT IN ('cancelled','finished','failed')`, matchID, searchState, reason)
	return err
}

func sweep(ctx context.Context, tx *sql.Tx) error {
	// A missing process cannot retain a match or give a replacement process its lease.
	rows, err := tx.QueryContext(ctx, `SELECT s.id,COALESCE(s.match_id,'') FROM ll_servers s WHERE active AND heartbeat_at < now()-interval '20 seconds'`)
	if err != nil {
		return err
	}
	type stale struct{ id, match string }
	var servers []stale
	for rows.Next() {
		var v stale
		if err = rows.Scan(&v.id, &v.match); err != nil {
			rows.Close()
			return err
		}
		servers = append(servers, v)
	}
	err = rows.Err()
	rows.Close()
	if err != nil {
		return err
	}
	for _, s := range servers {
		if s.match != "" {
			var state string
			if err = tx.QueryRowContext(ctx, `SELECT status FROM ll_matches WHERE id=$1`, s.match).Scan(&state); err != nil {
				return err
			}
			if state != "finished" && state != "aborted" {
				if err = terminal(ctx, tx, s.match, "aborted", "Server disconnected", 0, 0); err != nil {
					return err
				}
			}
		}
		if _, err = tx.ExecContext(ctx, `UPDATE ll_servers SET active=false,match_id=NULL WHERE id=$1`, s.id); err != nil {
			return err
		}
	}
	rows, err = tx.QueryContext(ctx, `SELECT id FROM ll_matches WHERE (status IN ('waitingServer','preparing','ready') AND expires_at<now()) OR (status='waitingServer' AND EXISTS(SELECT 1 FROM ll_searches WHERE match_id=ll_matches.id AND seen_at<now()-interval '2 minutes'))`)
	if err != nil {
		return err
	}
	var expired []string
	for rows.Next() {
		var id string
		if err = rows.Scan(&id); err != nil {
			rows.Close()
			return err
		}
		expired = append(expired, id)
	}
	err = rows.Err()
	rows.Close()
	if err != nil {
		return err
	}
	for _, id := range expired {
		if err = terminal(ctx, tx, id, "aborted", "Connection deadline expired", 0, 0); err != nil {
			return err
		}
	}
	_, err = tx.ExecContext(ctx, `UPDATE ll_searches SET status='failed',message='Search expired' WHERE status='searching' AND seen_at<now()-interval '2 minutes'`)
	return err
}

func allocate(ctx context.Context, tx *sql.Tx) error {
	// Advisory transaction lock makes this FIFO pool assignment atomic with cancellation.
	for {
		var matchID, serverID, leaseID string
		err := tx.QueryRowContext(ctx, `SELECT m.id,s.id,s.lease_id FROM ll_matches m JOIN ll_servers s ON s.protocol=m.protocol AND s.rules=m.rules AND s.content_hash=m.content_hash WHERE m.status='waitingServer' AND s.active AND s.match_id IS NULL AND s.heartbeat_at>=now()-interval '20 seconds' ORDER BY m.created_at,s.id LIMIT 1`).Scan(&matchID, &serverID, &leaseID)
		if err == sql.ErrNoRows {
			return nil
		}
		if err != nil {
			return err
		}
		if _, err = tx.ExecContext(ctx, `UPDATE ll_matches SET status='preparing',server_id=$2,lease_id=$3,expires_at=now()+interval '60 seconds',updated_at=now() WHERE id=$1`, matchID, serverID, leaseID); err != nil {
			return err
		}
		if _, err = tx.ExecContext(ctx, `UPDATE ll_servers SET match_id=$2 WHERE id=$1`, serverID, matchID); err != nil {
			return err
		}
		if _, err = tx.ExecContext(ctx, `UPDATE ll_searches SET status='preparing' WHERE match_id=$1`, matchID); err != nil {
			return err
		}
	}
}
