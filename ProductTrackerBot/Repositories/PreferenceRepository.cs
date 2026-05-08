// <copyright file="PreferenceRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;

/// <summary>
/// SQLite-backed implementation of <see cref="IPreferenceRepository"/>.
/// </summary>
public class PreferenceRepository : IPreferenceRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferenceRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public PreferenceRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <inheritdoc/>
    public async Task<string?> GetLanguageAsync(long chatId, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT LanguageCode
            FROM UserPreferences
            WHERE ChatId = @chatId";
        cmd.Parameters.AddWithValue("@chatId", chatId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return reader.GetString(0);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task SaveLanguageAsync(long chatId, string languageCode, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO UserPreferences (ChatId, LanguageCode, UpdatedAt)
            VALUES (@chatId, @languageCode, datetime('now'))";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@languageCode", languageCode);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
