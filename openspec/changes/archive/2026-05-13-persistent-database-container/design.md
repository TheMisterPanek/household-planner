## Context

The bot currently resolves its SQLite path via `AppContext.BaseDirectory` at startup:

```csharp
var dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../data/product-tracker.db"));
```

`docker-compose.yml` has no volume declaration, so this file lives entirely inside the container layer. Every `make up` (`docker compose up --build -d`) rebuilds the image, creates a fresh container, and discards the old layer — wiping the database.

## Goals / Non-Goals

**Goals:**
- Persist the SQLite file across `docker compose up --build` redeployments
- Keep the single-container setup (no new services)
- Make the DB path configurable via env var so it works both locally and in Docker

**Non-Goals:**
- Migrating to a client-server database (PostgreSQL, MySQL) — not needed for this scale
- Backup / restore automation
- Separate sidecar container for SQLite — adds complexity with no benefit for a file-based DB

## Decisions

### Named volume over bind mount
A Docker named volume (`db-data`) is preferred over a host bind mount (e.g. `./data:/data`):
- Works on any host without requiring a pre-existing directory
- Survives `docker compose down` (data is only lost on `docker volume rm`)
- Portable across Linux/macOS/Windows without path quoting issues

Bind mount alternative: simpler to inspect with `ls`, but fragile on Windows paths and couples data to repo directory structure.

### DB_PATH env var
Replace the hardcoded `AppContext.BaseDirectory`-relative path with:
```csharp
var dbPath = configuration["DB_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "../../../data/product-tracker.db");
```
- Zero change for local `dotnet run` (fallback keeps current behavior)
- Docker sets `DB_PATH=/data/product-tracker.db`, which lands on the named volume
- AOT-safe: `IConfiguration` reads env vars without reflection

### Volume mount path: `/data`
The volume is mounted at `/data` inside the container. This is short, conventional, and outside `/app` (which is the published app directory), avoiding any accidental conflict with dotnet publish output.

## Risks / Trade-offs

- **Accidental data loss on `docker volume rm db-data`** → Document clearly; volume name is project-scoped by default (`<project>_db-data`), so it won't collide with other projects
- **Permissions** — named volumes are owned by root by default; the .NET app in the container runs as the default user → no issue unless the Dockerfile switches to a non-root user
- **No migration for existing in-container DB** → First deploy after this change starts with a fresh DB (schema is re-created by `DatabaseInitializer`); data already lost due to the original bug, so this is a clean slate

## Migration Plan

1. Add `DB_PATH` read in `Program.cs` (with fallback)
2. Add `volumes:` section and `db-data` named volume to `docker-compose.yml`
3. Mount `/data` into `bot` service; set `DB_PATH=/data/product-tracker.db`
4. `make up` — first run creates the volume and re-initializes schema
5. **Rollback**: remove the `DB_PATH` env var and volume mount from `docker-compose.yml`; data in the volume is untouched until explicitly deleted

## Open Questions

- None — change is self-contained and reversible.
