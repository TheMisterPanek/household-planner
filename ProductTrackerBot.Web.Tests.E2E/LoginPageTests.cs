// <copyright file="LoginPageTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Web.Tests.E2E;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
/// Playwright E2E tests for the /login page.
/// The factory starts the web app on a random Kestrel port; Playwright's Chromium
/// browser navigates to that address directly.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class LoginPageTests : PageTest
{
    private static LoginWebApplicationFactory _factory = null!;
    private static string _baseUrl = string.Empty;

    [OneTimeSetUp]
    public static void StartServer()
    {
        _factory = new LoginWebApplicationFactory();
        _factory.Initialize();
        _baseUrl = _factory.ServerAddress;
    }

    [OneTimeTearDown]
    public static void StopServer() => _factory.Dispose();

    /// <summary>Reset stub TTL before each test so tests are isolated.</summary>
    [SetUp]
    public void ResetStub() => _factory.Stub.ForcedTtlSeconds = 300;

    // ── 3.1 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task PageTitle_IsSignIn()
    {
        await Page.GotoAsync(_baseUrl + "/login");
        // Wait for the Blazor circuit (title is updated by <PageTitle> once interactive).
        await Expect(Page.Locator("span.font-mono")).ToBeVisibleAsync();
        await Expect(Page).ToHaveTitleAsync("Sign in — Household Planner");
    }

    // ── 3.2 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task LoginCode_MatchesPattern()
    {
        await Page.GotoAsync(_baseUrl + "/login");
        var codeSpan = Page.Locator("span.font-mono");
        await Expect(codeSpan).ToBeVisibleAsync();
        var code = await codeSpan.TextContentAsync() ?? string.Empty;
        Assert.That(code.Trim(), Does.Match("^[A-Z0-9]{6}$"));
    }

    // ── 3.3 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task FreshLoad_ShowsCountdown_AndHidesRefreshButton()
    {
        await Page.GotoAsync(_baseUrl + "/login");
        await Expect(Page.GetByText("Code expires in", new PageGetByTextOptions { Exact = false }))
            .ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Get new code" }))
            .Not.ToBeVisibleAsync();
    }

    // ── 3.4 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Countdown_DecrementsAfterTwoSeconds()
    {
        await Page.GotoAsync(_baseUrl + "/login");
        await Expect(Page.GetByText("Code expires in", new PageGetByTextOptions { Exact = false }))
            .ToBeVisibleAsync();

        var initial = await ReadCountdownAsync();
        await Task.Delay(2000);
        var later = await ReadCountdownAsync();

        Assert.That(later, Is.LessThan(initial));
    }

    // ── 3.5 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ExpiredCode_ShowsRefreshButton_AndHidesCountdown()
    {
        _factory.Stub.ForcedTtlSeconds = 1;
        await Page.GotoAsync(_baseUrl + "/login");

        var refreshBtn = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Get new code" });
        await Expect(refreshBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await Expect(Page.GetByText("Code expires in", new PageGetByTextOptions { Exact = false }))
            .Not.ToBeVisibleAsync();
    }

    // ── 3.6 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ClickingRefresh_RestoresCountdownAndHidesButton()
    {
        _factory.Stub.ForcedTtlSeconds = 1;
        await Page.GotoAsync(_baseUrl + "/login");

        var refreshBtn = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Get new code" });
        await Expect(refreshBtn).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        // Restore TTL before clicking so the new code gets a full countdown.
        _factory.Stub.ForcedTtlSeconds = 300;
        await refreshBtn.ClickAsync();

        await Expect(Page.GetByText("Code expires in", new PageGetByTextOptions { Exact = false }))
            .ToBeVisibleAsync();
        await Expect(Page.Locator("span.font-mono")).Not.ToBeEmptyAsync();
        await Expect(refreshBtn).Not.ToBeVisibleAsync();
    }

    // ── 3.7 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task BotUsername_AppearsInInstructions()
    {
        await Page.GotoAsync(_baseUrl + "/login");
        await Expect(Page.GetByText("@TestBot", new PageGetByTextOptions { Exact = false }))
            .ToBeVisibleAsync();
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<int> ReadCountdownAsync()
    {
        // Matches "Code expires in 299 s" → returns 299
        var text = await Page
            .GetByText("Code expires in", new PageGetByTextOptions { Exact = false })
            .TextContentAsync() ?? string.Empty;
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return int.Parse(parts[3]); // "Code expires in <N> s"
    }
}
