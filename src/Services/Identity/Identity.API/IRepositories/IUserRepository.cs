using Identity.API.Models;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.IRepositories;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(string id);
    Task<AppUser?> GetByEmailAsync(string email);
    Task<IdentityResult> CreateAsync(AppUser user, string password);
    Task<IdentityResult> UpdateAsync(AppUser user);
    Task<IList<string>> GetRolesAsync(AppUser user);
    Task<IdentityResult> AddToRoleAsync(AppUser user, string role);
    Task<IdentityResult> AddToRolesAsync(AppUser user, IEnumerable<string> roles);
    Task<IdentityResult> RemoveFromRolesAsync(AppUser user, IEnumerable<string> roles);
    Task<bool> CheckPasswordAsync(AppUser user, string password);
    Task<IdentityResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword);
    Task<string> GeneratePasswordResetTokenAsync(AppUser user);
    Task<IdentityResult> ResetPasswordAsync(AppUser user, string token, string newPassword);
    Task UpdateSecurityStampAsync(AppUser user);
    Task<(List<AppUser> Items, int TotalCount)> GetPagedUsersAsync(string? search, string? role, UserStatus? status, int pageIndex, int pageSize);
    Task<List<AppUser>> GetAllUsersAsync();
    Task<Dictionary<string, List<string>>> GetUserRolesMapAsync(List<string> userIds);
    Task<bool> RoleExistsAsync(string roleName);
    Task<IdentityRole?> FindRoleByNameAsync(string roleName);
}
