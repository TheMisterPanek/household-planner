using System.Security.Claims;

namespace ProductTrackerBot.Web.Services;

public sealed class WebSessionStore(IHttpContextAccessor accessor)
{
    public long ChatId =>
        long.Parse(accessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public bool IsAuthenticated =>
        accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
