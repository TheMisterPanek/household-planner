## 1. Implementation

- [ ] 1.1 In `StartCommandHandler.HandleAsync`, send the welcome message first, then call `botClient.SetMyCommandsAsync` with a `BotCommand[]` built from handlers that have a non-null `Description`; skip the call when the list is empty
- [ ] 1.2 Wrap the `SetMyCommandsAsync` call in try/catch; log a Warning on failure and do not rethrow

## 2. Tests

- [ ] 2.1 Add test: `/start` with multiple public handlers calls `SetMyCommandsAsync` with the correct `BotCommand` list (command text and description match each handler)
- [ ] 2.2 Add test: `/start` with no public handlers does NOT call `SetMyCommandsAsync`
- [ ] 2.3 Add test: when `SetMyCommandsAsync` throws, the exception is swallowed and `SendMessage` (the welcome) was already called
