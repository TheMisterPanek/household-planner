// <copyright file="MealsCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /meals command — loads and displays meals for a group.
/// </summary>
public class MealsCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly MealRepository mealRepository;
    private readonly ILogger<MealsCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealsCommandHandler"/> class.
    /// </summary>
    public MealsCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        MealRepository mealRepository,
        ILogger<MealsCommandHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.mealRepository = mealRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/meals";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup)
        {
            await this.botClient.SendTextMessageAsync(
                message.Chat.Id,
                "❌ The /meals command only works in group chats.",
                cancellationToken: cancellationToken);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
        var meals = await this.mealRepository.GetAllAsync(group.Id);

        var keyboard = new List<List<InlineKeyboardButton>>();

        foreach (var meal in meals)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"View {meal.Name}", $"meal:view:{meal.Id}"),
            });
        }

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("➕ New Meal", "meal:new"),
        });

        var markup = new InlineKeyboardMarkup(keyboard);

        var text = meals.Count == 0
            ? "📋 No meals yet. Create one with [➕ New Meal]!"
            : $"📋 Meals ({meals.Count}):";

        await this.botClient.SendTextMessageAsync(
            message.Chat.Id,
            text,
            replyMarkup: markup,
            cancellationToken: cancellationToken);
    }
}
