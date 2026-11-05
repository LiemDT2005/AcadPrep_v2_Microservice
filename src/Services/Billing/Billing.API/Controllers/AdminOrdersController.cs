using Billing.API.DTOs;
using Billing.API.Formatters;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Billing.API.Controllers;

// UC-21.4 order list. JSON by default; Accept: text/csv exports one month through CsvOutputFormatter.
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders")]
public class AdminOrdersController(IOrderQueryService orders) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedDto<AdminOrderRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Get([FromQuery] AdminOrdersQuery query, CancellationToken ct)
    {
        if (!WantsCsv())
        {
            if (query.Month is not null)
            {
                ModelState.AddModelError(nameof(query.Month), "Month applies only to CSV export.");
                return ValidationProblem(ModelState);
            }
            return Ok(await orders.GetAdminPageAsync(query, ct));
        }

        var export = await orders.ExportMonthAsync(query, ct);
        // ProblemDetails always negotiates application/problem+json, even when CSV was requested.
        return export.IsSuccess ? Ok(export.Value) : Problem(statusCode: 422, title: export.Message);
    }

    private bool WantsCsv() => Request.GetTypedHeaders().Accept
        .Any(value => value.MediaType.Equals(CsvOutputFormatter.MediaType, StringComparison.OrdinalIgnoreCase));
}
