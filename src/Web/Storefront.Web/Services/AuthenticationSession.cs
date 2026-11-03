using Storefront.Web.Models;

namespace Storefront.Web.Services;

public static class AuthenticationSession
{
    public static void SaveAuthentication(this ISession session, AuthResponse data)
    {
        session.SetString("jwt", data.Token);
        session.SetString("name", data.FullName);
        session.SetString("userId", data.UserId);
        session.SetString("roles", string.Join(",", data.Roles ?? []));
        SetOptional(session, "refreshToken", data.RefreshToken);
        SetOptional(session, "avatar", data.Avatar);
    }

    private static void SetOptional(ISession session, string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) session.Remove(key);
        else session.SetString(key, value);
    }
}
