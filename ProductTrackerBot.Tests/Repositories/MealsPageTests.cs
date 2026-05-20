using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

/// <summary>
/// Integration-style tests for the Meals page data access layer.
/// Tests verify the repository operations that Meals.razor relies on.
/// </summary>
[Collection("DatabaseTests")]
public class MealsPageTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:MealsPageTests?mode=memory&cache=shared";

    private readonly SqliteConnection connection;
    private readonly MealRepository mealRepository;
    private readonly MealIngredientRepository ingredientRepository;
    private readonly MealStepRepository stepRepository;
    private readonly int groupId;

    public MealsPageTests()
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
            CREATE TABLE IF NOT EXISTS MealIngredients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id),
                Name TEXT NOT NULL,
                Quantity TEXT
            );
            CREATE TABLE IF NOT EXISTS MealSteps (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id),
                StepNumber INTEGER NOT NULL,
                Text TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        using var clean = this.connection.CreateCommand();
        clean.CommandText = @"
            DELETE FROM MealSteps;
            DELETE FROM MealIngredients;
            DELETE FROM Meals;
            DELETE FROM Groups;";
        clean.ExecuteNonQuery();

        using var insertGroup = this.connection.CreateCommand();
        insertGroup.CommandText = "INSERT INTO Groups (ChatId) VALUES (88001); SELECT last_insert_rowid();";
        this.groupId = (int)(long)insertGroup.ExecuteScalar()!;

        this.mealRepository = new MealRepository(ConnectionString);
        this.ingredientRepository = new MealIngredientRepository(ConnectionString);
        this.stepRepository = new MealStepRepository(ConnectionString);
    }

    // 3.1 — Clicking a meal in the sidebar loads its ingredients and steps
    [Fact]
    public async Task GetAllAsync_Returns_Ingredients_And_Steps_For_Selected_Meal()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Pasta");
        await this.ingredientRepository.AddAsync(meal.Id, "Flour", "500g");
        await this.stepRepository.AddAsync(meal.Id, "Boil water");

        var ingredients = await this.ingredientRepository.GetAllAsync(meal.Id);
        var steps = await this.stepRepository.GetAllAsync(meal.Id);

        Assert.Single(ingredients);
        Assert.Equal("Flour", ingredients[0].Name);
        Assert.Single(steps);
        Assert.Equal("Boil water", steps[0].Text);
    }

    // 3.2 — [+ New meal] → AddAsync called; new meal appears in list
    [Fact]
    public async Task AddAsync_MealAppearsInGetAllAsync()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Pizza");

        var meals = await this.mealRepository.GetAllAsync(this.groupId);

        Assert.Single(meals);
        Assert.Equal("Pizza", meals[0].Name);
        Assert.Equal(meal.Id, meals[0].Id);
    }

    // 3.3 — ✕ on meal with confirmation → DeleteAsync called; meal removed from list
    [Fact]
    public async Task DeleteAsync_MealRemovedFromList()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Soup");
        await this.mealRepository.AddAsync(this.groupId, "Pizza");

        await this.mealRepository.DeleteAsync(meal.Id);

        var meals = await this.mealRepository.GetAllAsync(this.groupId);
        Assert.Single(meals);
        Assert.DoesNotContain(meals, m => m.Id == meal.Id);
    }

    // 3.4 — [Rename] → UpdateNameAsync called with new name
    [Fact]
    public async Task UpdateNameAsync_UpdatesMealName()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Old Name");

        await this.mealRepository.UpdateNameAsync(meal.Id, "New Name");

        var meals = await this.mealRepository.GetAllAsync(this.groupId);
        Assert.Single(meals);
        Assert.Equal("New Name", meals[0].Name);
    }

    // 3.5 — [+ Add] ingredient → AddAsync called with correct args
    [Fact]
    public async Task AddAsync_IngredientPersistsWithCorrectFields()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Pasta");

        await this.ingredientRepository.AddAsync(meal.Id, "Flour", "500g");

        var ingredients = await this.ingredientRepository.GetAllAsync(meal.Id);
        Assert.Single(ingredients);
        Assert.Equal("Flour", ingredients[0].Name);
        Assert.Equal("500g", ingredients[0].Quantity);
        Assert.Equal(meal.Id, ingredients[0].MealId);
    }

    // 3.6 — ↑ on step 2 → swaps Order with step 1; steps rerender in new order
    [Fact]
    public async Task UpdateStepNumberAsync_SwapsStepOrder()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Pasta");
        var step1 = await this.stepRepository.AddAsync(meal.Id, "Boil water");
        var step2 = await this.stepRepository.AddAsync(meal.Id, "Add pasta");

        // Move step2 up: swap step numbers
        await this.stepRepository.UpdateStepNumberAsync(step1.Id, step2.StepNumber);
        await this.stepRepository.UpdateStepNumberAsync(step2.Id, step1.StepNumber);

        var steps = await this.stepRepository.GetAllAsync(meal.Id);
        Assert.Equal(2, steps.Count);
        Assert.Equal("Add pasta", steps[0].Text);  // formerly step2, now has lower number
        Assert.Equal("Boil water", steps[1].Text);
    }

    // 3.7 — ✎ on step → UpdateAsync called with edited text
    [Fact]
    public async Task UpdateAsync_StepTextIsUpdated()
    {
        var meal = await this.mealRepository.AddAsync(this.groupId, "Pasta");
        var step = await this.stepRepository.AddAsync(meal.Id, "Original text");

        await this.stepRepository.UpdateAsync(step.Id, "Updated text");

        var steps = await this.stepRepository.GetAllAsync(meal.Id);
        Assert.Single(steps);
        Assert.Equal("Updated text", steps[0].Text);
    }

    public void Dispose() => this.connection.Dispose();
}
