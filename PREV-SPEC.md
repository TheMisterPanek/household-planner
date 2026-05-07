# FamilyStockBot — Complete Specification

> Extracted from the original `product-tracker-bot` project.
> Use this file as the authoritative product spec when rebuilding.

---

## 1. Project Overview

**Name:** FamilyStockBot  
**Type:** Telegram group bot  
**Core value:** The family always knows what's in stock and what's about to expire — no more "did you buy milk?" calls and no more throwing away spoiled food.

An intelligent Telegram bot for managing food inventory in a shared household. Single source of truth for all group members: shared shopping list, inventory tracking, meal planning, and expiry notifications.

---

## 2. Tech Stack & Constraints

| Concern | Decision | Rationale |
|---------|----------|-----------|
| Language / Runtime | C# .NET AOT | Minimal image size (~50 MB), fast startup; suitable for cheap VPS (512 MB RAM) |
| Telegram transport | Long polling via `Telegram.Bot` | No HTTPS/public URL required; standard for self-hosted bots |
| Database | SQLite (embedded) via EF Core | Zero-dependency deployment; household scale doesn't need a network DB |
| Deployment | Docker single-container | Bot + SQLite in one container; SQLite file on a named Docker volume |
| AI abstraction | `IAIProvider` interface | No AI provider chosen yet; must be switchable without core changes |
| Logging | `Microsoft.Extensions.Logging` + Console | AOT-compatible; `docker logs` reads stdout |
| Tests | xUnit + SQLite `:memory:` | Real SQL logic, no mocks, fast |

**Hard constraints:**
- No runtime reflection — use source generators (EF Core, System.Text.Json)
- `SuppressTrimAnalysisWarnings=true` may be used for EF Core / Telegram.Bot AOT compatibility
- SQLite file must survive container restart (named Docker volume)
- All AI features go through `IAIProvider`; `NoopAIProvider` is the default (v1)
- All data scoped to a Telegram `ChatId` (one group per v1 instance)

---

## 3. Solution Structure

```
FamilyStockBot.sln
src/
  FamilyStockBot.Core/          # Domain entities, interfaces; NO external deps
  FamilyStockBot.Infrastructure/ # EF Core + SQLite, repositories, NoopAIProvider
  FamilyStockBot.TgBot/          # Entry point, DI wiring, UpdateDispatcher, handlers
tests/
  FamilyStockBot.Tests/          # xUnit; no AOT required
```

### Key Interfaces (Core)

```csharp
// All AI goes through this — switchable between OpenAI, Claude, Ollama
public interface IAIProvider
{
    bool IsAvailable { get; }
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
    Task<string> AnalyzeImageAsync(byte[] image, string prompt, CancellationToken ct = default);
}

// Populated from current Telegram Update; injected as Scoped
public interface IChatContext
{
    long ChatId { get; }
    long UserId { get; }
    string UserName { get; }
}

// Command handlers auto-discovered by UpdateDispatcher
public interface ICommandHandler
{
    string Command { get; } // e.g. "/buy"
    Task HandleAsync(Message message, CancellationToken ct);
}

// Callback (inline button) handlers
public interface ICallbackHandler
{
    string CallbackPrefix { get; } // e.g. "shop:done:"
    Task HandleAsync(CallbackQuery callback, CancellationToken ct);
}
```

### Dispatch Architecture

- `UpdateDispatcher` routes `Message` updates to `ICommandHandler` by command text and `CallbackQuery` to `ICallbackHandler` by prefix.
- No MediatR, no Channel/queue in v1. Handlers call Core services directly via DI.
- Exceptions in any handler are caught, logged at Error level, bot continues.

### Configuration

- Dev: `dotnet user-secrets set Telegram:BotToken <token>`
- Prod/Docker: `.env` file alongside `docker-compose.yml` (`.env` is `.gitignore`d)
- Pattern: `appsettings.json` + `IOptions<T>` typed POCOs

### Multi-step Dialog State

- Generic `PendingDialogService<TState>` — singleton, `ConcurrentDictionary` keyed by `(chatId, userId)`
- Used for all multi-step command flows (`/buy`, `/addproduct`, `/newmeal`, buy→stock quick flow)
- `/cancel` at any step aborts the dialog and clears state

---

## 4. Data Model

All entities use `int` auto-increment PKs. All data is group-scoped via `GroupId FK → Group`.

```
Group
  Id int PK
  ChatId long (Telegram chat ID)
  ListMessageId int? (shopping list live message)
  ExpiryThresholdDays int (default 3)
  DigestTime TimeSpan (default 08:00)

ShoppingItem
  Id int PK
  GroupId int FK
  Name string
  Quantity string?
  Source enum { Manual, MealPlan }
  AddedByName string

Product
  Id int PK
  GroupId int FK
  Name string
  Quantity decimal
  Unit string?
  ExpiryDate DateTime
  Category enum { Молочка, МясоРыба, ОвощиФрукты, КрупыЗерновые, Консервы, Напитки, Прочее }
  AddedAt DateTime

ExpiryAlert
  Id int PK
  GroupId int FK
  ProductId int FK
  AlertType enum { Warning, Expired }
  SentAt DateTime
  LastSnoozedAt DateTime?

Meal
  Id int PK
  GroupId int FK
  Name string

MealIngredient
  Id int PK
  MealId int FK
  Name string
  Quantity decimal
  Unit string?

WeekMealPlan
  Id int PK
  GroupId int FK
  WeekStart DateTime (Monday of the week)
  DayOfWeek int (0=Mon … 6=Sun)
  MealId int FK
```

---

## 5. Commands & UX

All user-facing strings are in **Russian**. Group-only commands respond with a redirect message if used in a private chat.

### Group Registration
- Lazy — `Group` row is upserted on the **first command** in the chat (not only `/start`)
- Private chat → "Эта команда работает только в групповом чате."

### Shopping List

| Command / Action | Behaviour |
|-----------------|-----------|
| `/buy` | 2-step dialog: asks Name → asks Quantity (inline `[Пропустить]`) |
| `/list` | Posts/edits the persistent live shopping list message |
| Tap `[✓ Name qty]` | Marks item bought, removes from list, shows "➕ Добавить в склад" button |
| Tap `[✗ Убрать]` | Removes item without marking bought |

**Live list message:**
- One persistent message per group, edited in-place on every change
- Header: `🛒 Список покупок:` + inline keyboard (one row per item)
- Each row: `[✓ Name qty]` `[✗ Убрать]`
- Empty state: `"Список покупок пуст"` (no buttons)
- If Telegram 48h edit limit hit → repost as new message, save new `MessageId`
- Bot add confirmation: `"[UserName] добавил(а) [Name] [qty unit]"`
- Bought confirmation: `"[UserName]: [Name] [qty] — отмечено ✓"`

### Inventory

| Command / Action | Behaviour |
|-----------------|-----------|
| `/stock` | Posts fresh inventory message (not live-edited across sessions) |
| `/addproduct` | 5-step dialog: Name → Qty → Unit (skip) → Expiry preset → Category |
| Tap `[− Использовать]` | Shows `[−25%] [−50%] [−75%] [Списать всё]` |
| Tap `[✗ Списать]` | Deletes immediately, confirms |
| Tap `[➕ Добавить в склад]` (after buying) | 2-step: expiry preset → quantity (pre-filled from shopping item) |

**Expiry presets:** `+3д` `+1н` `+2н` `+1м` `Своя дата`

**Category emojis:**
- Молочка → 🥛 | Мясо/рыба → 🍖 | Овощи/фрукты → 🥦
- Крупы/зерновые → 🌾 | Консервы → 🥫 | Напитки → 🥤 | Прочее → 📦

**Stock list row format:**
```
{N}. {emoji} {Name} {Qty} {Unit} · {Category} · через {X} д
```
Per-item buttons: `[− Использовать]` `[✗ Списать]`  
Empty state: `"📦 Склад пуст."`

**Consume flow:**
- `[−25%]` / `[−50%]` / `[−75%]` → update quantity, confirm remaining
- `[Списать всё]` or result ≤ 0 → delete, confirm written off

### Meal Planning

| Command / Action | Behaviour |
|-----------------|-----------|
| `/newmeal` | Multi-step: Name → ingredient loop (Name → Qty/skip → `[➕ Ещё]` / `[✅ Готово]`) |
| `/meals` | Numbered text list of saved meals |
| `/planweek` | Live-edited week grid Mon–Sun; tap day → pick meal from inline list or clear |
| `/generatelist` | Calculates week ingredient totals − current inventory → adds gaps to shopping list |

**Week grid format:**
```
Пн: Паста Болоньезе
Вт: —
Ср: Салат Цезарь
…
[📋 Создать список покупок]
```

**Generation rules:**
- Ingredient match: case-insensitive name equality between `MealIngredient.Name` and `Product.Name`
- Empty week plan → `"📋 Нет запланированных блюд на эту неделю."`
- All covered → `"✅ Всё необходимое уже есть в запасах!"`
- Generated items added as `ShoppingItem { Source = MealPlan }` — displayed with `📋` prefix

**Constraints:** One meal per day per week (v1). Minimum 1 ingredient required before saving meal.

### Expiry Notifications (Background Service)

`ExpiryCheckingService` — `IHostedService` background loop checking every N minutes.

| Event | Message format | Extra button |
|-------|---------------|-------------|
| Expiry within threshold (default 3 days) | `⚠️ {Name} истекает через {X} дней` | `[+1 неделя]` |
| Already expired | `🚨 {Name} — ПРОСРОЧЕН` | `[+1 неделя]` |
| Daily digest (default 08:00) | Summary of today + tomorrow expiries | — |

- Deduplication: alert not repeated within 24 h (tracked in `ExpiryAlert` table)
- `[+1 неделя]` snooze → extends `Product.ExpiryDate` by 7 days, bot confirms
- Daily digest sent only if there are items qualifying; silent otherwise
- Threshold and digest time configurable per group

---

## 6. V1 Requirements (22 total)

### Group Collaboration
- **GRP-01**: All members of the Telegram group share the same data; any member's action is immediately visible to all
- **GRP-02**: Bot messages include the display name of the user who performed the action

### Shopping List
- **SHOP-01**: Add item manually (name, optional quantity + unit)
- **SHOP-02**: View the shared shopping list
- **SHOP-03**: Mark item as bought; change reflected for all members in real time
- **SHOP-04**: Remove item without marking it bought
- **SHOP-05**: After marking bought, bot offers quick flow to add to inventory

### Inventory
- **INV-01**: Add product with name, quantity, unit, expiry, category (price/store/barcode deferred to v3)
- **INV-02**: View full inventory with quantities and expiry dates
- **INV-03**: Decrease remaining quantity (partial use)
- **INV-04**: Write off product completely

### Shelf Life
- **SHL-01**: Quick expiry presets (+3d, +1w, +2w, +1m, custom) when adding product
- **SHL-02**: "Extend +1 week" snooze button in expiry notifications

### Meal Planning
- **MEAL-01**: Create a meal with name + ingredient list (name, qty, unit)
- **MEAL-02**: View saved meals list
- **MEAL-03**: Plan current week by selecting meals per day
- **MEAL-04**: Generate shopping list from week plan minus current inventory

### Notifications
- **NOT-01**: Alert when product expiry is within configurable threshold (default 3 days)
- **NOT-02**: Alert when product expiry has already passed
- **NOT-03**: Daily digest of products expiring today or tomorrow

### Infrastructure
- **INFRA-01**: Bot packaged as Docker container, no external dependencies
- **INFRA-02**: All data stored in embedded SQLite

---

## 7. Deferred Features (v2+)

### V2
- **MULTI-01–03**: Multi-group support (private chat room selection, admin invite/remove)
- **ANLT-01–02**: Price history tracking, monthly spending summary per category

### V3
- **VISL-01**: Barcode scan → pre-fill inventory fields
- **VISL-02**: Receipt photo → bulk-add purchased products
- **VISL-03**: Meal photo → calorie/nutrition estimate
- **VISL-04**: All vision via `IAIProvider` (OpenAI / Claude / Ollama)

### V4
- **NLU-01–03**: Free-form natural language interaction, multilingual (RU + EN)

### Out of Scope (all versions)
- Web interface (Telegram only)
- Standalone mobile app
- Public SaaS with billing
- Real-time inventory camera

---

## 8. Implementation Progress (as of extraction)

| Phase | Feature | Status |
|-------|---------|--------|
| 1 | Foundation (AOT bot, Docker, SQLite, IAIProvider) | ✅ Complete |
| 2 | Shopping List MVP (GRP-01/02, SHOP-01–04) | ✅ Complete |
| 3 | Inventory Core (SHOP-05, INV-01–04, SHL-01) | ✅ Complete |
| 4 | Meal Planning (MEAL-01–04) | ✅ Complete |
| 5 | Expiry Notifications (NOT-01–03, SHL-02) | 🔧 In progress (files created, not registered) |

Phase 5 code exists in:
- `src/FamilyStockBot.Infrastructure/Notifications/` — `ExpiryAlert`, `ExpiryAlertService`
- `src/FamilyStockBot.TgBot/Handlers/Notifications/` — `ExtendExpiryCallbackHandler`
- `src/FamilyStockBot.TgBot/Services/ExpiryCheckingService.cs` — background hosted service

Still needed: migration registration, DI wiring in `Program.cs`, daily digest logic.

---

## 9. Docker & Deployment

```yaml
# docker-compose.yml (structure)
services:
  bot:
    build: .
    env_file: .env          # Telegram:BotToken, etc.
    volumes:
      - sqlite-data:/data   # SQLite file persists here
    restart: unless-stopped

volumes:
  sqlite-data:
```

```dockerfile
# Dockerfile (multi-stage AOT)
# Stage 1: build + AOT publish (linux-x64)
# Stage 2: runtime (mcr.microsoft.com/dotnet/runtime-deps)
# SQLite file path: /data/app.db (configurable via appsettings)
```

---

*Extracted: 2026-05-07 from product-tracker-bot (Phases 1–4 complete, Phase 5 in progress)*
