// <copyright file="LoginWebApplicationFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Web.Tests.E2E;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductTrackerBot.Services;
using ProductTrackerBot.Web.Components;

/// <summary>
/// Starts a real Kestrel server on a random port for Playwright E2E tests.
/// Replaces <see cref="ILoginCodeStore"/> with a controllable <see cref="LoginCodeStoreStub"/>
/// and provides test-safe defaults (ephemeral data protection, dummy username).
/// </summary>
/// <remarks>
/// <c>WebApplicationFactory&lt;Program&gt;</c> forces a <c>TestServer</c> cast that
/// prevents Playwright's browser from reaching the app over real TCP. This class starts
/// a minimal <see cref="WebApplication"/> directly so the browser can connect normally.
/// </remarks>
public sealed class LoginWebApplicationFactory : IDisposable
{
    private WebApplication? _app;

    /// <summary>Gets the login code store stub; tests adjust <see cref="LoginCodeStoreStub.ForcedTtlSeconds"/> between assertions.</summary>
    public LoginCodeStoreStub Stub { get; } = new LoginCodeStoreStub();

    /// <summary>Gets the base URL Kestrel is listening on (e.g. "http://127.0.0.1:51234").</summary>
    public string ServerAddress { get; private set; } = string.Empty;

    /// <summary>Builds and starts the web app synchronously; must be called once before tests run.</summary>
    public void Initialize()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // Provide test-only configuration values.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BOT_USERNAME"] = "@TestBot",
        });

        // ── Services needed by Login.razor ────────────────────────────────────
        builder.Services.AddSingleton<ILoginCodeStore>(this.Stub);

        // Use ephemeral data-protection keys (no filesystem dependency).
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o => o.LoginPath = "/login");
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(o => o.DisableImplicitFromServicesParameters = true);

        // ── Build & map middleware ─────────────────────────────────────────────
        _app = builder.Build();
        _app.UseStaticFiles();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseAntiforgery();
        _app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        _app.StartAsync().GetAwaiter().GetResult();
        this.ServerAddress = _app.Urls.First();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_app is null)
            return;
        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
