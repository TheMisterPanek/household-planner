// <copyright file="GroupRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the Groups table.
/// </summary>
public class GroupRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public GroupRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Gets an existing group by chat ID, or creates it if not found.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <returns>The group record.</returns>
    public virtual async Task<Group> GetOrCreateAsync(long chatId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        // Try to find existing group
        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT Id, ChatId, ListMessageId, LanguageCode FROM Groups WHERE ChatId = @chatId";
        selectCmd.Parameters.AddWithValue("@chatId", chatId);

        await using var reader = await selectCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Group
            {
                Id = reader.GetInt32(0),
                ChatId = reader.GetInt64(1),
                ListMessageId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                LanguageCode = reader.IsDBNull(3) ? "ru" : reader.GetString(3),
            };
        }

        await reader.DisposeAsync();

        // Insert new group
        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId, LanguageCode) VALUES (@chatId, 'ru'); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@chatId", chatId);

        var newId = (long)(await insertCmd.ExecuteScalarAsync())!;

        return new Group
        {
            Id = (int)newId,
            ChatId = chatId,
            ListMessageId = null,
            LanguageCode = "ru",
        };
    }

    /// <summary>
    /// Updates the ListMessageId for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="listMessageId">The new list message ID.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task UpdateListMessageIdAsync(int groupId, int listMessageId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Groups SET ListMessageId = @listMessageId WHERE Id = @groupId";
        cmd.Parameters.AddWithValue("@listMessageId", listMessageId);
        cmd.Parameters.AddWithValue("@groupId", groupId);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Sets the language code for a group.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="languageCode">The language code (e.g. 'en', 'ru').</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task SetLanguageAsync(long chatId, string languageCode)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Groups SET LanguageCode = @languageCode WHERE ChatId = @chatId";
        cmd.Parameters.AddWithValue("@languageCode", languageCode);
        cmd.Parameters.AddWithValue("@chatId", chatId);

        await cmd.ExecuteNonQueryAsync();
    }
}
