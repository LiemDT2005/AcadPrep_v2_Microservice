using Billing.API.DTOs;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Controllers;

// UC-21.3. Learner-only: Admin/Moderator never buy plans (Huy, 2026-11-05).
[ApiController]
[Authorize(Roles = "Learner")]
[Route("api/orders")]
public class OrdersController(IOrderQueryService orders, ICheckoutService checkout, ICurrentUser currentUser) : ControllerBase
{
    // UC-21.2. 201 = new order; 200 = the existing unexpired Pending order for this plan (reused).
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await checkout.CheckoutAsync(userId, currentUser.Email, request.PlanId!.Value,
            Entities.OrderKind.Purchase, CheckoutService.ClientIp(HttpContext), ct);
        return CheckoutResponse(this, result);
    }

    internal static IActionResult CheckoutResponse(ControllerBase controller, BillingResult<CheckoutResultDto> result) =>
        result.Error switch
        {
            BillingError.None => result.Value!.Reused ? controller.Ok(result.Value)
                : controller.StatusCode(StatusCodes.Status201Created, result.Value),
            BillingError.NotFound => controller.Problem(statusCode: 404, title: result.Message),
            BillingError.Unavailable => controller.Problem(statusCode: 503, title: result.Message),
            BillingError.Conflict => controller.Problem(statusCode: 409, title: result.Message),
            _ => controller.Problem(statusCode: 422, title: result.Message)
        };

    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedDto<OrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] MyOrdersQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await orders.GetMineAsync(userId, query, ct));
    }

    // Someone else's order and a missing order are both 404.
    [HttpGet("{orderCode}")]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(string orderCode, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var order = await orders.GetMineByCodeAsync(userId, orderCode, ct);
        return order is null ? Problem(statusCode: 404, title: "Order not found.") : Ok(order);
    }
}
