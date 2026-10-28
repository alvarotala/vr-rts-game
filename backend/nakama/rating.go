package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"math"
	"strings"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

const rankedLeaderboard = "ll_ranked_1v1_v1"

type rankProfile struct {
	PlayerID      string `json:"playerId"`
	DisplayName   string `json:"displayName"`
	Rating        int    `json:"rating"`
	Games         int    `json:"games"`
	Wins          int    `json:"wins"`
	Losses        int    `json:"losses"`
	Draws         int    `json:"draws"`
	Rank          int64  `json:"rank"`
	LeaderboardID string `json:"leaderboardId"`
	Version       int64  `json:"-"`
}

func publicName(player string) string {
	value := strings.ReplaceAll(player, "-", "")
	if len(value) > 6 {
		value = value[:6]
	}
	return "Player " + strings.ToUpper(value)
}

func loadRating(ctx context.Context, tx *sql.Tx, user string) (rankProfile, error) {
	p := rankProfile{PlayerID: user, DisplayName: publicName(user), LeaderboardID: rankedLeaderboard}
	if _, err := tx.ExecContext(ctx, `INSERT INTO ll_ratings(player_id) VALUES($1) ON CONFLICT DO NOTHING`, user); err != nil {
		return p, err
	}
	err := tx.QueryRowContext(ctx, `SELECT rating,games,wins,losses,draws,version FROM ll_ratings WHERE player_id=$1`, user).Scan(&p.Rating, &p.Games, &p.Wins, &p.Losses, &p.Draws, &p.Version)
	return p, err
}

func eloAfter(first, second, winner int) (int, int) {
	actual := .5
	if winner == 0 {
		actual = 1
	} else if winner == 1 {
		actual = 0
	}
	expected := 1 / (1 + math.Pow(10, float64(second-first)/400))
	delta := int(math.Round(32 * (actual - expected)))
	return max(0, first+delta), max(0, second-delta)
}

func searchRadius(ageSeconds float64) int {
	step := int(math.Floor(math.Max(0, ageSeconds) / 20))
	if step > 3 {
		step = 3
	}
	return 100 + 100*step
}

func ratingPairAllowed(first, second, firstRadius, secondRadius int) bool {
	difference := first - second
	if difference < 0 {
		difference = -difference
	}
	return difference <= firstRadius && difference <= secondRadius
}

// Called only while finishing an authoritative match and under the pool lock.
// Both player changes and the result ledger share that terminal transaction.
func rateFinished(ctx context.Context, tx *sql.Tx, match string, winner int) error {
	var version, count int
	if err := tx.QueryRowContext(ctx, `SELECT rank_version FROM ll_matches WHERE id=$1`, match).Scan(&version); err != nil {
		return err
	}
	if version != 1 {
		return nil
	}
	if err := tx.QueryRowContext(ctx, `SELECT count(*) FROM ll_rating_results WHERE match_id=$1`, match).Scan(&count); err != nil {
		return err
	}
	if count == 2 {
		return nil
	}
	if count != 0 {
		return invalid("Incomplete rating ledger")
	}
	rows, err := tx.QueryContext(ctx, `SELECT player_id FROM ll_players WHERE match_id=$1 ORDER BY seat`, match)
	if err != nil {
		return err
	}
	var ids []string
	for rows.Next() {
		var id string
		if err = rows.Scan(&id); err != nil {
			rows.Close()
			return err
		}
		ids = append(ids, id)
	}
	err = rows.Err()
	rows.Close()
	if err != nil {
		return err
	}
	if len(ids) != 2 {
		return invalid("A ranked match requires two assigned players")
	}
	a, err := loadRating(ctx, tx, ids[0])
	if err != nil {
		return err
	}
	b, err := loadRating(ctx, tx, ids[1])
	if err != nil {
		return err
	}
	afterA, afterB := eloAfter(a.Rating, b.Rating, winner)
	before, after := []rankProfile{a, b}, []int{afterA, afterB}
	for seat, p := range before {
		win, loss, draw := 0, 0, 0
		if winner == 2 {
			draw = 1
		} else if winner == seat {
			win = 1
		} else {
			loss = 1
		}
		if _, err = tx.ExecContext(ctx, `INSERT INTO ll_rating_results(match_id,player_id,rating_before,rating_after,delta,opponent_rating) VALUES($1,$2,$3,$4,$5,$6)`, match, p.PlayerID, p.Rating, after[seat], after[seat]-p.Rating, before[1-seat].Rating); err != nil {
			return err
		}
		if _, err = tx.ExecContext(ctx, `UPDATE ll_ratings SET rating=$2,games=games+1,wins=wins+$3,losses=losses+$4,draws=draws+$5,version=version+1,updated_at=now() WHERE player_id=$1`, p.PlayerID, after[seat], win, loss, draw); err != nil {
			return err
		}
		if _, err = tx.ExecContext(ctx, `INSERT INTO ll_rating_outbox(player_id,version) VALUES($1,$2) ON CONFLICT(player_id) DO UPDATE SET version=EXCLUDED.version`, p.PlayerID, p.Version+1); err != nil {
			return err
		}
	}
	return nil
}

// The separate advisory lock serializes every publisher across Nakama workers.
// Snapshot canonical state after taking it; never replay a stale event's score.
// A crash after the Nakama write leaves the outbox and safely repeats SET.
func publishRankings(ctx context.Context, db *sql.DB, nk runtime.NakamaModule) error {
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return err
	}
	defer tx.Rollback()
	if _, err = tx.ExecContext(ctx, `SELECT pg_advisory_xact_lock(74117779)`); err != nil {
		return err
	}
	rows, err := tx.QueryContext(ctx, `SELECT r.player_id,r.rating,r.games,r.wins,r.losses,r.draws,r.version FROM ll_rating_outbox o JOIN ll_ratings r USING(player_id) ORDER BY o.created_at,o.player_id LIMIT 64 FOR UPDATE OF o`)
	if err != nil {
		return err
	}
	var pending []rankProfile
	for rows.Next() {
		var p rankProfile
		if err = rows.Scan(&p.PlayerID, &p.Rating, &p.Games, &p.Wins, &p.Losses, &p.Draws, &p.Version); err != nil {
			rows.Close()
			return err
		}
		pending = append(pending, p)
	}
	err = rows.Err()
	rows.Close()
	if err != nil {
		return err
	}
	for _, p := range pending {
		metadata := map[string]interface{}{"games": p.Games, "wins": p.Wins, "losses": p.Losses, "draws": p.Draws, "version": p.Version}
		// Nakama skips an identical score/subscore SET, including metadata. Games
		// (the monotonic version) also gives ties a deterministic experience order.
		if _, err = nk.LeaderboardRecordWrite(ctx, rankedLeaderboard, p.PlayerID, publicName(p.PlayerID), int64(p.Rating), p.Version, metadata, nil); err != nil {
			return err
		}
		if _, err = tx.ExecContext(ctx, `DELETE FROM ll_rating_outbox WHERE player_id=$1 AND version<=$2`, p.PlayerID, p.Version); err != nil {
			return err
		}
	}
	return tx.Commit()
}

func rankingWorker(logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) {
	ticker := time.NewTicker(2 * time.Second)
	defer ticker.Stop()
	for range ticker.C {
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		if err := publishRankings(ctx, db, nk); err != nil {
			logger.Warn("Ranking projection pending; durable queue will retry")
		}
		cancel()
	}
}

type rankEntry struct {
	PlayerID    string `json:"playerId"`
	DisplayName string `json:"displayName"`
	Rating      int64  `json:"rating"`
	Rank        int64  `json:"rank"`
	Games       int    `json:"games"`
	Wins        int    `json:"wins"`
	Losses      int    `json:"losses"`
	Draws       int    `json:"draws"`
	IsSelf      bool   `json:"isSelf"`
}

func rankRead(top bool) func(context.Context, runtime.Logger, *sql.DB, runtime.NakamaModule, string) (string, error) {
	return func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		user, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
		if user == "" {
			return "", denied("Player authentication required")
		}
		if err := decode(payload, &struct{}{}); err != nil {
			return "", err
		}
		tx, err := transaction(ctx, db)
		if err != nil {
			return "", internal(logger, err)
		}
		defer tx.Rollback()
		profile, err := loadRating(ctx, tx, user)
		if err != nil {
			return "", internal(logger, err)
		}
		if err = tx.Commit(); err != nil {
			return "", internal(logger, err)
		}
		if err = publishRankings(ctx, db, nk); err != nil {
			return "", internal(logger, err)
		}
		limit := 1
		if top {
			limit = 10
		}
		records, own, _, _, err := nk.LeaderboardRecordsList(ctx, rankedLeaderboard, []string{user}, limit, "", 0)
		if err != nil {
			return "", internal(logger, err)
		}
		if len(own) > 0 && profile.Games > 0 {
			profile.Rank = own[0].Rank
		}
		var result any = profile
		if top {
			entries := make([]rankEntry, 0, len(records))
			for _, record := range records {
				e := rankEntry{PlayerID: record.OwnerId, DisplayName: publicName(record.OwnerId), Rating: record.Score, Rank: record.Rank, IsSelf: record.OwnerId == user}
				var metadata struct{ Games, Wins, Losses, Draws int }
				_ = json.Unmarshal([]byte(record.Metadata), &metadata)
				e.Games, e.Wins, e.Losses, e.Draws = metadata.Games, metadata.Wins, metadata.Losses, metadata.Draws
				entries = append(entries, e)
			}
			result = map[string]any{"entries": entries}
		}
		data, err := json.Marshal(result)
		return string(data), err
	}
}

type rankResultRequest struct {
	MatchID string `json:"matchId"`
}
type rankResult struct {
	MatchID        string `json:"matchId"`
	Status         string `json:"status"`
	RatingBefore   int    `json:"ratingBefore"`
	RatingAfter    int    `json:"ratingAfter"`
	RatingDelta    int    `json:"ratingDelta"`
	OpponentRating int    `json:"opponentRating"`
}

func rankMatchResult(ctx context.Context, logger runtime.Logger, tx *sql.Tx, user, payload string) (any, error) {
	var req rankResultRequest
	if err := decode(payload, &req); err != nil {
		return nil, err
	}
	var state string
	var version int
	err := tx.QueryRowContext(ctx, `SELECT m.status,m.rank_version FROM ll_matches m JOIN ll_players p ON p.match_id=m.id WHERE m.id=$1 AND p.player_id=$2`, req.MatchID, user).Scan(&state, &version)
	if err == sql.ErrNoRows {
		return nil, denied("Unknown player match")
	}
	if err != nil {
		return nil, err
	}
	result := rankResult{MatchID: req.MatchID, Status: "pending"}
	if version != 1 || state == "aborted" {
		result.Status = "unrated"
		return result, nil
	}
	if state != "finished" {
		return result, nil
	}
	err = tx.QueryRowContext(ctx, `SELECT rating_before,rating_after,delta,opponent_rating FROM ll_rating_results WHERE match_id=$1 AND player_id=$2`, req.MatchID, user).Scan(&result.RatingBefore, &result.RatingAfter, &result.RatingDelta, &result.OpponentRating)
	if err != nil {
		return nil, err
	}
	result.Status = "rated"
	return result, nil
}
