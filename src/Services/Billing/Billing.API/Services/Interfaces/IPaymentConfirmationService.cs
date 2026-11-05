using Billing.API.Services;

namespace Billing.API.Services.Interfaces;

public interface IPaymentConfirmationService
{
    // Shared by the VNPay Return and IPN callbacks (source = PaymentLogType.Return / Ipn); idempotent.
    Task<PaymentConfirmation> ConfirmAsync(IEnumerable<KeyValuePair<string, string?>> query, string? rawQuery,
        string source, CancellationToken ct);
}
