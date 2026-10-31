using Identity.API.DTOs;
using Identity.API.Models;

namespace Identity.API.Mappers;

public static class UserMapper
{
    public static UserProfileDto ToUserProfileDto(AppUser user, IEnumerable<string> roles)
    {
        return new UserProfileDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            Roles: [.. roles],
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt
        );
    }

    public static UserListItemDto ToUserListItemDto(AppUser user, List<string> roles)
    {
        return new UserListItemDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            AuthProvider: user.AuthProvider,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            Roles: roles
        );
    }

    public static UserSessionDto ToUserSessionDto(RefreshToken r, DateTime now)
    {
        return new UserSessionDto(
            Id: r.Id,
            TokenPreview: r.Token.Length > 12 ? r.Token.Substring(0, 12) + "..." : r.Token,
            CreatedAt: r.CreatedAt,
            ExpiresAt: r.ExpiresAt,
            CreatedByIp: r.CreatedByIp,
            IsActive: r.RevokedAt == null && r.ExpiresAt > now,
            ReasonRevoked: r.ReasonRevoked
        );
    }

    public static UserDetailAdminDto ToUserDetailAdminDto(
        AppUser user,
        IEnumerable<string> roles,
        List<UserSessionDto> recentSessions)
    {
        return new UserDetailAdminDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            Avatar: user.Avatar,
            PhoneNumber: user.PhoneNumber,
            Status: user.Status.ToString(),
            AuthProvider: user.AuthProvider,
            EmailConfirmed: user.EmailConfirmed,
            AccessFailedCount: user.AccessFailedCount,
            LockoutEnd: user.LockoutEnd,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt,
            LastLoginAt: user.LastLoginAt,
            Roles: [.. roles],
            RecentSessions: recentSessions
        );
    }

    public static UserListResponse ToUserListResponse(
        List<UserListItemDto> items,
        int totalCount,
        int pageIndex,
        int pageSize)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new UserListResponse(
            Items: items,
            TotalCount: totalCount,
            PageIndex: pageIndex,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }

    public static AuthResponse ToAuthResponse(
        AppUser user,
        string token,
        DateTime expiresAt,
        IEnumerable<string> roles,
        string? refreshToken = null,
        DateTime? refreshTokenExpiresAt = null)
    {
        return new AuthResponse(
            Token: token,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            UserId: user.Id,
            ExpiresAt: expiresAt,
            Roles: [.. roles],
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            RefreshToken: refreshToken,
            RefreshTokenExpiresAt: refreshTokenExpiresAt
        );
    }
}
