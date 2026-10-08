CREATE EXTENSION IF NOT EXISTS citext;

CREATE TABLE accounts(
    account_id BIGSERIAL PRIMARY KEY,
    username CITEXT UNIQUE NOT NULL,
    password_hash BYTEA NOT NULL,
    password_salt BYTEA NOT NULL,
    password_iterations INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE characters(
    character_id BIGSERIAL PRIMARY KEY,
    account_id BIGINT NOT NULL REFERENCES accounts(account_id),
    name CITEXT UNIQUE NOT NULL,
    class_id SMALLINT NOT NULL,
    level INT NOT NULL DEFAULT 1,
    experience BIGINT NOT NULL DEFAULT 0,
    copper BIGINT NOT NULL DEFAULT 0,
    zone TEXT NOT NULL,
    pos_x REAL NOT NULL,
    pos_z REAL NOT NULL,
    inventory JSONB NOT NULL DEFAULT '{}',
    quest_log JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_played_at TIMESTAMPTZ
);

CREATE INDEX ix_characters_account ON characters(account_id);
