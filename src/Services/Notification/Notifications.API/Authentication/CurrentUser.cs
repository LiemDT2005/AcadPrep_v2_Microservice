using System.Security.Claims;
using Notifications.Application.Interfaces;

namespace Notifications.API.Authentication;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    // MapInboundClaims is disabled: Identity's sub is the canonical identifier.
    // Do not fall back to a conflicting NameIdentifier or request-supplied identity.
    public Guid? UserId => IsAuthenticated &&
        Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : null;

    public string? Email => IsAuthenticated ? Principal?.FindFirstValue("email") : null;

    public IReadOnlyCollection<string> Roles => IsAuthenticated
        ? Principal!.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray()
        : [];
}
