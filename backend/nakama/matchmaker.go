package main

import (
	"context"
	"database/sql"
	"fmt"
	"sort"
	"strings"

	"github.com/heroiclabs/nakama-common/rtapi"
	"github.com/heroiclabs/nakama-common/runtime"
)

func beforeMatchmaker(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *rtapi.Envelope) (*rtapi.Envelope, error) {
	req := in.GetMatchmakerAdd()
	user, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	session, _ := ctx.Value(runtime.RUNTIME_CTX_SESSION_ID).(string)
	if req == nil || user == "" || session == "" || req.MinCount != 2 || req.MaxCount != 2 {
		return nil, invalid("Only authenticated 1v1 searches are supported")
	}
	tx, err := transaction(ctx, db)
	if err != nil {
		return nil, internal(logger, err)
	}
	defer tx.Rollback()
	if err = sweep(ctx, tx); err != nil {
		return nil, internal(logger, err)
	}
	var protocol, rules int
	var hash, queueSession string
	var rating int
	var age float64
	searchID := req.StringProperties["searchId"]
	err = tx.QueryRowContext(ctx, `SELECT protocol,rules,content_hash,queue_session,rating_at_search,extract(epoch FROM now()-created_at) FROM ll_searches WHERE id=$1 AND player_id=$2 AND status='searching'`, searchID, user).Scan(&protocol, &rules, &hash, &queueSession, &rating, &age)
	if err == sql.ErrNoRows {
		return nil, denied("Open your own search first")
	}
	if err != nil {
		return nil, internal(logger, err)
	}
	expected := compat(protocol, rules, hash)
	if req.StringProperties["compat"] != expected {
		return nil, denied("Search compatibility does not match")
	}
	if queueSession != "" && queueSession != session {
		return nil, conflict("Search already queued from another session")
	}
	if _, err = tx.ExecContext(ctx, `UPDATE ll_searches SET queue_session=$2,seen_at=now() WHERE id=$1`, searchID, session); err != nil {
		return nil, internal(logger, err)
	}
	if err = tx.Commit(); err != nil {
		return nil, internal(logger, err)
	}
	// Never accept a caller's wildcard query or ranking/faction properties.
	// Keyword fields need escaped terms; quoted phrases tokenize punctuation.
	radius := searchRadius(age)
	req.Query = `+properties.compat:` + strings.NewReplacer(":", `\:`, "-", `\-`).Replace(expected) + fmt.Sprintf(" +properties.rating:>=%d +properties.rating:<=%d", rating-radius, rating+radius)
	req.StringProperties = map[string]string{"compat": expected, "searchId": searchID}
	req.NumericProperties = map[string]float64{"rating": float64(rating)}
	return in, nil
}

type matchedPlayer struct {
	user, session, searchID, faction, content string
	protocol, rules                           int
	websocket                                 bool
	rating, radius                            int
}

func matchmakerMatched(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, entries []runtime.MatchmakerEntry) (string, error) {
	// A nonempty opaque ID prevents Nakama's fallback to a relayed match token.
	// Players always ask ll_search_status for their actual server assignment.
	rejectID := "unavailable-" + token()
	if len(entries) != 2 {
		return rejectID, nil
	}
	tx, err := transaction(ctx, db)
	if err != nil {
		logger.Error("Matchmaking unavailable: %v", err)
		return rejectID, nil
	}
	defer tx.Rollback()
	if err = sweep(ctx, tx); err != nil {
		logger.Error("Matchmaking sweep: %v", err)
		return rejectID, nil
	}
	players := make([]matchedPlayer, 2)
	valid := true
	for i, entry := range entries {
		p := &players[i]
		p.user = entry.GetPresence().GetUserId()
		p.session = entry.GetPresence().GetSessionId()
		p.searchID, _ = entry.GetProperties()["searchId"].(string)
		var queued string
		var age float64
		err = tx.QueryRowContext(ctx, `SELECT faction,protocol,rules,content_hash,queue_session,websocket,rating_at_search,extract(epoch FROM now()-created_at) FROM ll_searches WHERE id=$1 AND player_id=$2 AND status='searching'`, p.searchID, p.user).Scan(&p.faction, &p.protocol, &p.rules, &p.content, &queued, &p.websocket, &p.rating, &age)
		if err == sql.ErrNoRows {
			valid = false
			continue
		}
		if err != nil {
			logger.Error("Matchmaking lookup: %v", err)
			return rejectID, nil
		}
		supplied, _ := entry.GetProperties()["compat"].(string)
		suppliedRating, ratingOK := entry.GetProperties()["rating"].(float64)
		p.radius = searchRadius(age)
		if queued != p.session || supplied != compat(p.protocol, p.rules, p.content) || !ratingOK || suppliedRating != float64(p.rating) {
			valid = false
		}
	}
	if players[0].user == players[1].user || compat(players[0].protocol, players[0].rules, players[0].content) != compat(players[1].protocol, players[1].rules, players[1].content) || !ratingPairAllowed(players[0].rating, players[1].rating, players[0].radius, players[1].radius) {
		valid = false
	}
	if !valid {
		for _, p := range players {
			if _, err = tx.ExecContext(ctx, `UPDATE ll_searches SET status='failed',message='Search changed while finding an opponent' WHERE id=$1 AND player_id=$2 AND status='searching'`, p.searchID, p.user); err != nil {
				logger.Error("Matchmaking rejection: %v", err)
				return rejectID, nil
			}
		}
		if err = tx.Commit(); err != nil {
			logger.Error("Matchmaking rejection commit: %v", err)
		}
		return rejectID, nil
	}
	sort.Slice(players, func(i, j int) bool { return players[i].user < players[j].user })
	id := token()
	p := players[0]
	// A pair including a browser player is only allocated to a server with a websocket listener.
	websocket := players[0].websocket || players[1].websocket
	_, err = tx.ExecContext(ctx, `INSERT INTO ll_matches(id,status,protocol,rules,content_hash,websocket,rank_version) VALUES($1,'waitingServer',$2,$3,$4,$5,1)`, id, p.protocol, p.rules, p.content, websocket)
	if err != nil {
		logger.Error("Matchmaking allocation: %v", err)
		return rejectID, nil
	}
	for seat, p := range players {
		_, err = tx.ExecContext(ctx, `INSERT INTO ll_players(match_id,player_id,search_id,seat,faction,ticket_hash) VALUES($1,$2,$3,$4,$5,$6)`, id, p.user, p.searchID, seat, p.faction, ticketHash(joinTicket(id, p.user)))
		if err != nil {
			logger.Error("Matchmaking player: %v", err)
			return rejectID, nil
		}
		_, err = tx.ExecContext(ctx, `UPDATE ll_searches SET status='waitingServer',match_id=$2,seen_at=now() WHERE id=$1`, p.searchID, id)
		if err != nil {
			logger.Error("Matchmaking search: %v", err)
			return rejectID, nil
		}
	}
	if err = allocate(ctx, tx); err != nil {
		logger.Error("Matchmaking pool: %v", err)
		return rejectID, nil
	}
	if err = tx.Commit(); err != nil {
		logger.Error("Matchmaking commit: %v", err)
		return rejectID, nil
	}
	logger.Info("Allocated public pair %s", id)
	return id, nil
}
