package main

import (
	"context"
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"database/sql"
	_ "embed"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"regexp"
	"strings"

	"github.com/heroiclabs/nakama-common/runtime"
)

//go:embed schema.sql
var schema string

var ticketKey []byte
var allowedServers map[string]bool
var safeID = regexp.MustCompile(`^[a-zA-Z0-9_.:-]{1,128}$`)
var contentID = regexp.MustCompile(`^[a-zA-Z0-9_-]{1,128}$`)

type rpcFunc func(context.Context, runtime.Logger, *sql.Tx, string, string) (any, error)

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	env, _ := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	ticketKey = []byte(env["LL_TICKET_KEY"])
	if len(ticketKey) < 32 {
		return errors.New("LL_TICKET_KEY requires at least 32 characters")
	}
	allowedServers = map[string]bool{}
	for _, id := range strings.Split(env["LL_SERVER_IDS"], ",") {
		if id = strings.TrimSpace(id); id != "" {
			allowedServers[id] = true
		}
	}
	if len(allowedServers) == 0 {
		return errors.New("LL_SERVER_IDS must explicitly allow server identifiers")
	}
	if _, err := db.ExecContext(ctx, schema); err != nil {
		return fmt.Errorf("application schema: %w", err)
	}
	endpoints := []struct {
		name   string
		server bool
		fn     rpcFunc
	}{
		{"ll_search_open", false, searchOpen}, {"ll_search_status", false, searchStatus}, {"ll_search_cancel", false, searchCancel},
		{"ll_server_register", true, serverRegister}, {"ll_server_poll", true, serverPoll}, {"ll_server_admit", true, serverAdmit}, {"ll_server_event", true, serverEvent},
	}
	for _, endpoint := range endpoints {
		if err := initializer.RegisterRpc(endpoint.name, wrapRPC(endpoint.server, endpoint.fn)); err != nil {
			return err
		}
	}
	if err := initializer.RegisterBeforeRt("MatchmakerAdd", beforeMatchmaker); err != nil {
		return err
	}
	if err := initializer.RegisterMatchmakerMatched(matchmakerMatched); err != nil {
		return err
	}
	logger.Info("Little Legion allocation module ready (static NGO pool)")
	return nil
}

func wrapRPC(server bool, fn rpcFunc) func(context.Context, runtime.Logger, *sql.DB, runtime.NakamaModule, string) (string, error) {
	return func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
		user, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
		// Nakama authenticates http_key before entering this function. A player JWT
		// must never authorize registry, admission or game results.
		if server && user != "" || !server && user == "" {
			return "", runtime.NewError("Wrong credential type", 7)
		}
		tx, err := transaction(ctx, db)
		if err != nil {
			return "", internal(logger, err)
		}
		defer tx.Rollback()
		if err = sweep(ctx, tx); err != nil {
			return "", internal(logger, err)
		}
		result, err := fn(ctx, logger, tx, user, payload)
		if err != nil {
			var rpcErr *runtime.Error
			if errors.As(err, &rpcErr) {
				return "", err
			}
			return "", internal(logger, err)
		}
		if err = allocate(ctx, tx); err != nil {
			return "", internal(logger, err)
		}
		if err = tx.Commit(); err != nil {
			return "", internal(logger, err)
		}
		data, err := json.Marshal(result)
		return string(data), err
	}
}

func transaction(ctx context.Context, db *sql.DB) (*sql.Tx, error) {
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		return nil, err
	}
	// Serialize the small local pool across RPCs and the native matchmaking hook.
	// This prevents cancel/admit/start races even if multiple Nakama workers run.
	if _, err = tx.ExecContext(ctx, `SELECT pg_advisory_xact_lock(74117778)`); err != nil {
		tx.Rollback()
		return nil, err
	}
	return tx, nil
}
func internal(logger runtime.Logger, err error) error {
	logger.Error("Allocation transaction failed: %v", err)
	return runtime.NewError("Service temporarily unavailable", 13)
}
func invalid(message string) error  { return runtime.NewError(message, 3) }
func denied(message string) error   { return runtime.NewError(message, 7) }
func conflict(message string) error { return runtime.NewError(message, 9) }
func decode(payload string, value any) error {
	if len(payload) > 8192 {
		return invalid("Payload too large")
	}
	d := json.NewDecoder(strings.NewReader(payload))
	d.DisallowUnknownFields()
	if err := d.Decode(value); err != nil {
		return invalid("Invalid request")
	}
	if err := d.Decode(new(any)); err != io.EOF {
		return invalid("Invalid trailing data")
	}
	return nil
}
func token() string {
	data := make([]byte, 32)
	if _, err := rand.Read(data); err != nil {
		panic(err)
	}
	return base64.RawURLEncoding.EncodeToString(data)
}
func joinTicket(matchID, playerID string) string {
	mac := hmac.New(sha256.New, ticketKey)
	mac.Write([]byte("ll-join-v1\x00" + matchID + "\x00" + playerID))
	return base64.RawURLEncoding.EncodeToString(mac.Sum(nil))
}
func ticketHash(value string) string {
	digest := sha256.Sum256([]byte(value))
	return hex.EncodeToString(digest[:])
}
func compat(protocol, rules int, content string) string {
	return fmt.Sprintf("%d:%d:%s", protocol, rules, content)
}
func validFaction(faction string) bool { return faction == "industrial" || faction == "biological" }
func validCompatibility(protocol, rules int, content string) bool {
	return protocol > 0 && rules > 0 && contentID.MatchString(content)
}
func ok() any { return map[string]bool{"ok": true} }

func main() {}
