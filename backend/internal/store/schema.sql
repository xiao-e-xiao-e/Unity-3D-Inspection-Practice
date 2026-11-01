CREATE TABLE IF NOT EXISTS issues (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL CHECK (length(trim(title)) > 0),
    description TEXT NOT NULL DEFAULT '',
    priority TEXT NOT NULL CHECK (priority IN ('low', 'medium', 'high')),
    status TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'in_progress', 'resolved')),
    position_x REAL NOT NULL,
    position_y REAL NOT NULL,
    position_z REAL NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS issues_created_at ON issues(created_at DESC, id DESC);
CREATE TABLE IF NOT EXISTS idempotency_keys (
    key TEXT PRIMARY KEY,
    request_hash TEXT NOT NULL,
    issue_id TEXT NOT NULL REFERENCES issues(id)
);
-- Additive migration: keep existing issue records and idempotency keys intact.
CREATE TABLE IF NOT EXISTS deleted_issues (
    issue_id TEXT PRIMARY KEY REFERENCES issues(id),
    deleted_at TEXT NOT NULL
);
