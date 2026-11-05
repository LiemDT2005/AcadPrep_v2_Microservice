using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Tests;

// Registered only by the test factory; not a production business endpoint.
[ApiController]
[Authorize]
[Route("test/auth")]
public class AuthenticationProbeController(ICurrentUser user) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { user.UserId, user.Roles });

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public IActionResult Admin() => Ok();
}
