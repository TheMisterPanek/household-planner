// <copyright file="HistoryRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// SQLite-backed implementation of <see cref="IHistoryRepository"/>.
/// </summary>
public class HistoryRepository : IHistoryRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public HistoryRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(
        long chatId,
        long userId,
        string userName,
        BotActionType actionType,
        string payloadJson,
        string? revertPayloadJson,
        CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO BotActionHistory (ChatId, UserId, UserName, ActionType, Payload, revert_payload)
            VALUES (@chatId, @userId, @userName, @actionType, @payload, @revertPayload)";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@userName", userName);
        cmd.Parameters.AddWithValue("@actionType", actionType.ToString());
        cmd.Parameters.AddWithValue("@payload", payloadJson);
        cmd.Parameters.AddWithValue("@revertPayload", (object?)revertPayloadJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BotActionEntry>> GetRecentAsync(long chatId, int limit, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ChatId, UserId, UserName, ActionType, Payload, RecordedAt, revert_payload, reverted_at
            FROM BotActionHistory
            WHERE ChatId = @chatId
            ORDER BY Id DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@limit", limit);

        var entries = new List<BotActionEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<BotActionEntry?> GetLatestReversibleAsync(long chatId, long userId, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ChatId, UserId, UserName, ActionType, Payload, RecordedAt, revert_payload, reverted_at
            FROM BotActionHistory
            WHERE ChatId = @chatId AND UserId = @userId
              AND revert_payload IS NOT NULL
              AND reverted_at IS NULL
            ORDER BY Id DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadEntry(reader);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task MarkRevertedAsync(long entryId, string revertedAt, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE BotActionHistory SET reverted_at = @revertedAt WHERE Id = @id";
        cmd.Parameters.AddWithValue("@revertedAt", revertedAt);
        cmd.Parameters.AddWithValue("@id", entryId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static BotActionEntry ReadEntry(SqliteDataReader reader)
    {
        return new BotActionEntry
        {
            Id = reader.GetInt64(0),
            ChatId = reader.GetInt64(1),
            UserId = reader.GetInt64(2),
            UserName = reader.GetString(3),
            ActionType = Enum.Parse<BotActionType>(reader.GetString(4)),
            Payload = reader.GetString(5),
            RecordedAt = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
            RevertPayload = reader.IsDBNull(7) ? null : reader.GetString(7),
            RevertedAt = reader.IsDBNull(8) ? null : reader.GetString(8),
        };
    }
}
