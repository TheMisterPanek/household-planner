using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class MealStepRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly MealStepRepository repository;

    public MealStepRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:MealStepRepoTests?mode=memory&cache=shared");
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
            CREATE TABLE IF NOT EXISTS MealSteps (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id) ON DELETE CASCADE,
                StepNumber INTEGER NOT NULL,
                Text TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        // Clean any leftover data
        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM MealSteps;
            DELETE FROM Meals;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('Groups', 'Meals', 'MealSteps');";
        cleanCmd.ExecuteNonQuery();

        // Insert test group and meal
        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345); INSERT INTO Meals (GroupId, Name) VALUES (1, 'Pasta');";
        insertCmd.ExecuteNonQuery();

        this.repository = new MealStepRepository("Data Source=file:MealStepRepoTests?mode=memory&cache=shared");
    }

    public void Dispose()
    {
        this.connection.Close();
        this.connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_Assigns_Incrementing_StepNumber()
    {
        var step1 = await this.repository.AddAsync(mealId: 1, text: "Mix ingredients");
        var step2 = await this.repository.AddAsync(mealId: 1, text: "Heat oven");
        var step3 = await this.repository.AddAsync(mealId: 1, text: "Bake");

        Assert.Equal(1, step1.StepNumber);
        Assert.Equal(2, step2.StepNumber);
        Assert.Equal(3, step3.StepNumber);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Steps_Ordered_By_StepNumber()
    {
        await this.repository.AddAsync(1, "Mix ingredients");
        await this.repository.AddAsync(1, "Heat oven");
        await this.repository.AddAsync(1, "Bake");

        var steps = await this.repository.GetAllAsync(1);

        Assert.Equal(3, steps.Count);
        Assert.Equal(1, steps[0].StepNumber);
        Assert.Equal(2, steps[1].StepNumber);
        Assert.Equal(3, steps[2].StepNumber);
        Assert.Equal("Mix ingredients", steps[0].Text);
        Assert.Equal("Heat oven", steps[1].Text);
        Assert.Equal("Bake", steps[2].Text);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Correct_Step_Without_Affecting_Others()
    {
        var step1 = await this.repository.AddAsync(1, "Mix ingredients");
        var step2 = await this.repository.AddAsync(1, "Heat oven");
        var step3 = await this.repository.AddAsync(1, "Bake");

        await this.repository.DeleteAsync(step2.Id);

        var stepsAfter = await this.repository.GetAllAsync(1);

        Assert.Equal(2, stepsAfter.Count);
        Assert.Equal(1, stepsAfter[0].StepNumber);
        Assert.Equal(3, stepsAfter[1].StepNumber);
        Assert.Equal("Mix ingredients", stepsAfter[0].Text);
        Assert.Equal("Bake", stepsAfter[1].Text);
    }
}
