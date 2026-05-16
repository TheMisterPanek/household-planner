using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class DayMealsRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DayMealsRepository repository;
    private readonly MealRepository mealRepository;

    public DayMealsRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:DayMealsRepoTests?mode=memory&cache=shared");
        this.connection.Open();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER,
                LanguageCode TEXT NOT NULL DEFAULT 'ru'
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
                UNIQUE(GroupId, DayOfWeek)
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM DayMeals;
            DELETE FROM Meals;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('Groups', 'Meals', 'DayMeals');";
        cleanCmd.ExecuteNonQuery();

        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345);";
        insertCmd.ExecuteNonQuery();

        var cs = "Data Source=file:DayMealsRepoTests?mode=memory&cache=shared";
        this.repository = new DayMealsRepository(cs);
        this.mealRepository = new MealRepository(cs);
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public async Task GetWeekAsync_Returns_Empty_When_No_Plan()
    {
        var result = await this.repository.GetWeekAsync(groupId: 1);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetWeekAsync_Returns_Entries_With_MealNames()
    {
        var pasta = await this.mealRepository.AddAsync(1, "Pasta");
        var pizza = await this.mealRepository.AddAsync(1, "Pizza");
        await this.repository.UpsertAsync(1, 1, pasta.Id);
        await this.repository.UpsertAsync(1, 3, pizza.Id);

        var result = await this.repository.GetWeekAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].DayOfWeek);
        Assert.Equal("Pasta", result[0].MealName);
        Assert.Equal(3, result[1].DayOfWeek);
        Assert.Equal("Pizza", result[1].MealName);
    }

    [Fact]
    public async Task UpsertAsync_Inserts_New_Entry()
    {
        var meal = await this.mealRepository.AddAsync(1, "Soup");
        await this.repository.UpsertAsync(1, 5, meal.Id);

        var result = await this.repository.GetWeekAsync(1);
        Assert.Single(result);
        Assert.Equal(5, result[0].DayOfWeek);
        Assert.Equal(meal.Id, result[0].MealId);
    }

    [Fact]
    public async Task UpsertAsync_Replaces_Existing_Entry()
    {
        var meal1 = await this.mealRepository.AddAsync(1, "Soup");
        var meal2 = await this.mealRepository.AddAsync(1, "Salad");
        await this.repository.UpsertAsync(1, 2, meal1.Id);
        await this.repository.UpsertAsync(1, 2, meal2.Id);

        var result = await this.repository.GetWeekAsync(1);
        Assert.Single(result);
        Assert.Equal("Salad", result[0].MealName);
    }

    [Fact]
    public async Task ClearAsync_Removes_Entry()
    {
        var meal = await this.mealRepository.AddAsync(1, "Steak");
        await this.repository.UpsertAsync(1, 4, meal.Id);
        await this.repository.ClearAsync(1, 4);

        var result = await this.repository.GetWeekAsync(1);
        Assert.Empty(result);
    }
}
