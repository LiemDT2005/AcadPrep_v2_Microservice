using System.ComponentModel.DataAnnotations;

namespace Identity.API.Models;

public record RegisterRequest(
    [Required] string Email,
    [Required] string Password,
    [Required] string FullName);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record AuthResponse(string Token, string Email, string FullName, string UserId, DateTime ExpiresAt);
