// <copyright file="AiQueryService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;

/// <summary>
/// Two-round AI query service: Round 1 generates SQL, Round 2 formats results as natural language.
/// </summary>
public class AiQueryService : IAiQueryService
{
    private readonly IOpenRouterClient openRouterClient;
    private readonly string connectionString;
    private readonly ILocalizer localizer;
    private readonly ILogger<AiQueryService> logger;
    private readonly string identityTemplate;
    private readonly string model;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiQueryService"/> class.
    /// </summary>
    /// <param name="openRouterClient">The OpenRouter HTTP client.</param>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="options">AI query configuration (used to load IDENTITY.md).</param>
    /// <param name="localizer">Localizer for error messages.</param>
    /// <param name="logger">Logger.</param>
    public AiQueryService(
        IOpenRouterClient openRouterClient,
        string connectionString,
        AiQueryOptions options,
        ILocalizer localizer,
        ILogger<AiQueryService> logger)
    {
        this.openRouterClient = openRouterClient;
        this.connectionString = connectionString;
        this.localizer = localizer;
        this.logger = logger;
        this.identityTemplate = File.ReadAllText(options.IdentityMdPath);
        this.model = options.Model;
    }

    /// <inheritdoc/>
    public async Task<string> AnswerAsync(long chatId, long groupId, string question, CancellationToken ct)
    {
        var systemPrompt = this.identityTemplate
            .Replace("{groupId}", groupId.ToString(), StringComparison.Ordinal)
            .Replace("{chatId}", chatId.ToString(), StringComparison.Ordinal);

        // Round 1: generate SQL
        string? sqlResponse;
        try
        {
            sqlResponse = await this.openRouterClient.CompleteAsync(this.model, systemPrompt, question, ct);
        }
        catch (HttpRequestException ex)
        {
            this.logger.LogWarning(ex, "OpenRouter HTTP error in Round 1 for chat {ChatId}", chatId);
            return this.localizer.Get(chatId, "ai.error.service-unavailable");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            this.logger.LogWarning(ex, "OpenRouter timeout in Round 1 for chat {ChatId}", chatId);
            return this.localizer.Get(chatId, "ai.error.service-unavailable");
        }

        if (string.IsNullOrWhiteSpace(sqlResponse))
        {
            return this.localizer.Get(chatId, "ai.error.no-result");
        }

        var sql = ExtractSql(sqlResponse);

        if (!sql.Contains("@groupId", StringComparison.OrdinalIgnoreCase) &&
            !sql.Contains("@chatId", StringComparison.OrdinalIgnoreCase))
        {
            this.logger.LogWarning("AI generated SQL without required scoping placeholder. Chat={ChatId}", chatId);
            return this.localizer.Get(chatId, "ai.error.unsafe-query");
        }

        if (!sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
        {
            sql = sql.TrimEnd(';', ' ') + " LIMIT 50";
        }

        string formattedResults;
        try
        {
            formattedResults = await this.ExecuteQueryAsync(sql, groupId, chatId, ct);
        }
        catch (SqliteException ex)
        {
            this.logger.LogWarning(ex, "SQLite error executing AI-generated query for chat {ChatId}", chatId);
            formattedResults = "[Query could not be executed due to a SQL error. Please rephrase your question.]";
        }

        // Round 2: format results as natural language
        var round2Message =
            $"The user asked: \"{question}\"\n\nQuery results:\n{formattedResults}\n\n" +
            "Please answer the user's question based on these results in a friendly, natural language response.";

        string? answer;
        try
        {
            answer = await this.openRouterClient.CompleteAsync(this.model, systemPrompt, round2Message, ct);
        }
        catch (HttpRequestException ex)
        {
            this.logger.LogWarning(ex, "OpenRouter HTTP error in Round 2 for chat {ChatId}", chatId);
            return this.localizer.Get(chatId, "ai.error.service-unavailable");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            this.logger.LogWarning(ex, "OpenRouter timeout in Round 2 for chat {ChatId}", chatId);
            return this.localizer.Get(chatId, "ai.error.service-unavailable");
        }

        return answer ?? this.localizer.Get(chatId, "ai.error.no-result");
    }

    private static string ExtractSql(string response)
    {
        var trimmed = response.Trim();

        // Strip markdown code fences (```sql ... ``` or ``` ... ```)
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = trimmed.IndexOf('\n');
            if (newline >= 0)
            {
                trimmed = trimmed[(newline + 1)..];
            }

            var endFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (endFence >= 0)
            {
                trimmed = trimmed[..endFence];
            }
        }

        return trimmed.Trim();
    }

    private async Task<string> ExecuteQueryAsync(string sql, long groupId, long chatId, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA query_only = ON";
        await pragmaCmd.ExecuteNonQueryAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@chatId", chatId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var colNames = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(" | ", colNames));

        int rowCount = 0;
        while (await reader.ReadAsync(ct))
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "NULL" : (reader.GetValue(i).ToString() ?? "NULL"));
            sb.AppendLine(string.Join(" | ", values));
            rowCount++;

            if (sb.Length > 3000)
            {
                sb.AppendLine("[... truncated]");
                break;
            }
        }

        return rowCount == 0 ? "(no rows)" : sb.ToString();
    }
}
