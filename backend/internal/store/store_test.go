package store

import (
	"context"
	"database/sql"
	"inspection/internal/model"
	"path/filepath"
	"testing"
)

func TestSoftDeleteRetainsPayloadAndMigratesOldDatabase(t *testing.T) {
	path := filepath.Join(t.TempDir(), "legacy.db")
	s, err := Open(path)
	if err != nil {
		t.Fatal(err)
	}
	original, _, err := s.Create(context.Background(), model.CreateIssue{Title: "保留数据", Priority: "medium", Position: model.Position{X: 2}}, "original-key")
	if err != nil {
		t.Fatal(err)
	}
	// Simulate the schema shipped before soft deletion was introduced.
	if _, err = s.db.Exec("DROP TABLE deleted_issues"); err != nil {
		t.Fatal(err)
	}
	s.Close()
	s, err = Open(path)
	if err != nil {
		t.Fatal(err)
	}
	defer s.Close()
	if got, err := s.Get(context.Background(), original.ID); err != nil || got != original {
		t.Fatal(got, err)
	}
	if err = s.Delete(context.Background(), original.ID); err != nil {
		t.Fatal(err)
	}
	kept, err := scanIssue(s.db.QueryRow("SELECT "+columns+" FROM issues WHERE id=?", original.ID))
	if err != nil || kept != original {
		t.Fatal("soft delete lost payload", kept, err)
	}
	var deletedAt string
	if err = s.db.QueryRow("SELECT deleted_at FROM deleted_issues WHERE issue_id=?", original.ID).Scan(&deletedAt); err != nil || deletedAt == "" {
		t.Fatal(deletedAt, err)
	}
	var id string
	if err = s.db.QueryRow("SELECT issue_id FROM idempotency_keys WHERE key=?", "original-key").Scan(&id); err != nil || id != original.ID {
		t.Fatal(id, err)
	}
	s.Close()
	if err = s.Delete(context.Background(), original.ID); err == nil || err == sql.ErrNoRows {
		t.Fatal("closed storage should fail", err)
	}
}
