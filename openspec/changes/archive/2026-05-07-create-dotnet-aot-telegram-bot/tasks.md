## 1. Project Scaffold

- [x] 1.1 Create `dotnet new console` project with AOT enabled (`<PublishAot>true</PublishAot>`, `<InvariantGlobalization>true</InvariantGlobalization>`)
- [x] 1.2 Add NuGet packages: `Telegram.Bot`, `DotNetEnv`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging.Console`
- [x] 1.3 Add `.editorconfig` and StyleCop analyzer package; enable nullable reference types
- [x] 1.4 Create `.env.example` with `BOT_TOKEN=` placeholder and add `.env` to `.gitignore`

## 2. Configuration (dotenv-config spec)

- [x] 2.1 Call `DotNetEnv.Env.Load()` at the top of `Program.cs` before `Host.CreateApplicationBuilder`
- [x] 2.2 Create `BotConfiguration` record with `Token` property bound from `BOT_TOKEN` env var
- [x] 2.3 Register `BotConfiguration` via `services.AddOptions<BotConfiguration>().BindConfiguration("")` and validate that `Token` is non-empty on startup

## 3. Structured Logging (structured-logging spec)

- [x] 3.1 Configure `AddJsonConsole()` on the logging builder with UTC timestamps
- [x] 3.2 Remove default console and debug providers; keep only JSON console
- [x] 3.3 Verify `Logging__LogLevel__Default` env var sets the minimum log level; document in `.env.example`
- [x] 3.4 Ensure no code path logs `BotConfiguration.Token` or any secret field

## 4. Telegram Bot Core (telegram-bot-core spec)

- [x] 4.1 Register `TelegramBotClient` as singleton in DI, passing the source-generated `JsonSerializerContext` for AOT compatibility
- [x] 4.2 Implement `BotHostedService : BackgroundService` that starts the polling loop via `bot.StartReceiving(...)` and logs "Bot polling started"
- [x] 4.3 Implement `UpdateHandler` class with `IUpdateHandler` interface; handle `Message` updates (echo reply) and log unknown update types at Debug level
- [x] 4.4 Register `BotHostedService` and `UpdateHandler` in DI
- [x] 4.5 Implement graceful shutdown: pass `CancellationToken` to `StartReceiving`; log "Bot polling stopped" in `StopAsync`

## 5. AOT Compatibility

- [x] 5.1 Add `TrimmerRoots.xml` preserving the Telegram.Bot JSON serializer context and any other types flagged by trimmer warnings
- [x] 5.2 Run `dotnet publish -r linux-x64 -c Release` and resolve all trimmer/AOT warnings to zero
- [x] 5.3 Smoke-test the published binary locally with a real `.env` and confirm bot responds

## 6. Docker

- [x] 6.1 Write `Dockerfile` using `mcr.microsoft.com/dotnet/sdk:9.0` build stage and `mcr.microsoft.com/dotnet/runtime-deps:9.0` runtime stage
- [x] 6.2 Copy the AOT binary into the runtime image; set `ENTRYPOINT`
- [x] 6.3 Build and run the Docker image locally; verify bot starts and logs appear on stdout in JSON format
