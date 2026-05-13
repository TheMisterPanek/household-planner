// <copyright file="UndoService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

/// <summary>
/// Orchestrates inverse operations for undoable history entries.
/// </summary>
public class UndoService : IUndoService
{
    private readonly IHistoryRepository historyRepository;
    private readonly ShoppingItemRepository shoppingItemRepository;
    private readonly GroupRepository groupRepository;
    private readonly ILogger<UndoService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UndoService"/> class.
    /// </summary>
    public UndoService(
        IHistoryRepository historyRepository,
        ShoppingItemRepository shoppingItemRepository,
        GroupRepository groupRepository,
        ILogger<UndoService> logger)
    {
        this.historyRepository = historyRepository;
        this.shoppingItemRepository = shoppingItemRepository;
        this.groupRepository = groupRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> UndoLastAsync(long chatId, long userId, CancellationToken ct)
    {
        var entry = await this.historyRepository.GetLatestReversibleAsync(chatId, userId, ct);
        if (entry is null)
        {
            return "undo.nothing";
        }

        RevertOperation? operation;
        try
        {
            operation = entry.ActionType switch
            {
                BotActionType.ItemBought => JsonSerializer.Deserialize(entry.RevertPayload!, BotActionPayloadContext.Default.ItemBoughtRevert),
                BotActionType.ItemRemoved => JsonSerializer.Deserialize(entry.RevertPayload!, BotActionPayloadContext.Default.ItemRemovedRevert),
                BotActionType.ListArchived => JsonSerializer.Deserialize(entry.RevertPayload!, BotActionPayloadContext.Default.ListArchivedRevert),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to deserialize revert_payload for history entry {EntryId}", entry.Id);
            return "undo.error";
        }

        if (operation is null)
        {
            this.logger.LogWarning("Unsupported action type for undo: {ActionType} (entry {EntryId})", entry.ActionType, entry.Id);
            return "undo.error";
        }

        try
        {
            switch (operation)
            {
                case ItemBoughtRevert bought:
                    await this.shoppingItemRepository.AddAsync(bought.ListId, bought.ItemName, bought.Quantity, "Undo");
                    break;
                case ItemRemovedRevert removed:
                    await this.shoppingItemRepository.AddAsync(removed.ListId, removed.ItemName, removed.Quantity, "Undo");
                    break;
                case ListArchivedRevert:
                    this.logger.LogWarning("ListArchivedRevert is not yet supported — no unarchive method available");
                    return "undo.error";
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to apply inverse operation for history entry {EntryId}", entry.Id);
            return "undo.error";
        }

        await this.historyRepository.MarkRevertedAsync(entry.Id, DateTime.UtcNow.ToString("O"), ct);
        return "undo.success";
    }
}
