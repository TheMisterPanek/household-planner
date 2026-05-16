// <copyright file="AiCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the <c>/ai</c> command and exposes shared question-answering logic for bot mention routing.
/// </summary>
public class AiCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly IAiQueryService aiQueryService;
    private readonly ConversationHistoryService conversationHistory;
    private readonly ILocalizer localizer;
    private readonly ILogger<AiCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">Telegram bot client.</param>
    /// <param name="groupRepository">Group repository to resolve GroupId.</param>
    /// <param name="aiQueryService">AI query service.</param>
    /// <param name="conversationHistory">Short-term conversation memory service.</param>
    /// <param name="localizer">Localizer.</param>
    /// <param name="logger">Logger.</param>
    public AiCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        IAiQueryService aiQueryService,
        ConversationHistoryService conversationHistory,
        ILocalizer localizer,
        ILogger<AiCommandHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.aiQueryService = aiQueryService;
        this.conversationHistory = conversationHistory;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/ai";

    /// <inheritdoc/>
    public string? Description => "Ask a question about your shopping data";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var question = ExtractQuestion(message.Text);
        await this.ExecuteQueryAsync(message, question, cancellationToken);
    }

    private async Task ExecuteQueryAsync(Message message, string question, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: this.localizer.Get(message.Chat.Id, "ai.usage-hint"),
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        this.logger.LogInformation("AI query from chat {ChatId}: {Question}", message.Chat.Id, question);

        var recentContext = this.conversationHistory.GetRecentContext(message.Chat.Id);
        this.conversationHistory.Record(message.Chat.Id, $"User: {question}");

        using var typingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var typingTask = this.SendTypingLoopAsync(message.Chat.Id, typingCts.Token);

        string answer;
        try
        {
            var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
            answer = await this.aiQueryService.AnswerAsync(message.Chat.Id, group.Id, question, recentContext, cancellationToken);
        }
        finally
        {
            await typingCts.CancelAsync();
            try { await typingTask; } catch (OperationCanceledException) { }
        }

        this.conversationHistory.Record(message.Chat.Id, $"Bot: {answer}");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: answer,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task SendTypingLoopAsync(long chatId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await this.botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
                await Task.Delay(4000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to send typing indicator to chat {ChatId}", chatId);
                break;
            }
        }
    }

    private static string ExtractQuestion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var spaceIdx = text.IndexOf(' ');
        if (spaceIdx < 0)
        {
            return string.Empty;
        }

        return text[(spaceIdx + 1)..].Trim();
    }
}
