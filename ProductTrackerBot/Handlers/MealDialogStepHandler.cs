// <copyright file="MealDialogStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Processes dialog steps for meal creation and ingredient/step addition.
/// </summary>
public class MealDialogStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PendingDialogService<MealCreateDialogState> mealCreateDialogService;
    private readonly PendingDialogService<MealAddIngredientDialogState> mealIngredientDialogService;
    private readonly PendingDialogService<MealAddStepDialogState> mealStepDialogService;
    private readonly MealRepository mealRepository;
    private readonly MealIngredientRepository ingredientRepository;
    private readonly MealStepRepository stepRepository;
    private readonly ILogger<MealDialogStepHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealDialogStepHandler"/> class.
    /// </summary>
    public MealDialogStepHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PendingDialogService<MealCreateDialogState> mealCreateDialogService,
        PendingDialogService<MealAddIngredientDialogState> mealIngredientDialogService,
        PendingDialogService<MealAddStepDialogState> mealStepDialogService,
        MealRepository mealRepository,
        MealIngredientRepository ingredientRepository,
        MealStepRepository stepRepository,
        ILogger<MealDialogStepHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.mealCreateDialogService = mealCreateDialogService;
        this.mealIngredientDialogService = mealIngredientDialogService;
        this.mealStepDialogService = mealStepDialogService;
        this.mealRepository = mealRepository;
        this.ingredientRepository = ingredientRepository;
        this.stepRepository = stepRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(long chatId, long userId)
    {
        return this.mealCreateDialogService.GetState(chatId, userId) is not null
            || this.mealIngredientDialogService.GetState(chatId, userId) is not null
            || this.mealStepDialogService.GetState(chatId, userId) is not null;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null || message.Text is null)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From.Id;

        // Check for meal creation dialog
        var mealCreateState = this.mealCreateDialogService.GetState(chatId, userId);
        if (mealCreateState is not null)
        {
            await this.HandleMealCreationAsync(message, cancellationToken);
            return;
        }

        // Check for ingredient addition dialog
        var ingredientState = this.mealIngredientDialogService.GetState(chatId, userId);
        if (ingredientState is not null)
        {
            await this.HandleIngredientAdditionAsync(message, ingredientState, cancellationToken);
            return;
        }

        // Check for step addition dialog
        var stepState = this.mealStepDialogService.GetState(chatId, userId);
        if (stepState is not null)
        {
            await this.HandleStepAdditionAsync(message, stepState, cancellationToken);
            return;
        }
    }

    private async Task HandleMealCreationAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var mealName = message.Text!.Trim();

        if (string.IsNullOrWhiteSpace(mealName))
        {
            await this.botClient.SendMessage(
                chatId,
                "❌ Meal name cannot be empty. Please try again:",
                cancellationToken: cancellationToken);
            return;
        }

        this.mealCreateDialogService.ClearState(chatId, userId);

        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var meal = await this.mealRepository.AddAsync(group.Id, mealName);

        await this.botClient.SendMessage(
            chatId,
            $"✅ Meal '{mealName}' created!",
            cancellationToken: cancellationToken);
    }

    private async Task HandleIngredientAdditionAsync(
        Message message,
        MealAddIngredientDialogState state,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var input = message.Text!.Trim();

        if (state.Step == 1)
        {
            // Step 1: collecting ingredient name
            state.Name = input;
            state.Step = 2;

            await this.botClient.SendMessage(
                chatId,
                $"Quantity for '{input}'? (or /skip to skip)",
                cancellationToken: cancellationToken);
        }
        else if (state.Step == 2)
        {
            // Step 2: collecting quantity
            var quantity = input.Equals("/skip", StringComparison.OrdinalIgnoreCase) ? null : input;

            await this.ingredientRepository.AddAsync(state.MealId, state.Name!, quantity);
            this.mealIngredientDialogService.ClearState(chatId, userId);

            var quantityText = quantity is null ? "no quantity" : $"({quantity})";
            await this.botClient.SendMessage(
                chatId,
                $"✅ Added ingredient: {state.Name} {quantityText}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStepAdditionAsync(
        Message message,
        MealAddStepDialogState state,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var stepText = message.Text!.Trim();

        if (string.IsNullOrWhiteSpace(stepText))
        {
            await this.botClient.SendMessage(
                chatId,
                "❌ Step text cannot be empty. Please try again:",
                cancellationToken: cancellationToken);
            return;
        }

        await this.stepRepository.AddAsync(state.MealId, stepText);
        this.mealStepDialogService.ClearState(chatId, userId);

        await this.botClient.SendMessage(
            chatId,
            $"✅ Added step: {stepText}",
            cancellationToken: cancellationToken);
    }
}
