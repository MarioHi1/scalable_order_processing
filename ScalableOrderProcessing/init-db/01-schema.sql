-- Run automatically by Postgres on the very first container start
-- (only when the data directory is still empty -- so NOT again with an
-- existing volume, not even after docker compose restart).

CREATE TABLE orders
(
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ NULL,
    finished_at TIMESTAMPTZ NULL,
    timeout_at TIMESTAMPTZ NULL
);

INSERT INTO orders (status)
SELECT 'Open'
FROM generate_series(1, 50);