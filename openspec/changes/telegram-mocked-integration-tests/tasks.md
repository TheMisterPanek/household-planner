## 1. Create `TelegramIntegrationTestBase`

- [ ] 1.1 Create `ProductTrackerBot.Tests/Integration/TelegramIntegrationTestBase.cs` as an abstract base class implementing `IDisposable`:
  - Open a named in-memory SQLite connection: `"Data Source=file:integration_test?mode=memory&cache=shared"`
  - Run `DatabaseInitializer.StartAsync(CancellationToken.None)` using the same connection string to initialize the schema
  - Instantiate real repositories: `GroupRepository`, `ShoppingItemRepository`, `HistoryRepository` (and any others needed by handlers below)
  - Instantiate real services: `ShoppingListService`, `UndoService`, `PendingDialogService<BuyDialogState>`, `PendingDialogService<PriceCaptureDialogState>`
  - Instantiate real handlers: `BuyCommandHandler`, `ListCommandHandler`, `StartCommandHandler`, `ShopDoneCallbackHandler`, `ShopRemoveCallbackHandler`, `PriceCaptureStepHandler` — wire all constructor dependencies from the repositories and services above; use `Mock.Of<ILogger<T>>()` for loggers; use a `Mock<ILocalizer>` that returns the key name as the value
  - Build a `UpdateDispatcher` with those handlers and a `Mock.Of<ILogger<UpdateDispatcher>>()`
  - Expose `Mock<ITelegramBotClient> BotMock` configured to return `new Message()` for `SendMessageRequest` and `EditMessageTextRequest`
  - Expose `Task DispatchAsync(Update update)` that calls `UpdateDispatcher.HandleUpdateAsync(BotMock.Object, update, CancellationToken.None)`
  - Expose `Task ClearDataAsync()` that deletes all rows from all tables (call at the start of each test to ensure isolation)
  - Expose static `Update` builder helpers using `JsonSerializer.Deserialize<Update>` with snake_case options:
    - `CommandUpdate(long chatId, long userId, string text)`
    - `CallbackUpdate(long chatId, long userId, int messageId, string data)`
    - `MessageUpdate(long chatId, long userId, string text)`
  - `Dispose()` closes the SQLite connection

## 2. Command dispatch integration tests

- [ ] 2.1 Create `ProductTrackerBot.Tests/Integration/CommandIntegrationTests.cs` extending `TelegramIntegrationTestBase`
- [ ] 2.2 **/start — sends welcome message**: dispatch `CommandUpdate(-100, 42, "/start")`, verify `BotMock.SendRequest` called once with a non-empty text
- [ ] 2.3 **/buy — adds item and sends confirmation**: dispatch `CommandUpdate(-100, 42, "/buy Milk 2l")`, verify `BotMock.SendRequest` called with text containing "Milk", then query `ShoppingItemRepository` and verify the item exists in the database for the group
- [ ] 2.4 **/buy — group created on first use**: dispatch `/buy` command for a chat ID that has never been seen, verify `GroupRepository.GetOrCreateAsync` created a new group row (query the repository directly)
- [ ] 2.5 **/list — shows empty list for new group**: dispatch `CommandUpdate(-100, 42, "/list")`, verify `BotMock.SendRequest` called once (empty list message)
- [ ] 2.6 **/list — shows items after /buy**: dispatch `/buy Eggs` then `/list`, verify the list reply contains "Eggs"

## 3. Callback dispatch integration tests

- [ ] 3.1 Create `ProductTrackerBot.Tests/Integration/CallbackIntegrationTests.cs` extending `TelegramIntegrationTestBase`
- [ ] 3.2 **shop:done — marks item done and sends reply**: seed a group and item in SQLite, dispatch `CallbackUpdate(-100, 42, 99, "shop:done:{itemId}")`, verify `BotMock.SendRequest` (or `EditMessageTextRequest`) is called and the item is no longer returned by `ShoppingItemRepository.GetActiveAsync`
- [ ] 3.3 **shop:remove — removes item and sends reply**: seed a group and item, dispatch `CallbackUpdate(-100, 42, 99, "shop:remove:{itemId}")`, verify item is removed from the repository
- [ ] 3.4 **Unknown callback prefix — no handler called**: dispatch `CallbackUpdate(-100, 42, 99, "unknown:action:1")`, verify `BotMock.SendRequest` is never called

## 4. Dialog state integration tests

- [ ] 4.1 Create `ProductTrackerBot.Tests/Integration/DialogIntegrationTests.cs` extending `TelegramIntegrationTestBase`
- [ ] 4.2 **Price capture — full flow**: dispatch `/buy Milk` to start a buy dialog step, then dispatch a plain `MessageUpdate(-100, 42, "1.99")` as the price capture reply, verify `BotMock.SendRequest` is called for both steps and the item's price is persisted in the repository
- [ ] 4.3 **Dialog message ignored when no pending state**: dispatch a plain `MessageUpdate(-100, 42, "hello")` with no prior dialog in flight, verify `BotMock.SendRequest` is never called

## 5. Manual smoke test

- [ ] 5.1 Start the bot (`make run`) with a real `BOT_TOKEN` and send `/start` in a Telegram chat — verify the welcome message appears
- [ ] 5.2 Send `/buy Milk 2l` — verify the bot confirms the item was added
- [ ] 5.3 Send `/list` — verify Milk 2l appears in the list with a "done" and "remove" inline button
- [ ] 5.4 Tap the "done" button — verify the item disappears from the list
- [ ] 5.5 Send `/buy Eggs`, then respond to the price prompt with a price — verify the price is recorded and the confirmation message is sent
- [ ] 5.6 Send an unknown command (`/foobar`) — verify the bot does not crash and sends no reply
- [ ] 5.7 Send a plain text message outside any dialog — verify the bot does not crash and sends no reply
