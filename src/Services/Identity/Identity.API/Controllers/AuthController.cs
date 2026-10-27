using Identity.API.Data;
using Identity.API.Models;
using Identity.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return BadRequest(new { error = "Email already registered." });

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        var (token, expires) = tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Email!, user.FullName, user.Id, expires));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { error = "Invalid email or password." });

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!check.Succeeded)
            return Unauthorized(new { error = "Invalid email or password." });

        var (token, expires) = tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Email!, user.FullName, user.Id, expires));
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();
        return Ok(new { user.Id, user.Email, user.FullName });
    }
}
