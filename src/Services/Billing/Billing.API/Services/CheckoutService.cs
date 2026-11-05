using System.Net;
using System.Security.Cryptography;
using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Billing.API.Services;

// UC-21.2 checkout and UC-23.3 renewal. Amount, plan and duration always come from Billing data, never the
// client. At most one unexpired Pending order per user and plan (Huy, 2026-11-05): a repeated click returns it.
public class CheckoutService(IOrderRepository orders, ISubscriptionRepository subscriptions, IUnitOfWork unitOfWork,
    IRevenueCache revenueCache, VnpayClient vnpay, IOptions<VnpayOptions> options, TimeProvider clock, ILogger<CheckoutService> logger)
    : ICheckoutService
{
    public const string PlanNotPurchasable = "PlanNotPurchasable";
    public const string NoSubscriptionToRenew = "NoSubscriptionToRenew";

    public async Task<BillingResult<CheckoutResultDto>> CheckoutAsync(Guid userId, string? email, Guid planId,
        string kind, string clientIp, CancellationToken ct)
    {
        var plan = await subscriptions.GetPlanAsync(planId, ct);
        if (plan is null || !plan.IsActive)
            return BillingResult<CheckoutResultDto>.Failure(BillingError.NotFound, "Plan not found.");
        if (plan.Code == SubscriptionService.FreePlanCode || plan.Price <= 0 || plan.DurationDays <= 0)
            return BillingResult<CheckoutResultDto>.Failure(BillingError.Unprocessable, PlanNotPurchasable);
        if (kind == OrderKind.Renewal && !await subscriptions.HasAnyAsync(userId, ct))
            return BillingResult<CheckoutResultDto>.Failure(BillingError.Unprocessable, NoSubscriptionToRenew);
        if (!vnpay.IsConfigured)
            return BillingResult<CheckoutResultDto>.Failure(BillingError.Unavailable, "Payment is not configured.");

        var now = clock.GetUtcNow().UtcDateTime;
        if (await orders.GetLivePendingAsync(userId, planId, now, ct) is { } existing) return Reuse(existing);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
                // A Pending order past ExpiresAt no longer counts: close it so the unique slot frees up.
                var stale = await orders.GetExpiredPendingForUpdateAsync(userId, planId, now, ct);
                foreach (var order in stale) { order.Status = OrderStatus.Expired; order.Version = Guid.NewGuid(); }
                if (stale.Count > 0) await unitOfWork.SaveChangesAsync(ct);

                var created = new Order
                {
                    OrderCode = NewOrderCode(), UserId = userId, UserEmail = email, PlanId = plan.Id,
                    PlanCode = plan.Code, PlanName = plan.Name, DurationDays = plan.DurationDays, Amount = plan.Price,
                    Kind = kind, Status = OrderStatus.Pending, CreatedAt = now,
                    ExpiresAt = now.AddMinutes(options.Value.OrderExpiryMinutes)
                };
                created.PaymentUrl = vnpay.CreatePaymentUrl(new VnpayPaymentRequest(created.OrderCode, created.Amount,
                    $"Thanh toan goi {plan.Code} don {created.OrderCode}", clientIp, created.CreatedAt, created.ExpiresAt));
                orders.Add(created);
                orders.Add(new PaymentLog
                {
                    OrderId = created.Id, OrderCode = created.OrderCode, Type = PaymentLogType.Create,
                    RawQuery = created.PaymentUrl, IsSignatureValid = true, Result = kind, CreatedAt = now
                });
                await unitOfWork.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                await revenueCache.InvalidateAsync(ct); // pending/expired counts changed
                logger.LogInformation("Checkout order created. OrderCode={OrderCode} Kind={Kind}", created.OrderCode, kind);
                return BillingResult<CheckoutResultDto>.Success(
                    new CheckoutResultDto(created.OrderCode, created.PaymentUrl, created.ExpiresAt, false));
            }
            catch (DbUpdateException)
            {
                // A concurrent click won the per-plan Pending slot (or, very rarely, an order code collided).
                unitOfWork.ResetTracking();
                if (await orders.GetLivePendingAsync(userId, planId, now, ct) is { } winner) return Reuse(winner);
            }
        }
        return BillingResult<CheckoutResultDto>.Failure(BillingError.Conflict, "Checkout changed concurrently. Please retry.");
    }

    // Prefer the forwarded browser address (Storefront -> Gateway); VNPay only records it.
    public static string ClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
        var address = IPAddress.TryParse(forwarded, out var parsed) ? parsed : context.Connection.RemoteIpAddress;
        if (address is null || IPAddress.IsLoopback(address)) return "127.0.0.1";
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private static BillingResult<CheckoutResultDto> Reuse(Order order) => BillingResult<CheckoutResultDto>.Success(
        new CheckoutResultDto(order.OrderCode, order.PaymentUrl!, order.ExpiresAt, true));

    // 14 random digits from a CSPRNG: unguessable, numeric, unique per day as VNPay requires (unique index).
    private static string NewOrderCode() =>
        string.Concat(Enumerable.Range(0, 14).Select(index => RandomNumberGenerator.GetInt32(index == 0 ? 1 : 0, 10)));
}
