using Identity.API.Data;
using Identity.API.IRepositories;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserRepository(
        IdentityDbContext context,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<AppUser?> GetByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<AppUser?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _userManager.FindByEmailAsync(normalizedEmail);
    }

    public async Task<IdentityResult> CreateAsync(AppUser user, string password)
    {
        return await _userManager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> UpdateAsync(AppUser user)
    {
        return await _userManager.UpdateAsync(user);
    }

    public async Task<IList<string>> GetRolesAsync(AppUser user)
    {
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IdentityResult> AddToRoleAsync(AppUser user, string role)
    {
        return await _userManager.AddToRoleAsync(user, role);
    }

    public async Task<IdentityResult> AddToRolesAsync(AppUser user, IEnumerable<string> roles)
    {
        return await _userManager.AddToRolesAsync(user, roles);
    }

    public async Task<IdentityResult> RemoveFromRolesAsync(AppUser user, IEnumerable<string> roles)
    {
        return await _userManager.RemoveFromRolesAsync(user, roles);
    }

    public async Task<bool> CheckPasswordAsync(AppUser user, string password)
    {
        return await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<IdentityResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword)
    {
        return await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(AppUser user)
    {
        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<IdentityResult> ResetPasswordAsync(AppUser user, string token, string newPassword)
    {
        return await _userManager.ResetPasswordAsync(user, token, newPassword);
    }

    public async Task UpdateSecurityStampAsync(AppUser user)
    {
        await _userManager.UpdateSecurityStampAsync(user);
    }

    public async Task<(List<AppUser> Items, int TotalCount)> GetPagedUsersAsync(
        string? search,
        string? role,
        UserStatus? status,
        int pageIndex,
        int pageSize)
    {
        var query = _context.Users.AsNoTracking();

        // 1. Tìm kiếm theo Tên hoặc Email
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u =>
                (u.FullName != null && u.FullName.Contains(s)) ||
                (u.Email != null && u.Email.Contains(s)));
        }

        // 2. Lọc theo trạng thái
        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        // 3. Lọc theo vai trò nếu có
        if (!string.IsNullOrWhiteSpace(role))
        {
            var targetRole = role.Trim();
            var roleEntity = await _roleManager.FindByNameAsync(targetRole);
            if (roleEntity is not null)
            {
                var userIdsWithRole = _context.UserRoles
                    .Where(ur => ur.RoleId == roleEntity.Id)
                    .Select(ur => ur.UserId);

                query = query.Where(u => userIdsWithRole.Contains(u.Id));
            }
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetUserRolesMapAsync(List<string> userIds)
    {
        var userRoles = await (from ur in _context.UserRoles
                               join r in _context.Roles on ur.RoleId equals r.Id
                               where userIds.Contains(ur.UserId) && r.Name != null
                               select new { ur.UserId, RoleName = r.Name })
                              .ToListAsync();

        return userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());
    }

    public async Task<bool> RoleExistsAsync(string roleName)
    {
        return await _roleManager.RoleExistsAsync(roleName);
    }

    public async Task<IdentityRole?> FindRoleByNameAsync(string roleName)
    {
        return await _roleManager.FindByNameAsync(roleName);
    }
}
