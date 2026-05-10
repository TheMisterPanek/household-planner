using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class MealIngredientRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly MealIngredientRepository repository;

    public MealIngredientRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:MealIngredientRepoTests?mode=memory&cache=shared");
        this.connection.Open();

        // Initialize schema
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
            CREATE TABLE IF NOT EXISTS MealIngredients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id) ON DELETE CASCADE,
                Name TEXT NOT NULL,
                Quantity TEXT
            );";
        cmd.ExecuteNonQuery();

        // Clean any leftover data
        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM MealIngredients;
            DELETE FROM Meals;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('Groups', 'Meals', 'MealIngredients');"
;
        cleanCmd.ExecuteNonQuery();

        // Insert test group and meal
        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345); INSERT INTO Meals (GroupId, Name) VALUES (1, 'Pasta');";

        insertCmd.ExecuteNonQuery();

        this.repository = new MealIngredientRepository("Data Source=file:MealIngredientRepoTests?mode=memory&cache=shared");
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_With_Quantity()
    {
        var ingredient = await this.repository.AddAsync(mealId: 1, name: "Flour", quantity: "200g");

        Assert.True(ingredient.Id > 0);
        Assert.Equal(1, ingredient.MealId);
        Assert.Equal("Flour", ingredient.Name);
        Assert.Equal("200g", ingredient.Quantity);
    }

    [Fact]
    public async Task AddAsync_Without_Quantity()
    {
        var ingredient = await this.repository.AddAsync(mealId: 1, name: "Salt");

        Assert.True(ingredient.Id > 0);
        Assert.Equal(1, ingredient.MealId);
        Assert.Equal("Salt", ingredient.Name);
        Assert.Null(ingredient.Quantity);
    }

    [Fact]
    public async Task GetAllAsync_Returns_All_Ingredients_For_Meal()
    {
        await this.repository.AddAsync(1, "Flour", "200g");
        await this.repository.AddAsync(1, "Water", null);
        await this.repository.AddAsync(1, "Salt", "1 tsp");

        var ingredients = await this.repository.GetAllAsync(1);

        Assert.Equal(3, ingredients.Count);
        Assert.Equal("Flour", ingredients[0].Name);
        Assert.Equal("Water", ingredients[1].Name);
        Assert.Equal("Salt", ingredients[2].Name);
    }
}
