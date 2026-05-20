using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

/// <summary>
/// Integration-style tests for the WeekPlan page data access layer.
/// Tests verify the repository operations that WeekPlan.razor relies on.
/// </summary>
[Collection("DatabaseTests")]
public class WeekPlanPageTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:WeekPlanPageTests?mode=memory&cache=shared";
    private const string WeekStart = "2026-05-18"; // a fixed Monday for tests

    private readonly SqliteConnection connection;
    private readonly DayMealsRepository dayMealsRepo;
    private readonly MealRepository mealRepo;
    private readonly int groupId;

    public WeekPlanPageTests()
    {
        this.connection = new SqliteConnection(ConnectionString);
        this.connection.Open();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Meals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                Name TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS DayMeals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                DayOfWeek INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
                MealId INTEGER NOT NULL REFERENCES Meals(Id),
                WeekStartDate TEXT NOT NULL DEFAULT ''
            );";
        cmd.ExecuteNonQuery();

        using var clean = this.connection.CreateCommand();
        clean.CommandText = "DELETE FROM DayMeals; DELETE FROM Meals; DELETE FROM Groups;";
        clean.ExecuteNonQuery();

        using var insertGroup = this.connection.CreateCommand();
        insertGroup.CommandText = "INSERT INTO Groups (ChatId) VALUES (88002); SELECT last_insert_rowid();";
        this.groupId = (int)(long)insertGroup.ExecuteScalar()!;

        this.dayMealsRepo = new DayMealsRepository(ConnectionString);
        this.mealRepo = new MealRepository(ConnectionString);
    }

    private async Task<int> SeedMealAsync(string name = "Pasta")
    {
        var meal = await this.mealRepo.AddAsync(this.groupId, name);
        return meal.Id;
    }

    // 5.1 — GetWeekAsync returns entries with DayOfWeek 1–7
    [Fact]
    public async Task GetWeekAsync_Returns_Entries_With_Correct_DayOfWeek()
    {
        var mId = await SeedMealAsync();
        await this.dayMealsRepo.InsertAsync(this.groupId, 1, mId, WeekStart); // Monday
        await this.dayMealsRepo.InsertAsync(this.groupId, 5, mId, WeekStart); // Friday

        var entries = await this.dayMealsRepo.GetWeekAsync(this.groupId, WeekStart);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.DayOfWeek == 1);
        Assert.Contains(entries, e => e.DayOfWeek == 5);
    }

    // 5.2 — Existing plan assignments pre-populate correctly
    [Fact]
    public async Task InsertAsync_ThenGetWeekAsync_Returns_Correct_MealId_For_Day()
    {
        var mId = await SeedMealAsync("Pizza");
        await this.dayMealsRepo.InsertAsync(this.groupId, 3, mId, WeekStart); // Wednesday

        var entries = await this.dayMealsRepo.GetWeekAsync(this.groupId, WeekStart);

        Assert.Single(entries);
        Assert.Equal(3, entries[0].DayOfWeek);
        Assert.Equal(mId, entries[0].MealId);
    }

    // 5.3 — Selecting a meal (upsert = clear + insert) stores correct dayOfWeek and mealId
    [Fact]
    public async Task ClearThenInsert_UpsertsBehavior_CorrectDayAndMeal()
    {
        var mId1 = await SeedMealAsync("Pasta");
        var mId2 = await SeedMealAsync("Soup");

        // Assign mId1 to Wednesday
        await this.dayMealsRepo.InsertAsync(this.groupId, 3, mId1, WeekStart);

        // Replace with mId2 (upsert = clear + insert)
        await this.dayMealsRepo.ClearAsync(this.groupId, 3, WeekStart);
        await this.dayMealsRepo.InsertAsync(this.groupId, 3, mId2, WeekStart);

        var entries = await this.dayMealsRepo.GetWeekAsync(this.groupId, WeekStart);
        Assert.Single(entries);
        Assert.Equal(3, entries[0].DayOfWeek);
        Assert.Equal(mId2, entries[0].MealId);
    }

    // 5.4 — Selecting "— Not set —" calls ClearAsync; no entry returned for that day
    [Fact]
    public async Task ClearAsync_RemovesEntryForDay()
    {
        var mId = await SeedMealAsync();
        await this.dayMealsRepo.InsertAsync(this.groupId, 4, mId, WeekStart); // Thursday

        await this.dayMealsRepo.ClearAsync(this.groupId, 4, WeekStart);

        var entries = await this.dayMealsRepo.GetWeekAsync(this.groupId, WeekStart);
        Assert.Empty(entries);
    }

    public void Dispose() => this.connection.Dispose();
}
