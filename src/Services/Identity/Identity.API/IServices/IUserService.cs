using Identity.API.DTOs;
using Identity.API.Models;

namespace Identity.API.IServices;

public interface IUserService
{
    Task<UserListResponse> GetUsersAsync(string? search, string? role, UserStatus? status, int pageIndex, int pageSize);
    Task<(byte[] CsvBytes, string FileName)> ExportUsersCsvAsync();
    Task<(bool Succeeded, string? Error, UserDetailAdminDto? Detail)> GetUserDetailAsync(string id);
    Task<(bool Succeeded, string? Error, string? Message)> AssignRolesAsync(string id, AssignRolesRequest request, string? currentAdminId);
    Task<(bool Succeeded, string? Error, string? Message, UserStatus? Status)> UpdateStatusAsync(string id, UpdateUserStatusRequest request, string? currentAdminId, string? clientIp);
}
