using Billing.API.DTOs;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/plans")]
public class PlansController(IPlanService plans) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try { return Ok(await plans.GetActiveAsync(ct)); }
        catch (PricingConfigurationException)
        {
            return Problem(statusCode: 503, title: "Pricing is not fully configured.");
        }
    }
}
