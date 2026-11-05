using Billing.API.DTOs;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/subscriptions")]
public class AdminSubscriptionsController(ISubscriptionGrantService grants, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("grant")]
    [ProducesResponseType(typeof(SubscriptionGrantedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Grant(GrantSubscriptionRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        var result = await grants.GrantAsync(request, adminId, ct);
        // There is no subscription GET-by-id resource yet; do not advertise a fabricated Location.
        return result.Error switch
        {
            BillingError.None => StatusCode(StatusCodes.Status201Created, result.Value),
            BillingError.NotFound => Problem(statusCode: 404, title: result.Message),
            BillingError.Conflict => Problem(statusCode: 409, title: result.Message),
            BillingError.Unavailable => Problem(statusCode: 503, title: result.Message),
            _ => Problem(statusCode: 422, title: result.Message)
        };
    }
}
