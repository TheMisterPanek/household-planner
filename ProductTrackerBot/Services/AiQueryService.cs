// <copyright file="AiQueryService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;

/// <summary>
/// AI query service: Round 1 returns a planning document with zero or more APPLY_READ_SQL templates.
/// Templates are resolved and substituted; Round 2 produces the final answer only when templates were present.
/// </summary>
public partial class AiQueryService : IAiQueryService
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

    [GeneratedRegex(@"ADD_ITEM\s*\(\s*(?<name>[^,)]+?)(?:\s*,\s*(?<count>[^)]+?))?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex AddItemRegex();

    /// <inheritdoc/>
    public async Task<AiQueryResult> AnswerAsync(long chatId, long groupId, string question, string recentContext, string language, CancellationToken ct)
    {
        var systemPrompt = this.identityTemplate
            .Replace("{groupId}", groupId.ToString(), StringComparison.Ordinal)
            .Replace("{chatId}", chatId.ToString(), StringComparison.Ordinal)
            .Replace("{primaryLanguage}", language, StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(recentContext))
        {
            systemPrompt += $"\n\n## Recent Conversation (last 15 min)\n{recentContext}";
        }

        systemPrompt += """


## Item Suggestion Syntax
When your answer naturally leads to specific shopping items (e.g. you suggest a recipe, a meal plan, or a restocking list), you MAY append ADD_ITEM markers to your response — one per item:
  ADD_ITEM(item name, quantity)
  ADD_ITEM(item name)
These will be rendered as "Add to list" buttons in Telegram. Only use them for concrete, buyable items. Place markers at the end of your response. Maximum 8 suggestions.
""";

        // Round 1: get planning document or direct answer
        string? round1Response;
        try
        {
            round1Response = await this.openRouterClient.CompleteAsync(this.model, systemPrompt, question, ct);
        }
        catch (HttpRequestException ex)
        {
            this.logger.LogWarning(ex, "OpenRouter HTTP error in Round 1 for chat {ChatId}", chatId);
            return new AiQueryResult(this.localizer.Get(chatId, "ai.error.service-unavailable"), []);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            this.logger.LogWarning(ex, "OpenRouter timeout in Round 1 for chat {ChatId}", chatId);
            return new AiQueryResult(this.localizer.Get(chatId, "ai.error.service-unavailable"), []);
        }

        if (string.IsNullOrWhiteSpace(round1Response))
        {
            return new AiQueryResult(this.localizer.Get(chatId, "ai.error.no-result"), []);
        }

        var templates = ExtractTemplates(round1Response).ToList();

        // Mode 1 / Mode 3: no templates → return Round 1 response directly (no Round 2 call)
        if (templates.Count == 0)
        {
            if (ContainsSql(round1Response))
            {
                this.logger.LogWarning("Round 1 response contained raw SQL for chat {ChatId}", chatId);
                return new AiQueryResult(this.localizer.Get(chatId, "ai.error.sql-in-response"), []);
            }

            return BuildResult(round1Response);
        }

        // Mode 2: resolve each template
        var resolvedDoc = round1Response;
        foreach (var (fullMatch, sql) in templates)
        {
            if (!ValidateSqlScope(sql))
            {
                this.logger.LogWarning("AI generated SQL without required scoping placeholder in template. Chat={ChatId}", chatId);
                resolvedDoc = resolvedDoc.Replace(fullMatch, "[blocked: SQL must be scoped to @groupId or @chatId]", StringComparison.Ordinal);
                continue;
            }

            var scopedSql = sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                ? sql
                : sql.TrimEnd(';', ' ') + " LIMIT 50";

            string replacement;
            try
            {
                replacement = await this.ExecuteQueryAsync(scopedSql, groupId, chatId, ct);
            }
            catch (SqliteException ex)
            {
                this.logger.LogWarning(ex, "SQLite error executing AI-generated query for chat {ChatId}", chatId);
                replacement = "[query error: SQL could not be executed]";
            }

            resolvedDoc = resolvedDoc.Replace(fullMatch, replacement, StringComparison.Ordinal);
        }

        // Round 2: final answer from resolved planning document
        string? answer;
        try
        {
            answer = await this.openRouterClient.CompleteAsync(this.model, systemPrompt, resolvedDoc, ct);
        }
        catch (HttpRequestException ex)
        {
            this.logger.LogWarning(ex, "OpenRouter HTTP error in Round 2 for chat {ChatId}", chatId);
            return new AiQueryResult(this.localizer.Get(chatId, "ai.error.service-unavailable"), []);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            this.logger.LogWarning(ex, "OpenRouter timeout in Round 2 for chat {ChatId}", chatId);
            return new AiQueryResult(this.localizer.Get(chatId, "ai.error.service-unavailable"), []);
        }

        if (answer != null && ContainsSql(answer))
        {
            this.logger.LogWarning("Round 2 response contained raw SQL for chat {ChatId}", chatId);
            return new AiQueryResult(this.localizer.Get(chatId, "ai.error.sql-in-response"), []);
        }

        return answer is not null ? BuildResult(answer) : new AiQueryResult(this.localizer.Get(chatId, "ai.error.no-result"), []);
    }

    /// <summary>
    /// Extracts ADD_ITEM suggestions from the AI response text.
    /// </summary>
    internal static IReadOnlyList<AiSuggestion> ExtractSuggestions(string text)
    {
        var matches = AddItemRegex().Matches(text);
        if (matches.Count == 0)
        {
            return [];
        }

        var list = new List<AiSuggestion>(matches.Count);
        foreach (Match m in matches)
        {
            var name = m.Groups["name"].Value.Trim();
            var countGroup = m.Groups["count"];
            var count = countGroup.Success ? countGroup.Value.Trim() : null;
            list.Add(new AiSuggestion(name, count));
        }

        return list;
    }

    /// <summary>
    /// Strips ADD_ITEM markers from text, collapses extra blank lines, and trims.
    /// </summary>
    internal static string StripSuggestions(string text)
    {
        var stripped = AddItemRegex().Replace(text, string.Empty);
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"\n{3,}", "\n\n");
        return stripped.Trim();
    }

    private static AiQueryResult BuildResult(string answer)
    {
        var suggestions = ExtractSuggestions(answer);
        var cleanedText = suggestions.Count > 0 ? StripSuggestions(answer) : answer;
        return new AiQueryResult(cleanedText, suggestions);
    }

    /// <summary>
    /// Extracts all APPLY_READ_SQL(...) templates from a response string.
    /// Uses parenthesis-counting to handle nested parens in SQL subqueries and functions.
    /// </summary>
    internal static IEnumerable<(string fullMatch, string sql)> ExtractTemplates(string response)
    {
        const string marker = "APPLY_READ_SQL(";
        int searchFrom = 0;
        while (true)
        {
            int start = response.IndexOf(marker, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                yield break;
            }

            int contentStart = start + marker.Length;
            int depth = 1;
            int i = contentStart;
            while (i < response.Length && depth > 0)
            {
                if (response[i] == '(')
                {
                    depth++;
                }
                else if (response[i] == ')')
                {
                    depth--;
                }

                i++;
            }

            if (depth != 0)
            {
                // Unmatched parentheses — skip this occurrence
                searchFrom = contentStart;
                continue;
            }

            // i points one past the closing )
            string fullMatch = response.Substring(start, i - start);
            string sql = response.Substring(contentStart, (i - 1) - contentStart).Trim();

            yield return (fullMatch, sql);
            searchFrom = i;
        }
    }

    /// <summary>
    /// Returns true if the text looks like it contains a raw SQL SELECT statement outside any APPLY_READ_SQL wrapper.
    /// </summary>
    internal static bool ContainsSql(string text)
    {
        // Strip APPLY_READ_SQL(...) blocks first so we don't flag legitimate template syntax
        var stripped = System.Text.RegularExpressions.Regex.Replace(
            text, @"APPLY_READ_SQL\s*\(.*?\)", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        return System.Text.RegularExpressions.Regex.IsMatch(
            stripped,
            @"\bSELECT\b.{1,200}\bFROM\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    /// <summary>
    /// Returns true if the SQL contains @groupId or @chatId (case-insensitive scope check).
    /// </summary>
    internal static bool ValidateSqlScope(string sql)
    {
        return sql.Contains("@groupId", StringComparison.OrdinalIgnoreCase)
            || sql.Contains("@chatId", StringComparison.OrdinalIgnoreCase);
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
