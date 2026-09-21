# example-table.md

## PostgreSQL Example Schema

The candidate is responsible for creating the database.

### Orders Table

```sql
CREATE TABLE orders
(
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    status VARCHAR(50) NOT NULL,

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    started_at TIMESTAMPTZ NULL,

    finished_at TIMESTAMPTZ NULL,

    timeout_at TIMESTAMPTZ NULL
);
```

### Allowed Status Values

```text
Open
InProgress
Completed
Timeout
```

---

## Sample Data

The following script creates 50 open orders for testing.

```sql
INSERT INTO orders (status)
SELECT 'Open'
FROM generate_series(1, 50);
```

### Verification

```sql
SELECT *
FROM orders
ORDER BY id;
```

### Count Open Orders

```sql
SELECT COUNT(*)
FROM orders
WHERE status = 'Open';
```

Expected result:

```text
50
```
