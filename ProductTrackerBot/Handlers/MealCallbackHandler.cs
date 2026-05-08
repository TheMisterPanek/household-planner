// <copyright file="MealCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles all meal-related callback queries (meal:*).
/// </summary>
public class MealCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly MealRepository mealRepository;
    private readonly MealIngredientRepository ingredientRepository;
    private readonly MealStepRepository stepRepository;
    private readonly ShoppingItemRepository shoppingItemRepository;
    private readonly PendingDialogService<MealCreateDialogState> mealCreateDialogService;
    private readonly PendingDialogService<MealAddIngredientDialogState> ingredientDialogService;
    private readonly PendingDialogService<MealAddStepDialogState> stepDialogService;
    private readonly MealMergeService mergeService;
    private readonly ILogger<MealCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealCallbackHandler"/> class.
    /// </summary>
    public MealCallbackHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        MealRepository mealRepository,
        MealIngredientRepository ingredientRepository,
        MealStepRepository stepRepository,
        ShoppingItemRepository shoppingItemRepository,
        PendingDialogService<MealCreateDialogState> mealCreateDialogService,
        PendingDialogService<MealAddIngredientDialogState> ingredientDialogService,
        PendingDialogService<MealAddStepDialogState> stepDialogService,
        MealMergeService mergeService,
        ILogger<MealCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.mealRepository = mealRepository;
        this.ingredientRepository = ingredientRepository;
        this.stepRepository = stepRepository;
        this.shoppingItemRepository = shoppingItemRepository;
        this.mealCreateDialogService = mealCreateDialogService;
        this.ingredientDialogService = ingredientDialogService;
        this.stepDialogService = stepDialogService;
        this.mergeService = mergeService;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "meal:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null)
        {
            return;
        }

        var data = callbackQuery.Data;
        var parts = data.Split(':');

        if (parts.Length < 2)
        {
            return;
        }

        var action = parts[1];

        try
        {
            switch (action)
            {
                case "list":
                    await this.HandleMealListAsync(callbackQuery, cancellationToken);
                    break;
                case "view":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var mealId))
                    {
                        await this.HandleMealViewAsync(callbackQuery, mealId, cancellationToken);
                    }

                    break;
                case "new":
                    await this.HandleNewMealAsync(callbackQuery, cancellationToken);
                    break;
                case "delete":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var deleteMealId))
                    {
                        await this.HandleDeleteConfirmAsync(callbackQuery, deleteMealId, cancellationToken);
                    }

                    break;
                case "delete_confirm":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var confirmMealId))
                    {
                        await this.HandleDeleteConfirmExecuteAsync(callbackQuery, confirmMealId, cancellationToken);
                    }

                    break;
                case "add_ingredient":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var ingredMealId))
                    {
                        await this.HandleAddIngredientAsync(callbackQuery, ingredMealId, cancellationToken);
                    }

                    break;
                case "ingredient_skip":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var skipMealId))
                    {
                        await this.HandleIngredientSkipAsync(callbackQuery, skipMealId, cancellationToken);
                    }

                    break;
                case "remove_ingredient":
                    if (parts.Length >= 4 && int.TryParse(parts[2], out var rmMealId) && int.TryParse(parts[3], out var ingredientId))
                    {
                        await this.HandleRemoveIngredientAsync(callbackQuery, rmMealId, ingredientId, cancellationToken);
                    }

                    break;
                case "add_step":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var stepMealId))
                    {
                        await this.HandleAddStepAsync(callbackQuery, stepMealId, cancellationToken);
                    }

                    break;
                case "remove_step":
                    if (parts.Length >= 4 && int.TryParse(parts[2], out var rmStepMealId) && int.TryParse(parts[3], out var stepId))
                    {
                        await this.HandleRemoveStepAsync(callbackQuery, rmStepMealId, stepId, cancellationToken);
                    }

                    break;
                case "to_list":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var toListMealId))
                    {
                        await this.HandleAddToListAsync(callbackQuery, toListMealId, cancellationToken);
                    }

                    break;
                case "merge":
                    if (parts.Length >= 3)
                    {
                        var mergeAction = parts[2];
                        if (mergeAction == "skip" || mergeAction == "add")
                        {
                            if (parts.Length >= 5 && int.TryParse(parts[4], out var conflictIndex))
                            {
                                var sessionId = parts[3];
                                await this.HandleMergeConflictAsync(callbackQuery, sessionId, conflictIndex, mergeAction == "add", cancellationToken);
                            }
                        }
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error handling meal callback: {Data}", data);
        }

        await this.botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleMealListAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var group = await this.groupRepository.GetOrCreateAsync(chatId);
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

        await this.botClient.EditMessageTextAsync(
            chatId,
            callbackQuery.Message.MessageId,
            text,
            replyMarkup: markup,
            cancellationToken: cancellationToken);
    }

    private async Task HandleMealViewAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var meal = await this.mealRepository.GetByIdAsync(mealId);

        if (meal is null)
        {
            await this.botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Meal not found", cancellationToken: cancellationToken);
            return;
        }

        var ingredients = await this.ingredientRepository.GetAllAsync(mealId);
        var steps = await this.stepRepository.GetAllAsync(mealId);

        var text = this.BuildMealDetailText(meal, ingredients, steps);
        var keyboard = this.BuildMealDetailKeyboard(mealId, ingredients.Count, steps.Count);

        await this.botClient.EditMessageTextAsync(
            chatId,
            callbackQuery.Message.MessageId,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleNewMealAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message!.Chat.Id;

        this.mealCreateDialogService.SetState(chatId, userId, new MealCreateDialogState { Step = 1 });

        await this.botClient.SendTextMessageAsync(
            chatId,
            "What's the meal name?",
            cancellationToken: cancellationToken);
    }

    private async Task HandleDeleteConfirmAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Yes, delete", $"meal:delete_confirm:{mealId}") },
            new[] { InlineKeyboardButton.WithCallbackData("Cancel", "meal:list") },
        });

        await this.botClient.EditMessageTextAsync(
            chatId,
            callbackQuery.Message.MessageId,
            "⚠️ Are you sure you want to delete this meal?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleDeleteConfirmExecuteAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        await this.mealRepository.DeleteAsync(mealId);

        var group = await this.groupRepository.GetOrCreateAsync(chatId);
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

        await this.botClient.EditMessageTextAsync(
            chatId,
            callbackQuery.Message.MessageId,
            text,
            replyMarkup: markup,
            cancellationToken: cancellationToken);
    }

    private async Task HandleAddIngredientAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message!.Chat.Id;

        var state = new MealAddIngredientDialogState { Step = 1, MealId = mealId };
        this.ingredientDialogService.SetState(chatId, userId, state);

        await this.botClient.SendTextMessageAsync(
            chatId,
            "What ingredient?",
            cancellationToken: cancellationToken);
    }

    private async Task HandleIngredientSkipAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message!.Chat.Id;

        var state = this.ingredientDialogService.GetState(chatId, userId);
        if (state is not null && state.Step == 2 && state.Name is not null)
        {
            await this.ingredientRepository.AddAsync(mealId, state.Name, quantity: null);
            this.ingredientDialogService.ClearState(chatId, userId);

            var meal = await this.mealRepository.GetByIdAsync(mealId);
            var ingredients = await this.ingredientRepository.GetAllAsync(mealId);
            var steps = await this.stepRepository.GetAllAsync(mealId);

            var text = this.BuildMealDetailText(meal!, ingredients, steps);
            var keyboard = this.BuildMealDetailKeyboard(mealId, ingredients.Count, steps.Count);

            await this.botClient.EditMessageTextAsync(
                chatId,
                callbackQuery.Message.MessageId,
                text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleRemoveIngredientAsync(CallbackQuery callbackQuery, int mealId, int ingredientId, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        await this.ingredientRepository.DeleteAsync(ingredientId);

        var meal = await this.mealRepository.GetByIdAsync(mealId);
        var ingredients = await this.ingredientRepository.GetAllAsync(mealId);
        var steps = await this.stepRepository.GetAllAsync(mealId);

        var text = this.BuildMealDetailText(meal!, ingredients, steps);
        var keyboard = this.BuildMealDetailKeyboard(mealId, ingredients.Count, steps.Count);

        await this.botClient.EditMessageTextAsync(
            chatId,
            callbackQuery.Message.MessageId,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleAddStepAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var chatId = callbackQuery.Message!.Chat.Id;

        this.stepDialogService.SetState(chatId, userId, new MealAddStepDialogState { MealId = mealId });

        await this.botClient.SendTextMessageAsync(
            chatId,
            "What's the step?",
            cancellationToken: cancellationToken);
    }

    private async Task HandleRemoveStepAsync(CallbackQuery callbackQuery, int mealId, int stepId, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        await this.stepRepository.DeleteAsync(stepId);

        var meal = await this.mealRepository.GetByIdAsync(mealId);
        var ingredients = await this.ingredientRepository.GetAllAsync(mealId);
        var steps = await this.stepRepository.GetAllAsync(mealId);

        var text = this.BuildMealDetailText(meal!, ingredients, steps);
        var keyboard = this.BuildMealDetailKeyboard(mealId, ingredients.Count, steps.Count);

        await this.botClient.EditMessageTextAsync(
            chatId,
            callbackQuery.Message.MessageId,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleAddToListAsync(CallbackQuery callbackQuery, int mealId, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        var ingredients = await this.ingredientRepository.GetAllAsync(mealId);
        var existingItems = await this.shoppingItemRepository.GetAllAsync(group.Id);

        var conflicts = new List<MergeConflict>();
        var addedCount = 0;

        foreach (var ingredient in ingredients)
        {
            var existing = existingItems.FirstOrDefault(
                i => i.Name.Equals(ingredient.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                conflicts.Add(new MergeConflict
                {
                    IngredientName = ingredient.Name,
                    MealQuantity = ingredient.Quantity,
                    ExistingQuantity = existing.Quantity,
                });
            }
            else
            {
                await this.shoppingItemRepository.AddAsync(
                    group.Id,
                    ingredient.Name,
                    ingredient.Quantity,
                    "Meal");
                addedCount++;
            }
        }

        if (conflicts.Count == 0)
        {
            await this.botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                $"✅ Added {addedCount} items",
                cancellationToken: cancellationToken);
        }
        else
        {
            var sessionId = this.mergeService.CreateSession(chatId, mealId, conflicts);
            var conflictText = this.BuildConflictMessage(conflicts);
            var conflictKeyboard = this.BuildConflictKeyboard(sessionId, conflicts);

            await this.botClient.SendTextMessageAsync(
                chatId,
                conflictText,
                replyMarkup: conflictKeyboard,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleMergeConflictAsync(
        CallbackQuery callbackQuery,
        string sessionId,
        int conflictIndex,
        bool add,
        CancellationToken cancellationToken)
    {
        var session = this.mergeService.TryGetSession(sessionId);
        if (session is null)
        {
            await this.botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "⏰ This request has expired, please tap 🛒 again",
                cancellationToken: cancellationToken);
            return;
        }

        var conflict = session.Conflicts[conflictIndex];
        var group = await this.groupRepository.GetOrCreateAsync(session.ChatId);

        if (add)
        {
            await this.shoppingItemRepository.AddAsync(
                group.Id,
                conflict.IngredientName,
                conflict.MealQuantity,
                "Meal");
        }

        this.mergeService.ResolveConflict(sessionId, conflictIndex, add);

        var unresolved = session.Conflicts
            .Where((_, i) => !session.Resolved.ContainsKey(i))
            .ToList();

        if (unresolved.Count == 0)
        {
            var added = session.Conflicts.Where((_, i) => session.Resolved.GetValueOrDefault(i, false)).ToList();
            var skipped = session.Conflicts.Where((_, i) => !session.Resolved.GetValueOrDefault(i, true)).ToList();

            var summary = this.BuildSummaryMessage(added, skipped);

            await this.botClient.EditMessageTextAsync(
                callbackQuery.Message!.Chat.Id,
                callbackQuery.Message.MessageId,
                summary,
                cancellationToken: cancellationToken);
        }
        else
        {
            var conflictText = this.BuildConflictMessage(unresolved);
            var conflictKeyboard = this.BuildConflictKeyboard(sessionId, unresolved);

            await this.botClient.EditMessageTextAsync(
                callbackQuery.Message!.Chat.Id,
                callbackQuery.Message.MessageId,
                conflictText,
                replyMarkup: conflictKeyboard,
                cancellationToken: cancellationToken);
        }
    }

    private string BuildMealDetailText(Meal meal, List<MealIngredient> ingredients, List<MealStep> steps)
    {
        var lines = new List<string> { $"🍽️ **{meal.Name}**" };

        if (ingredients.Count > 0)
        {
            lines.Add("\n**Ingredients:**");
            foreach (var ing in ingredients)
            {
                var qty = ing.Quantity is null ? "" : $" ({ing.Quantity})";
                lines.Add($"• {ing.Name}{qty}");
            }
        }

        if (steps.Count > 0)
        {
            lines.Add("\n**Steps:**");
            foreach (var step in steps)
            {
                lines.Add($"{step.StepNumber}. {step.Text}");
            }
        }

        return string.Join("\n", lines);
    }

    private InlineKeyboardMarkup BuildMealDetailKeyboard(int mealId, int ingredientCount, int stepCount)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("➕ Ingredient", $"meal:add_ingredient:{mealId}"),
            InlineKeyboardButton.WithCallbackData("➕ Step", $"meal:add_step:{mealId}"),
        });

        if (ingredientCount > 0)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("🛒 Add to list", $"meal:to_list:{mealId}"),
            });
        }

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🗑 Delete", $"meal:delete:{mealId}"),
            InlineKeyboardButton.WithCallbackData("← Back", "meal:list"),
        });

        return new InlineKeyboardMarkup(keyboard);
    }

    private string BuildConflictMessage(List<MergeConflict> conflicts)
    {
        var lines = new List<string> { "⚠️ **Ingredient conflicts:**" };
        foreach (var conflict in conflicts)
        {
            var existing = conflict.ExistingQuantity ?? "not specified";
            var needed = conflict.MealQuantity ?? "not specified";
            lines.Add($"• **{conflict.IngredientName}** — on list: {existing} | needed: {needed}");
        }

        return string.Join("\n", lines);
    }

    private InlineKeyboardMarkup BuildConflictKeyboard(string sessionId, List<MergeConflict> conflicts)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();

        for (int i = 0; i < conflicts.Count; i++)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("✓ Skip", $"meal:merge:skip:{sessionId}:{i}"),
                InlineKeyboardButton.WithCallbackData("+ Add anyway", $"meal:merge:add:{sessionId}:{i}"),
            });
        }

        return new InlineKeyboardMarkup(keyboard);
    }

    private string BuildSummaryMessage(List<MergeConflict> added, List<MergeConflict> skipped)
    {
        var parts = new List<string> { "✅ **Done!**" };

        if (added.Count > 0)
        {
            var names = string.Join(", ", added.Select(c => c.IngredientName));
            parts.Add($"Added: {names}");
        }

        if (skipped.Count > 0)
        {
            var names = string.Join(", ", skipped.Select(c => c.IngredientName));
            parts.Add($"Skipped: {names}");
        }

        return string.Join("\n", parts);
    }
}
