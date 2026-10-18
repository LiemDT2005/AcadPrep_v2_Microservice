using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Notifications.Application.Interfaces;

namespace Notifications.API.Tests;

// Loaded only by the test host, never by the production API.
[ApiController]
[Authorize]
[Route("_foundation-tests")]
public class FoundationTestController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        currentUser.IsAuthenticated, currentUser.UserId, currentUser.Email, currentUser.Roles
    });

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult Admin() => NoContent();

    [HttpGet("status/{status:int}")]
    public IActionResult ErrorStatus(int status) => StatusCode(status);

    [HttpPost("validation")]
    public IActionResult Validate(FoundationRequest request) => Ok();

    [HttpGet("exception")]
    public IActionResult Exception() => throw new InvalidOperationException("Internal foundation test diagnostic.");

    [HttpPost("business-validation")]
    public IActionResult BusinessValidation() => UnprocessableEntity(
        new ValidationProblemDetails(new Dictionary<string, string[]> { ["audience"] = ["Invalid audience."] })
        {
            Status = StatusCodes.Status422UnprocessableEntity
        });
}

public record FoundationRequest([Required] string? Name);
