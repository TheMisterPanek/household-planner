## Overview

Two independent features share a common pattern: **parse → review → persist**.

1. **Add review flow** — wraps all `/buy` entry paths (inline and dialog) in a confirmation step.
2. **Edit existing items** — adds [✏️] buttons to the list view and a matching edit → review → update flow.

No AI is needed. Regex handles the parsing; inline keyboard buttons handle the UX.

---

## 1. Smart Parser — `BuyInputParser`

**Problem**: `LastIndexOf(' ')` treats the last word as the quantity. This misidentifies unit words (`штуку`, `пачку`) and leaves prefixes (`купить`) in the name.

**Solution**: A static class `BuyInputParser` with `Parse(string rawText, out string name, out string? quantity)`.

### Regex strategy

```
^(?:купить\s+)?         # strip common prefix "купить " (optional)
(?<name>.+?)            # item name (non-greedy)
(?:\s+(?<qty>           # trailing quantity (optional):
  \d+(?:[.,]\d+)?       #   number (integer or decimal)
  (?:\s*(?:             #   optional unit:
    шт(?:уку?|\.)?|     #     shт/штук/штуку/шт.
    кг|г|               #     кг, г
    л|мл|               #     л, мл
    пачк(?:у|и|а)?|     #     пачку/пачки/пачка
    банк(?:у|и|а)?|     #     банку/банки/банка
    бутылк(?:у|и|а)?|   #     бутылку/…
    kg|g|l|ml|pcs|      #     Latin units
    (?:piece|pack)s?     #     English words
  ))?                   
))?
$
```

- If `qty` capture group is non-empty → `quantity = qty`, `name = name group`.
- If `qty` is empty but the last token is a standalone integer → treat it as quantity (e.g. `отривин 2`).
- Strip any remaining leading/trailing whitespace.
- If the raw text has no recognizable quantity pattern, set `quantity = null` and `name = full text`.

**Input examples**:

| Raw input | `name` | `quantity` |
|---|---|---|
| `купить отривин 1 штуку` | `отривин` | `1 шт` |
| `отривин 1 штуку` | `отривин` | `1 шт` |
| `молоко 2 л` | `молоко` | `2 л` |
| `хлеб` | `хлеб` | `null` |
| `хлеб 2` | `хлеб` | `2` |
| `купить зубная паста colgate max fresh` | `зубная паста colgate max fresh` | `null` |

---

## 2. Add Review Flow

### Sequence — inline `/buy`

```
User: /buy отривин 1 штуку
↓
BuyCommandHandler
  BuyInputParser.Parse → name="отривин", qty="1 шт"
  token = PendingAddService.Store(PendingAddItem { GroupId, name, qty, AddedByName, ChatId })
  Bot → "Add: отривин ×1 шт?\n[✓ Add]  [✏️ Edit]  [✕ Cancel]"
           buy:confirm:{token}   buy:edit:{token}   buy:cancel:{token}

[✓ Add] → BuyConfirmCallbackHandler
  item = PendingAddService.Get(token) → PendingAddService.Clear(token)
  ShoppingItemRepository.AddAsync(...)
  IHistoryRepository.RecordAsync(ItemAdded)
  Bot → "✓ Pavel added: отривин (1 шт)"

[✏️ Edit] → BuyEditCallbackHandler
  item = PendingAddService.Get(token) → PendingAddService.Clear(token)
  BuyDialogState { Step=1, IsOneLineMode=true, GroupId, AddedByName }
  PendingDialogService<BuyDialogState>.SetState(chatId, userId, state)
  Bot → "Enter corrected item and quantity:"

  User: "отривин 1 шт"
  → BuyStepHandler (step=1, IsOneLineMode=true)
    BuyInputParser.Parse → name="отривин", qty="1 шт"
    token2 = PendingAddService.Store(...)
    Bot → "Add: отривин ×1 шт?\n[✓ Add]  [✕ Cancel]"

[✕ Cancel] → BuyCancelCallbackHandler
  PendingAddService.Clear(token)
  Bot → "Cancelled."
```

### Sequence — dialog `/buy` (no inline args)

```
User: /buy
↓
BuyCommandHandler → dialogService.SetState(Step=1)
Bot → "What do you want to buy?"

User: "отривин"
↓
BuyStepHandler (step=1) → state.Name="отривин", state.Step=2
Bot → "How much? [Skip]"

User: "1 штуку"
↓
BuyStepHandler (step=2)
  state.Quantity = "1 штуку"  (user-typed, not parsed — shown as-is in review)
  token = PendingAddService.Store(...)
  Bot → "Add: отривин ×1 штуку?\n[✓ Add]  [✏️ Edit]  [✕ Cancel]"
```

> **Note**: Dialog step 2 stores the quantity string verbatim — the user explicitly typed it so the review is a safety net, not a reparse. The edit path still uses `BuyInputParser` if the user rewrites it in one-line mode.

### `PendingAddService`

```csharp
public sealed class PendingAddService
{
    private readonly Dictionary<string, PendingAddItem> _pending = new();

    public string Store(PendingAddItem item)
    {
        var token = GenerateToken();
        _pending[token] = item;
        return token;
    }

    public PendingAddItem? Get(string token) => _pending.GetValueOrDefault(token);

    public void Clear(string token) => _pending.Remove(token);

    private static string GenerateToken()
        => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
```

- Registered as `AddSingleton<PendingAddService>`.
- No persistence; tokens expire when the process restarts (acceptable for a household bot).

### Callback data sizes

| Callback | Example | Bytes |
|---|---|---|
| `buy:confirm:{token}` | `buy:confirm:a1b2c3d4` | 20 |
| `buy:edit:{token}` | `buy:edit:a1b2c3d4` | 17 |
| `buy:cancel:{token}` | `buy:cancel:a1b2c3d4` | 19 |

All well under the 64-byte Telegram limit.

---

## 3. Edit Existing Item Flow

### Sequence

```
User: /list
↓ ShoppingListService.BuildListAsync
Bot → list message with per-item buttons:
  [✓ отривин 1 шт]  [✏️]  [🗑 Remove]
   shop:done:{id}   item:edit:{id}   shop:remove:{id}

User taps [✏️] on item id=42 (name="отривин", qty="1 шт")
↓ ItemEditCallbackHandler (prefix "item:edit:")
  item = ShoppingItemRepository.GetByIdAsync(42)
  EditItemDialogState { ItemId=42, GroupId=..., OriginalName="отривин", OriginalQuantity="1 шт" }
  PendingDialogService<EditItemDialogState>.SetState(chatId, userId, state)
  Bot.AnswerCallbackQuery(...)
  Bot → "✏️ Editing: отривин (1 шт)\nEnter new name and quantity:"

User: "отривин 2 шт"
↓ ItemEditStepHandler (IDialogMessageHandler)
  BuyInputParser.Parse → name="отривин", qty="2 шт"
  PendingDialogService<EditItemDialogState>.ClearState(...)
  token = PendingEditService.Store(PendingEditItem { ItemId=42, GroupId=..., Name="отривин", Quantity="2 шт" })
  Bot → "Save: отривин ×2 шт?\n[✓ Save]  [✕ Cancel]"
          item:save:{token}   item:cancel-edit:{token}

[✓ Save] → ItemSaveCallbackHandler (prefix "item:save:")
  pending = PendingEditService.Get(token) → PendingEditService.Clear(token)
  ShoppingItemRepository.UpdateAsync(pending.ItemId, pending.Name, pending.Quantity)
  IHistoryRepository.RecordAsync(ItemEdited)
  Refresh list message
  Bot → "✏️ Updated: отривин (2 шт)"

[✕ Cancel] → ItemCancelEditCallbackHandler (prefix "item:cancel-edit:")
  PendingEditService.Clear(token)
  Bot → "Cancelled."
```

### `ShoppingListService` button layout change

Current per-item row:
```
[✓ отривин 1 шт] [Remove]
```

New per-item row:
```
[✓ отривин 1 шт] [✏️] [Remove]
```

The edit button label is just `"✏️"` (emoji only) to keep the row width manageable. Callback: `item:edit:{itemId}`.

Callback size: `item:edit:42` = 12 bytes ✓.

### `ShoppingItemRepository.UpdateAsync`

```csharp
public virtual async Task UpdateAsync(int itemId, string name, string? quantity)
{
    await using var connection = new SqliteConnection(this.connectionString);
    await connection.OpenAsync();
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = "UPDATE ShoppingItems SET Name = @name, Quantity = @quantity WHERE Id = @id";
    cmd.Parameters.AddWithValue("@name", name);
    cmd.Parameters.AddWithValue("@quantity", (object?)quantity ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@id", itemId);
    await cmd.ExecuteNonQueryAsync();
}
```

No schema changes — uses existing `Name` and `Quantity` columns.

---

## 4. New Models

```
PendingAddItem    { long ChatId, int GroupId, string Name, string? Quantity, string AddedByName }
PendingEditItem   { long ChatId, int ItemId, int GroupId, string Name, string? Quantity }
EditItemDialogState { int ItemId, int GroupId, string OriginalName, string? OriginalQuantity }
```

`PendingAddItem` and `PendingEditItem` are not serialized to SQLite; they are pure in-memory records. No `BotActionPayloadContext` registration needed for these.

---

## 5. Localization Keys

| Key | EN | Purpose |
|---|---|---|
| `buy.review-add` | `Add: {item}?` | Review prompt (no qty) |
| `buy.review-add-qty` | `Add: {item} ×{quantity}?` | Review prompt (with qty) |
| `buy.btn-confirm` | `✓ Add` | Confirm button |
| `buy.btn-edit` | `✏️ Edit` | Edit button (review) |
| `buy.btn-cancel` | `✕ Cancel` | Cancel button |
| `buy.cancelled` | `Cancelled.` | After cancel |
| `buy.edit-prompt` | `Enter corrected item and quantity:` | Prompt after Edit tap |
| `item.edit-prompt` | `✏️ Editing: {item}\nEnter new name and quantity:` | Edit existing item prompt |
| `item.review-save` | `Save: {item}?` | Edit review (no qty) |
| `item.review-save-qty` | `Save: {item} ×{quantity}?` | Edit review (with qty) |
| `item.btn-save` | `✓ Save` | Save button |
| `item.btn-cancel` | `✕ Cancel` | Cancel edit button |
| `item.saved` | `✏️ Updated: {item}` | After save (no qty) |
| `item.saved-qty` | `✏️ Updated: {item} ({quantity})` | After save (with qty) |
| `item.edit-button` | `✏️` | Per-item list button |

All keys added to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`.

---

## 6. Registration in `Program.cs`

New singletons:
```csharp
builder.Services.AddSingleton<PendingAddService>();
builder.Services.AddSingleton<PendingEditService>();
```

New scoped handlers:
```csharp
builder.Services.AddScoped<ICallbackHandler, BuyConfirmCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, BuyEditCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, BuyCancelCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ItemEditCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ItemSaveCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ItemCancelEditCallbackHandler>();
builder.Services.AddScoped<IDialogMessageHandler, ItemEditStepHandler>();
```

`BuyStepHandler` and `BuyCommandHandler` are already registered; only their internals change.

---

## 7. AOT Compatibility

- `BuyInputParser` uses only `System.Text.RegularExpressions` with `[GeneratedRegex]` attribute — fully AOT-safe.
- No new reflection-based serialization. `PendingAddItem` and `PendingEditItem` are in-memory only.
- `EditItemDialogState` stored in `PendingDialogService<T>` (in-memory dictionary) — no serialization needed.
- All Telegram API calls use the existing `ITelegramBotClient` interface.
