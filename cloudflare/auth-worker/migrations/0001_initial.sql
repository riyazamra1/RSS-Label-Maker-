PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS accounts (
 id TEXT PRIMARY KEY, full_name TEXT NOT NULL, email TEXT NOT NULL UNIQUE,
 password_hash TEXT NOT NULL, verified_at INTEGER, created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS email_challenges (
 id TEXT PRIMARY KEY, account_id TEXT, email TEXT NOT NULL,
 purpose TEXT NOT NULL CHECK (purpose IN ('verify','reset')), code_hash TEXT NOT NULL,
 expires_at INTEGER NOT NULL, attempts INTEGER NOT NULL DEFAULT 0, consumed_at INTEGER, created_at INTEGER NOT NULL,
 FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_challenges_email_purpose ON email_challenges(email,purpose,created_at);
CREATE TABLE IF NOT EXISTS device_sessions (
 id TEXT PRIMARY KEY, account_id TEXT NOT NULL, token_hash TEXT NOT NULL UNIQUE,
 device_name TEXT NOT NULL, created_at INTEGER NOT NULL, expires_at INTEGER NOT NULL, revoked_at INTEGER,
 FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_sessions_account ON device_sessions(account_id,revoked_at);
CREATE TABLE IF NOT EXISTS rate_limits (bucket_key TEXT PRIMARY KEY, window_start INTEGER NOT NULL, hits INTEGER NOT NULL);