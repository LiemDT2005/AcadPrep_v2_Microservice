using System.Text;
using Identity.API.DTOs;
using Identity.API.Hubs;
using Identity.API.IRepositories;
using Identity.API.IServices;
using Identity.API.Mappers;
using Identity.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace Identity.API.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IHubContext<UserHub> _hubContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IHubContext<UserHub> hubContext,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<UserListResponse> GetUsersAsync(
        string? search,
        string? role,
        UserStatus? status,
        int pageIndex,
        int pageSize)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var (users, totalCount) = await _userRepository.GetPagedUsersAsync(search, role, status, pageIndex, pageSize);

        var userIds = users.Select(u => u.Id).ToList();
        var rolesMap = await _userRepository.GetUserRolesMapAsync(userIds);

        var items = users.Select(u =>
        {
            var userRoles = rolesMap.TryGetValue(u.Id, out var rList) ? rList : [];
            return UserMapper.ToUserListItemDto(u, userRoles);
        }).ToList();

        return UserMapper.ToUserListResponse(items, totalCount, pageIndex, pageSize);
    }

    public async Task<(byte[] CsvBytes, string FileName)> ExportUsersCsvAsync()
    {
        var users = await _userRepository.GetAllUsersAsync();
        var userIds = users.Select(u => u.Id).ToList();
        var rolesMap = await _userRepository.GetUserRolesMapAsync(userIds);

        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine("ID,Email,Full Name,Status,Auth Provider,Roles,Created At,Last Login");

        foreach (var u in users)
        {
            var roles = rolesMap.TryGetValue(u.Id, out var rList) ? string.Join(" | ", rList) : string.Empty;
            var created = u.CreatedAt.ToString("MM/dd/yyyy HH:mm");
            var lastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToString("MM/dd/yyyy HH:mm") : "Never logged in";

            sb.AppendLine($"\"{u.Id}\",\"{u.Email}\",\"{u.FullName}\",\"{u.Status}\",\"{u.AuthProvider}\",\"{roles}\",\"{created}\",\"{lastLogin}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"acadprep_users_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return (bytes, fileName);
    }

    public async Task<(bool Succeeded, string? Error, UserDetailAdminDto? Detail)> GetUserDetailAsync(string id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
            return (false, "User account not found.", null);

        var roles = await _userRepository.GetRolesAsync(user);

        var now = Common.TimeHelper.Now;
        var recentTokens = await _refreshTokenRepository.GetRecentTokensByUserIdAsync(id, 5);
        var sessionDtos = recentTokens.Select(r => UserMapper.ToUserSessionDto(r, now)).ToList();

        return (true, null, UserMapper.ToUserDetailAdminDto(user, roles, sessionDtos));
    }

    public async Task<(bool Succeeded, string? Error, string? Message)> AssignRolesAsync(
        string id,
        AssignRolesRequest request,
        string? currentAdminId)
    {
        if (request.Roles is null || request.Roles.Count == 0)
            return (false, "User must have at least one assigned role.", null);

        var validRoles = new[] { "Admin", "Moderator", "Learner" };
        foreach (var r in request.Roles)
        {
            if (!validRoles.Contains(r))
            {
                return (false, $"Role '{r}' is invalid. Allowed roles: Admin, Moderator, Learner.", null);
            }
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
            return (false, "User account not found.", null);

        if (string.Equals(user.Email, "admin@acadprep.com", StringComparison.OrdinalIgnoreCase) && !request.Roles.Contains("Admin"))
        {
            return (false, "Cannot revoke Admin role from the Master Admin account (admin@acadprep.com).", null);
        }

        if (user.Id == currentAdminId && !request.Roles.Contains("Admin"))
        {
            return (false, "You cannot revoke your own Admin role.", null);
        }

        var currentRoles = await _userRepository.GetRolesAsync(user);

        var removeResult = await _userRepository.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            return (false, string.Join("; ", removeResult.Errors.Select(e => e.Description)), null);

        var addResult = await _userRepository.AddToRolesAsync(user, request.Roles);
        if (!addResult.Succeeded)
            return (false, string.Join("; ", addResult.Errors.Select(e => e.Description)), null);

        user.UpdatedAt = Common.TimeHelper.Now;
        await _userRepository.UpdateAsync(user);
        await _userRepository.UpdateSecurityStampAsync(user);

        _logger.LogInformation("Roles updated for User {UserId} by Admin {AdminId}: {Roles}", user.Id, currentAdminId, string.Join(", ", request.Roles));

        return (true, null, "Roles updated successfully.");
    }

    public async Task<(bool Succeeded, string? Error, string? Message, UserStatus? Status)> UpdateStatusAsync(
        string id,
        UpdateUserStatusRequest request,
        string? currentAdminId,
        string? clientIp)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user is null)
            return (false, "User account not found.", null, null);

        if (string.Equals(user.Email, "admin@acadprep.com", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Cannot change status of Master Admin account (admin@acadprep.com).", null, null);
        }

        if (user.Id == currentAdminId)
        {
            return (false, "You cannot ban your own account.", null, null);
        }

        user.Status = request.Status;
        user.UpdatedAt = Common.TimeHelper.Now;
        var result = await _userRepository.UpdateAsync(user);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)), null, null);

        if (request.Status == UserStatus.Banned)
        {
            var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(id);
            foreach (var t in activeTokens)
            {
                t.RevokedAt = Common.TimeHelper.Now;
                t.RevokedByIp = clientIp;
                t.ReasonRevoked = $"Banned by Admin: {request.Reason ?? "Violation of platform terms"}";
                await _refreshTokenRepository.UpdateAsync(t);
            }
            await _refreshTokenRepository.SaveChangesAsync();

            try
            {
                await _hubContext.Clients.User(id).SendAsync("ForceLogout", request.Reason ?? "Your account has been suspended by an administrator.");
                _logger.LogInformation("SignalR ForceLogout event dispatched to User {UserId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR ForceLogout to User {UserId}", id);
            }
        }

        _logger.LogInformation("Status updated to {Status} for User {UserId} by Admin {AdminId}", request.Status, user.Id, currentAdminId);

        var message = request.Status == UserStatus.Banned ? "Account has been banned successfully." : "Account has been unbanned successfully.";
        return (true, null, message, user.Status);
    }
}
