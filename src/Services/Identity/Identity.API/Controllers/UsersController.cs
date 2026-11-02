using System.Security.Claims;
using Identity.API.DTOs;
using Identity.API.IServices;
using Identity.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    private string? GetClientIp() =>
        HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var ip)
            ? ip.FirstOrDefault()
            : HttpContext.Connection.RemoteIpAddress?.ToString();

    // ==========================================
    // UC-19.1: Get Users List (Pagination, Filter, Search)
    // ==========================================
    [HttpGet]
    public async Task<ActionResult<UserListResponse>> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] UserStatus? status = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await _userService.GetUsersAsync(search, role, status, pageIndex, pageSize);
        return Ok(response);
    }

    // ==========================================
    // UC-19.1: Export Users List to CSV
    // ==========================================
    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportUsersCsv()
    {
        var (csvBytes, fileName) = await _userService.ExportUsersCsvAsync();
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }

    // ==========================================
    // UC-19.2: Get User Account Details & Sessions
    // ==========================================
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailAdminDto>> GetUserDetail(string id)
    {
        var (succeeded, error, detail) = await _userService.GetUserDetailAsync(id);
        if (!succeeded)
            return NotFound(new { error });

        return Ok(detail);
    }

    // ==========================================
    // UC-19.4: Role Assignment (Assign Roles)
    // ==========================================
    [HttpPut("{id}/roles")]
    public async Task<IActionResult> AssignRoles(string id, [FromBody] AssignRolesRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var (succeeded, error, message) = await _userService.AssignRolesAsync(id, request, currentAdminId);

        if (!succeeded)
            return BadRequest(new { error });

        return Ok(new
        {
            message,
            userId = id,
            roles = request.Roles
        });
    }

    // ==========================================
    // UC-19.3: Ban / Unban Account & Force Logout
    // ==========================================
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateUserStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var (succeeded, error, message, status) = await _userService.UpdateStatusAsync(id, request, currentAdminId, GetClientIp());

        if (!succeeded)
            return BadRequest(new { error });

        return Ok(new
        {
            message,
            userId = id,
            status = status?.ToString()
        });
    }
}
