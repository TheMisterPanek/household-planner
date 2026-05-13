## 1. Application — DB path configuration

- [x] 1.1 In `Program.cs`, replace the hardcoded `AppContext.BaseDirectory`-relative path with `configuration["DB_PATH"] ?? <existing fallback>` so `DB_PATH` env var overrides the default
- [x] 1.2 Verify `Directory.CreateDirectory` still runs for the parent directory of the resolved path (covers both the volume path and the local fallback)
- [x] 1.3 Confirm `dotnet build` succeeds with 0 errors after the change

## 2. Docker Compose — named volume

- [x] 2.1 Add a `volumes:` top-level section to `docker-compose.yml` declaring the `db-data` named volume
- [x] 2.2 Mount `db-data` at `/data` in the `bot` service
- [x] 2.3 Add `DB_PATH=/data/product-tracker.db` to the `bot` service environment (or `env_file` if preferred)

## 3. Smoke test — local Docker

- [x] 3.1 Run `make up`, send a bot command to create data, then run `make up` again and confirm data persists
- [x] 3.2 Run `docker compose down` (no `--volumes`) then `make up` and confirm data still persists
- [x] 3.3 Confirm `dotnet run` (local, no `DB_PATH` set) still works and creates the DB at the existing default path
