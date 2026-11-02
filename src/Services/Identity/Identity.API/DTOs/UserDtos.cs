using System.ComponentModel.DataAnnotations;
using Identity.API.Models;

namespace Identity.API.DTOs;

public record UserListItemDto(
    string Id,
    string Email,
    string FullName,
    string? Avatar,
    string Status,
    string AuthProvider,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<string> Roles);

public record UserListResponse(
    List<UserListItemDto> Items,
    int TotalCount,
    int PageIndex,
    int PageSize,
    int TotalPages);

public record UserSessionDto(
    int Id,
    string TokenPreview,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string? CreatedByIp,
    bool IsActive,
    string? ReasonRevoked);

public record UserDetailAdminDto(
    string Id,
    string Email,
    string FullName,
    string? Avatar,
    string? PhoneNumber,
    string Status,
    string AuthProvider,
    bool EmailConfirmed,
    int AccessFailedCount,
    DateTimeOffset? LockoutEnd,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt,
    List<string> Roles,
    List<UserSessionDto> RecentSessions);

public record AssignRolesRequest(
    [Required(ErrorMessage = "Roles list is required.")]
    List<string> Roles);

public record UpdateUserStatusRequest(
    [Required] UserStatus Status,
    string? Reason = null);
