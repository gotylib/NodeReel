using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NodeReel.Api;

public static class UserClaims
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        if (raw is null || !Guid.TryParse(raw, out var id))
            throw new UnauthorizedAccessException("User id claim is missing.");
        return id;
    }

    public static Guid GetUserId(this ControllerBase controller) => controller.User.GetUserId();
}
