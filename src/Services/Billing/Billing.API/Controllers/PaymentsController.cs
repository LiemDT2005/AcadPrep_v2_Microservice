using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Billing.API.Controllers;

// Called by VNPay (anonymous). Both callbacks are verified by HMAC-SHA512 before anything else and share one
// idempotent confirmation path (Huy, 2026-11-05, option B).
[ApiController]
[AllowAnonymous]
[Route("api/payments")]
public class PaymentsController(IPaymentConfirmationService confirmations, IOptions<FrontendOptions> frontend) : ControllerBase
{
    // Browser redirect after payment. Works on localhost (no public URL needed); redirects only to the
    // configured Storefront result page, which reads the real order status from the API.
    [HttpGet("vnpay-return")]
    public async Task<IActionResult> Return(CancellationToken ct)
    {
        var result = await confirmations.ConfirmAsync(Query(), Request.QueryString.Value, PaymentLogType.Return, ct);
        if (!Uri.TryCreate(frontend.Value.PaymentResultUrl, UriKind.Absolute, out var target))
            return Problem(statusCode: 503, title: "Payment result page is not configured.");
        var error = result.RspCode switch
        {
            "97" => "invalid_signature", "01" => "order_not_found", "04" => "invalid_amount", "99" => "processing_error",
            _ => null
        };
        var query = new List<string>();
        if (result.OrderCode is not null) query.Add($"orderCode={Uri.EscapeDataString(result.OrderCode)}");
        if (error is not null) query.Add($"error={error}");
        return Redirect(target.GetLeftPart(UriPartial.Path) + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty));
    }

    // Server-to-server notification. Always HTTP 200 with RspCode/Message (VNPay retries on 01/04/97/99).
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> Ipn(CancellationToken ct)
    {
        var result = await confirmations.ConfirmAsync(Query(), Request.QueryString.Value, PaymentLogType.Ipn, ct);
        return Ok(new VnpayIpnResponse(result.RspCode, result.Message));
    }

    private IEnumerable<KeyValuePair<string, string?>> Query() =>
        Request.Query.Select(item => new KeyValuePair<string, string?>(item.Key, item.Value.ToString()));
}
