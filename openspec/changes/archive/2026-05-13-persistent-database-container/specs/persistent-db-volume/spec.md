## ADDED Requirements

### Requirement: Database file persists across container rebuilds
The system SHALL store the SQLite database file on a Docker named volume so that `docker compose up --build` does not destroy existing data.

#### Scenario: Redeploy with make up preserves data
- **WHEN** `make up` is run on a host that already has the `db-data` named volume with a database file
- **THEN** the bot starts and the existing shopping lists, purchases, and history records remain intact

#### Scenario: Fresh deployment creates volume and initializes schema
- **WHEN** `make up` is run on a host with no existing `db-data` volume
- **THEN** Docker creates the volume, the bot creates `product-tracker.db` at `/data/product-tracker.db`, and `DatabaseInitializer` runs `CREATE TABLE IF NOT EXISTS` for all tables

#### Scenario: Volume survives docker compose down
- **WHEN** `docker compose down` is executed (without `--volumes`)
- **THEN** the `db-data` named volume is retained and the database file remains unmodified

#### Scenario: Explicit volume removal clears data
- **WHEN** `docker compose down --volumes` or `docker volume rm` is executed
- **THEN** the volume and its contents are deleted; the next `make up` starts with an empty database

### Requirement: DB path is configurable via environment variable
The system SHALL read the database file path from the `DB_PATH` environment variable when set, falling back to the current default path for local development.

#### Scenario: DB_PATH env var present
- **WHEN** the application starts with `DB_PATH=/data/product-tracker.db`
- **THEN** the SQLite connection uses `/data/product-tracker.db` as the data source

#### Scenario: DB_PATH env var absent
- **WHEN** the application starts without `DB_PATH` set
- **THEN** the SQLite connection falls back to the path resolved via `AppContext.BaseDirectory` (existing behavior, no regression)
