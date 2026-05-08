using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class MealRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly MealRepository repository;

    public MealRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file::memory:?cache=shared");
        this.connection.Open();

        // Initialize schema
        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER
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
            );
            CREATE TABLE IF NOT EXISTS MealSteps (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id) ON DELETE CASCADE,
                StepNumber INTEGER NOT NULL,
                Text TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        // Insert test group
        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345);";
        insertCmd.ExecuteNonQuery();

        this.repository = new MealRepository("Data Source=file::memory:?cache=shared");
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_Inserts_Correctly()
    {
        var meal = await this.repository.AddAsync(groupId: 1, name: "Pasta");

        Assert.True(meal.Id > 0);
        Assert.Equal(1, meal.GroupId);
        Assert.Equal("Pasta", meal.Name);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Only_Meals_For_Given_Group()
    {
        await this.repository.AddAsync(groupId: 1, name: "Pasta");
        await this.repository.AddAsync(groupId: 1, name: "Pizza");

        // Insert another group and meal
        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (54321);";
        insertCmd.ExecuteNonQuery();

        await this.repository.AddAsync(groupId: 2, name: "Burger");

        var group1Meals = await this.repository.GetAllAsync(groupId: 1);
        var group2Meals = await this.repository.GetAllAsync(groupId: 2);

        Assert.Equal(2, group1Meals.Count);
        Assert.Single(group2Meals);
        Assert.All(group1Meals, m => Assert.Equal(1, m.GroupId));
    }

    [Fact]
    public async Task DeleteAsync_Cascades_To_MealIngredients_And_MealSteps()
    {
        var meal = await this.repository.AddAsync(groupId: 1, name: "Pasta");
        
        var ingredientRepo = new MealIngredientRepository("Data Source=file::memory:?cache=shared");
        var stepRepo = new MealStepRepository("Data Source=file::memory:?cache=shared");

        await ingredientRepo.AddAsync(meal.Id, "Flour", "200g");
        await ingredientRepo.AddAsync(meal.Id, "Water", null);
        await stepRepo.AddAsync(meal.Id, "Mix ingredients");

        // Verify data was added
        var ingredientsBefore = await ingredientRepo.GetAllAsync(meal.Id);
        var stepsBefore = await stepRepo.GetAllAsync(meal.Id);
        Assert.Equal(2, ingredientsBefore.Count);
        Assert.Single(stepsBefore);

        // Delete meal
        await this.repository.DeleteAsync(meal.Id);

        // Verify ingredients and steps were cascaded
        var ingredientsAfter = await ingredientRepo.GetAllAsync(meal.Id);
        var stepsAfter = await stepRepo.GetAllAsync(meal.Id);
        Assert.Empty(ingredientsAfter);
        Assert.Empty(stepsAfter);
    }
}
