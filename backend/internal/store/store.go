package store

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"database/sql"
	_ "embed"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"time"

	"inspection/internal/model"
	_ "modernc.org/sqlite"
)

//go:embed schema.sql
var schema string

var ErrNotFound = errors.New("issue not found")
var ErrConflict = errors.New("idempotency key already used with different content")
var ErrDeleted = errors.New("issue has been deleted")

type Store struct{ db *sql.DB }

func Open(path string) (*Store, error) {
	if err := os.MkdirAll(filepath.Dir(path), 0755); err != nil {
		return nil, err
	}
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, err
	}
	// One connection serializes write transactions and keeps PRAGMA settings consistent.
	db.SetMaxOpenConns(1)
	if _, err = db.Exec("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;"); err == nil {
		_, err = db.Exec(schema)
	}
	if err != nil {
		db.Close()
		return nil, err
	}
	return &Store{db: db}, nil
}

func (s *Store) Close() error { return s.db.Close() }
func (s *Store) Ping(ctx context.Context) error {
	var count int
	return s.db.QueryRowContext(ctx, "SELECT COUNT(*) FROM issues").Scan(&count)
}

const columns = "id,title,description,priority,status,position_x,position_y,position_z,created_at,updated_at"
const active = "NOT EXISTS (SELECT 1 FROM deleted_issues WHERE issue_id=issues.id)"

type scanner interface{ Scan(...any) error }

func scanIssue(row scanner) (model.Issue, error) {
	var i model.Issue
	err := row.Scan(&i.ID, &i.Title, &i.Description, &i.Priority, &i.Status, &i.Position.X, &i.Position.Y, &i.Position.Z, &i.CreatedAt, &i.UpdatedAt)
	return i, err
}

func (s *Store) List(ctx context.Context) ([]model.Issue, error) {
	rows, err := s.db.QueryContext(ctx, "SELECT "+columns+" FROM issues WHERE "+active+" ORDER BY created_at DESC,id DESC")
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	issues := make([]model.Issue, 0)
	for rows.Next() {
		i, err := scanIssue(rows)
		if err != nil {
			return nil, err
		}
		issues = append(issues, i)
	}
	return issues, rows.Err()
}

func (s *Store) Get(ctx context.Context, id string) (model.Issue, error) {
	i, err := scanIssue(s.db.QueryRowContext(ctx, "SELECT "+columns+" FROM issues WHERE id=? AND "+active, id))
	if errors.Is(err, sql.ErrNoRows) {
		return i, ErrNotFound
	}
	return i, err
}

func (s *Store) Delete(ctx context.Context, id string) error {
	// One atomic statement hides the record, while preserving payload and retry history.
	result, err := s.db.ExecContext(ctx, "INSERT INTO deleted_issues(issue_id,deleted_at) SELECT id,? FROM issues WHERE id=? AND "+active,
		time.Now().UTC().Format("2006-01-02T15:04:05.000000000Z"), id)
	if err != nil {
		return err
	}
	n, err := result.RowsAffected()
	if err != nil {
		return err
	}
	if n == 0 {
		return ErrNotFound
	}
	return nil
}

func (s *Store) Create(ctx context.Context, input model.CreateIssue, key string) (model.Issue, bool, error) {
	var empty model.Issue
	body, err := json.Marshal(input)
	if err != nil {
		return empty, false, err
	}
	hash := sha256.Sum256(body)
	fingerprint := hex.EncodeToString(hash[:])
	tx, err := s.db.BeginTx(ctx, nil)
	if err != nil {
		return empty, false, err
	}
	defer tx.Rollback()
	if key != "" {
		var id, savedHash string
		err = tx.QueryRowContext(ctx, "SELECT issue_id,request_hash FROM idempotency_keys WHERE key=?", key).Scan(&id, &savedHash)
		if err == nil {
			var deleted bool
			if err = tx.QueryRowContext(ctx, "SELECT EXISTS(SELECT 1 FROM deleted_issues WHERE issue_id=?)", id).Scan(&deleted); err != nil {
				return empty, false, err
			}
			if deleted {
				return empty, false, ErrDeleted
			}
			if fingerprint != savedHash {
				return empty, false, ErrConflict
			}
			i, err := scanIssue(tx.QueryRowContext(ctx, "SELECT "+columns+" FROM issues WHERE id=?", id))
			return i, true, err
		}
		if !errors.Is(err, sql.ErrNoRows) {
			return empty, false, err
		}
	}
	id, err := newID()
	if err != nil {
		return empty, false, err
	}
	now := time.Now().UTC().Format("2006-01-02T15:04:05.000000000Z")
	i := model.Issue{ID: id, Title: input.Title, Description: input.Description, Priority: input.Priority, Status: "open", Position: input.Position, CreatedAt: now, UpdatedAt: now}
	_, err = tx.ExecContext(ctx, "INSERT INTO issues ("+columns+") VALUES (?,?,?,?,?,?,?,?,?,?)", i.ID, i.Title, i.Description, i.Priority, i.Status, i.Position.X, i.Position.Y, i.Position.Z, i.CreatedAt, i.UpdatedAt)
	if err != nil {
		return empty, false, err
	}
	if key != "" {
		if _, err = tx.ExecContext(ctx, "INSERT INTO idempotency_keys(key,request_hash,issue_id) VALUES(?,?,?)", key, fingerprint, id); err != nil {
			return empty, false, err
		}
	}
	if err = tx.Commit(); err != nil {
		return empty, false, err
	}
	return i, false, nil
}

func (s *Store) UpdateStatus(ctx context.Context, id, status string) (model.Issue, error) {
	var empty model.Issue
	tx, err := s.db.BeginTx(ctx, nil)
	if err != nil {
		return empty, err
	}
	defer tx.Rollback()
	result, err := tx.ExecContext(ctx, "UPDATE issues SET status=?,updated_at=? WHERE id=? AND "+active, status, time.Now().UTC().Format("2006-01-02T15:04:05.000000000Z"), id)
	if err != nil {
		return empty, err
	}
	n, err := result.RowsAffected()
	if err != nil {
		return empty, err
	}
	if n == 0 {
		return empty, ErrNotFound
	}
	i, err := scanIssue(tx.QueryRowContext(ctx, "SELECT "+columns+" FROM issues WHERE id=?", id))
	if err != nil {
		return empty, err
	}
	if err = tx.Commit(); err != nil {
		return empty, err
	}
	return i, nil
}

func newID() (string, error) {
	var b [16]byte
	if _, err := rand.Read(b[:]); err != nil {
		return "", err
	}
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%x-%x-%x-%x-%x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:16]), nil
}
