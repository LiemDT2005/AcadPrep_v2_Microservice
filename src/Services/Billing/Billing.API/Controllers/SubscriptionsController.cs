using Billing.API.DTOs;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Controllers;

[ApiController]
// Learner-only: Admin/Moderator do not buy plans or use learner features (Huy, 2026-11-05).
[Authorize(Roles = "Learner")]
[Route("api/subscriptions")]
public class SubscriptionsController(ISubscriptionService subscriptions, IUsageService usage, ICheckoutService checkout,
    ICurrentUser currentUser) : ControllerBase
{
    // UC-23.3 renewal: same checkout/VNPay flow with Kind=Renewal; 422 NoSubscriptionToRenew if never subscribed.
    [HttpPost("renew")]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Renew(CheckoutRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await checkout.CheckoutAsync(userId, currentUser.Email, request.PlanId!.Value,
            Entities.OrderKind.Renewal, CheckoutService.ClientIp(HttpContext), ct);
        return OrdersController.CheckoutResponse(this, result);
    }

    // UC-23.2: own limits, reset times and AI token usage. AI failures still return 200 with ai.available=false.
    [HttpGet("me/usage")]
    [ProducesResponseType(typeof(UsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMyUsage(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        try { return Ok(await usage.GetMineAsync(userId, ct)); }
        catch (PricingConfigurationException)
        {
            return Problem(statusCode: 503, title: "Plans are not fully configured.");
        }
    }

    // The caller reads only its own subscription; the user ID always comes from the token.
    [HttpGet("me")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        try { return Ok(await subscriptions.GetMineAsync(userId, ct)); }
        catch (PricingConfigurationException)
        {
            return Problem(statusCode: 503, title: "Plans are not fully configured.");
        }
    }
}
