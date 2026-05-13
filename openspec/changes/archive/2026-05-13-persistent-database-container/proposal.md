## Why

Every `make up` triggers `docker compose up --build`, which rebuilds the container image and discards the SQLite file baked inside it — wiping all data. A named Docker volume for the database directory will persist the file across redeployments without changing the single-container architecture or migrating away from SQLite.

## What Changes

- Add a named Docker volume (`db-data`) in `docker-compose.yml` mounted at the path where the bot writes its SQLite file
- Configure the bot's database path via environment variable (`DB_PATH`) so the path is not hardcoded in code
- Update `docker-compose.yml` to declare the volume and pass `DB_PATH` to the container
- Update `.env.example` / docs to reflect the new env var

## Capabilities

### New Capabilities
- `persistent-db-volume`: Docker named volume that stores the SQLite database file, surviving container rebuilds and `docker compose down` (data is kept unless volume is explicitly deleted)

### Modified Capabilities
- `telegram-bot-core`: Connection string / DB path resolution now reads from `DB_PATH` env var instead of a hardcoded relative path; startup behavior is otherwise unchanged

## Impact

- **`docker-compose.yml`**: Add `volumes:` section and mount, pass `DB_PATH` env var
- **`Program.cs` / `DatabaseInitializer.cs`**: Read `DB_PATH` from `IConfiguration` (env var fallback to current default)
- **`.env` / `.env.example`**: Document `DB_PATH`
- **No schema changes**, no new dependencies, no AOT concerns
- **Rollback**: Remove the volume mount and revert `DB_PATH` usage; existing in-container path continues to work (data in the volume is unaffected until volume is deleted)
- **Affected team**: single developer; no downstream consumers
