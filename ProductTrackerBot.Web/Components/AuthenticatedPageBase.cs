using Microsoft.AspNetCore.Components;
using ProductTrackerBot.Web.Services;

namespace ProductTrackerBot.Web.Components;

public abstract class AuthenticatedPageBase : ComponentBase
{
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected WebSessionStore Session { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (!Session.IsAuthenticated)
            Nav.NavigateTo("/login", forceLoad: true);
    }
}
