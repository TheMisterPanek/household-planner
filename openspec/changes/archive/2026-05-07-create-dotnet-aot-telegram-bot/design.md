## Context

A new standalone .NET 9 console application that runs a Telegram bot. The app must compile to a native AOT binary (no JIT, no runtime required on the target) so it starts in milliseconds and ships as a single small executable. Configuration comes from a `.env` file at the working directory, and all output is structured JSON logs to stdout.

No existing codebase to migrate; this is a greenfield project within this repo directory.

## Goals / Non-Goals

**Goals:**
- Single AOT-compiled binary that can run inside a minimal Docker image (e.g., `mcr.microsoft.com/dotnet/runtime-deps`)
- Telegram polling loop (long-polling via `Telegram.Bot`) that dispatches updates
- `.env` file loaded into `IConfiguration` at startup
- Structured logging to stdout, log level configurable via env var
- Dependency injection container wired via `Microsoft.Extensions.Hosting`

**Non-Goals:**
- Webhook mode (polling only for now)
- Persistent storage or database
- Any business-logic commands beyond a basic echo/ping handler to verify connectivity
- Unit tests in this initial scaffold (added in a follow-up)

## Decisions

### 1. Hosting model: `IHostBuilder` generic host

**Decision**: Use `Microsoft.Extensions.Hosting` generic host with a hosted service (`BackgroundService`) for the polling loop.

**Rationale**: Gives DI, configuration, and logging for free; integrates cleanly with AOT via the `PublishTrimmed`/`PublishAot` properties. The alternative (bare `Main` with manual DI) is lower ceremony but loses lifetime management.

**AOT note**: `Host.CreateDefaultBuilder` uses some reflection internally. Use `Host.CreateApplicationBuilder` (or the trimming-safe `CreateEmptyApplicationBuilder`) and wire components explicitly to avoid trimmed-away branches.

---

### 2. Telegram.Bot library vs raw HTTP

**Decision**: Use `Telegram.Bot` NuGet package.

**Rationale**: Mature API surface, actively maintained, and as of v21+ ships with `System.Text.Json`-based serialization and explicit AOT/trimming support via `[JsonSerializable]` source-generated contexts.

**AOT flag**: Ensure `Telegram.Bot`'s `TelegramBotClientOptions` is passed the source-generated `JsonSerializerContext`; do not rely on runtime reflection for JSON.

---

### 3. `.env` loading: `DotNetEnv`

**Decision**: Use the `DotNetEnv` NuGet package to load `.env` into `Environment` variables before `IConfiguration` is built.

**Rationale**: Minimal dependency, no reflection-heavy patterns. Loaded via `DotNetEnv.Env.Load()` before the host is built; values then flow naturally into `IConfiguration` via the environment variable provider.

**Alternative considered**: Manual line-by-line parsing — rejected as unnecessary reinvention.

**AOT compatibility**: `DotNetEnv` uses only string parsing; no reflection. Safe.

---

### 4. Logging: `Microsoft.Extensions.Logging` + console JSON formatter

**Decision**: Use the built-in `AddJsonConsole()` formatter with log level sourced from `Logging__LogLevel__Default` env var (standard .NET env-var override syntax).

**Rationale**: No extra dependency; trimming-safe; structured output parseable by log aggregators.

---

### 5. AOT publication settings

```xml
<PublishAot>true</PublishAot>
<InvariantGlobalization>true</InvariantGlobalization>
<TrimmerRootDescriptor>TrimmerRoots.xml</TrimmerRootDescriptor>
```

A `TrimmerRoots.xml` will preserve the Telegram.Bot JSON context and any other types the linker would otherwise remove.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `Telegram.Bot` AOT compatibility gaps in minor versions | Pin to a specific version; run `dotnet publish -r linux-x64` in CI to catch linker warnings early |
| `DotNetEnv` not finding `.env` in Docker | Document that `.env` must be bind-mounted or baked into image; fall back gracefully if file absent |
| `InvariantGlobalization=true` breaks Unicode in bot messages | Evaluate; if Cyrillic/CJK needed, set `false` and accept ~1 MB binary size increase |
| AOT binary size larger than expected | Use `IlcOptimizationPreference=Speed` vs `Size` to tune; strip debug symbols in release |

## Migration Plan

1. Create project: `dotnet new console -n ProductTrackerBot --aot`
2. Add NuGet packages and AOT settings to `.csproj`
3. Implement `BotHostedService`, `UpdateHandler`, configuration binding, logging setup
4. `dotnet publish -r linux-x64 -c Release` → verify binary runs
5. Write `Dockerfile` using `runtime-deps` base image
6. Smoke-test with real bot token in `.env`

Rollback: delete project folder. No shared state affected.

## Open Questions

- Should the bot token live only in `.env` or also support Azure Key Vault / Secrets Manager injection? (Defer to follow-up)
- Is globalization required? (Affects binary size — confirm before finalizing `InvariantGlobalization`)
