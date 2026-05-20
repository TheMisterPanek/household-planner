// <copyright file="LoginCodeStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

/// <summary>
/// SQLite-backed store for login codes and session tokens, shared across all processes
/// that connect to the same database file.
/// </summary>
public sealed class LoginCodeStore : ILoginCodeStore
{
    private readonly string connectionString;
    private readonly TimeProvider time;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCodeStore"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string (shared with the main database).</param>
    /// <param name="time">Time provider (use <see cref="TimeProvider.System"/> in production).</param>
    public LoginCodeStore(string connectionString, TimeProvider time)
    {
        this.connectionString = connectionString;
        this.time = time;
        this.EnsureSchema();
    }

    /// <summary>
    /// Generates a one-time 6-digit login code with a 5-minute TTL and returns a 32-byte hex session token.
    /// </summary>
    /// <param name="token">The session token to poll /api/auth/status with.</param>
    /// <returns>A zero-padded 6-digit code string.</returns>
    public string GenerateCode(out string token)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var expiryTs = ToUnixSeconds(this.time.GetUtcNow().UtcDateTime.AddMinutes(5));

        using var conn = this.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM LoginCodes WHERE ExpiresUtc < @now;
            INSERT OR REPLACE INTO LoginCodes (Code, Token, ChatId, ExpiresUtc, Verified, TokenExpiresUtc)
            VALUES (@code, @token, 0, @expiry, 0, 0);";
        cmd.Parameters.AddWithValue("@now", ToUnixSeconds(this.time.GetUtcNow().UtcDateTime));
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@expiry", expiryTs);
        cmd.ExecuteNonQuery();

        return code;
    }

    /// <inheritdoc/>
    public int GetRemainingSeconds(string code)
    {
        using var conn = this.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ExpiresUtc FROM LoginCodes WHERE Code = @code";
        cmd.Parameters.AddWithValue("@code", code);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return 0;
        var remaining = reader.GetInt64(0) - ToUnixSeconds(this.time.GetUtcNow().UtcDateTime);
        return (int)Math.Max(0, remaining);
    }

    /// <summary>
    /// Verifies a login code submitted via the Telegram bot, marking the associated token as verified.
    /// </summary>
    /// <param name="code">The 6-digit code entered by the user.</param>
    /// <param name="chatId">The Telegram chat ID of the verifying user.</param>
    /// <returns><c>true</c> if the code was valid and not yet expired; otherwise <c>false</c>.</returns>
    public bool TryVerify(string code, long chatId)
    {
        var nowTs = ToUnixSeconds(this.time.GetUtcNow().UtcDateTime);
        var tokenExpiry = ToUnixSeconds(this.time.GetUtcNow().UtcDateTime.AddHours(24));

        using var conn = this.Open();
        using var tx = conn.BeginTransaction();

        using var select = conn.CreateCommand();
        select.Transaction = tx;
        select.CommandText = "SELECT ExpiresUtc FROM LoginCodes WHERE Code = @code AND Verified = 0";
        select.Parameters.AddWithValue("@code", code);
        using var reader = select.ExecuteReader();

        if (!reader.Read() || reader.GetInt64(0) < nowTs)
        {
            tx.Rollback();
            return false;
        }

        reader.Close();

        using var update = conn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "UPDATE LoginCodes SET Verified = 1, ChatId = @chatId, TokenExpiresUtc = @tokenExpiry WHERE Code = @code";
        update.Parameters.AddWithValue("@chatId", chatId);
        update.Parameters.AddWithValue("@tokenExpiry", tokenExpiry);
        update.Parameters.AddWithValue("@code", code);
        update.ExecuteNonQuery();

        tx.Commit();
        return true;
    }

    /// <summary>
    /// Retrieves the chat ID associated with a verified session token.
    /// </summary>
    /// <param name="token">The hex session token.</param>
    /// <param name="chatId">The resolved chat ID, or 0 if not found.</param>
    /// <returns><c>true</c> if the token is valid and not expired; otherwise <c>false</c>.</returns>
    public bool TryGetSession(string token, out long chatId)
    {
        chatId = 0;
        var nowTs = ToUnixSeconds(this.time.GetUtcNow().UtcDateTime);

        using var conn = this.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ChatId, TokenExpiresUtc FROM LoginCodes WHERE Token = @token AND Verified = 1";
        cmd.Parameters.AddWithValue("@token", token);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return false;

        var id = reader.GetInt64(0);
        var expiry = reader.GetInt64(1);

        if (nowTs > expiry)
            return false;

        chatId = id;
        return true;
    }

    /// <summary>
    /// Removes a pending code from the store.
    /// </summary>
    /// <param name="code">The code to remove.</param>
    public void Purge(string code)
    {
        using var conn = this.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM LoginCodes WHERE Code = @code";
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(this.connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        using var conn = this.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS LoginCodes (
                Code TEXT NOT NULL PRIMARY KEY,
                Token TEXT NOT NULL,
                ChatId INTEGER NOT NULL DEFAULT 0,
                ExpiresUtc INTEGER NOT NULL,
                Verified INTEGER NOT NULL DEFAULT 0,
                TokenExpiresUtc INTEGER NOT NULL DEFAULT 0
            );";
        cmd.ExecuteNonQuery();
    }

    private static long ToUnixSeconds(DateTime utc) =>
        new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}
