using Billing.API.DTOs;
using Billing.API.Formatters;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Controllers;

// UC-21.5. JSON by default; Accept: text/csv returns the same points through CsvOutputFormatter.
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/revenue")]
public class AdminRevenueController(IRevenueService revenue) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(RevenueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery] RevenueQuery query, CancellationToken ct)
    {
        var (value, field, error) = await revenue.GetAsync(query, ct);
        if (value is null)
        {
            ModelState.AddModelError(field ?? string.Empty, error ?? "Invalid range.");
            return ValidationProblem(ModelState);
        }
        var csv = Request.GetTypedHeaders().Accept.Any(item =>
            item.MediaType.Equals(CsvOutputFormatter.MediaType, StringComparison.OrdinalIgnoreCase));
        return csv ? Ok(new RevenueCsvExport(value)) : Ok(value);
    }
}
