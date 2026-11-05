using System.Text.Json;
using BuildingBlocks.Common.Events;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Services;

public record PaymentConfirmation(string RspCode, string Message, string? OrderCode);

// One idempotent path for both VNPay callbacks (Huy, 2026-11-05, option B): the signed browser Return and the
// server-to-server IPN. Whichever arrives first confirms the order; the other gets 02 "already confirmed".
// Checks follow the VNPay docs order: checksum (+ merchant), order, amount, order state, then update.
public class PaymentConfirmationService(IOrderRepository orders, ISubscriptionRepository subscriptions,
    IOutboxRepository outbox, IUnitOfWork unitOfWork, VnpayClient vnpay, IRevenueCache revenueCache, TimeProvider clock,
    ILogger<PaymentConfirmationService> logger) : IPaymentConfirmationService
{
    private readonly SubscriptionActivator activator = new(subscriptions);

    public async Task<PaymentConfirmation> ConfirmAsync(IEnumerable<KeyValuePair<string, string?>> query, string? rawQuery,
        string source, CancellationToken ct)
    {
        var callback = vnpay.Validate(query);
        var log = new PaymentLog
        {
            Type = source, RawQuery = rawQuery is { Length: > 4000 } ? rawQuery[..4000] : rawQuery,
            IsSignatureValid = callback.IsSignatureValid && vnpay.IsOwnMerchant(callback.TmnCode),
            OrderCode = callback.TxnRef is { Length: <= 20 } code ? code : null
        };
        PaymentConfirmation result;
        try { result = await ProcessAsync(callback, log, ct); }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Unknown failure: VNPay retries the IPN on 99; nothing partial was committed.
            unitOfWork.ResetTracking();
            logger.LogError("VNPay {Source} processing failed. OrderCode={OrderCode} Reason={Reason}",
                source, log.OrderCode, exception.GetType().Name);
            result = new PaymentConfirmation("99", "Unknown error", log.OrderCode);
        }

        unitOfWork.ResetTracking();
        log.RspCode = result.RspCode;
        log.Result = result.Message;
        log.CreatedAt = clock.GetUtcNow().UtcDateTime;
        orders.Add(log);
        try { await unitOfWork.SaveChangesAsync(ct); }
        catch (DbUpdateException) { logger.LogWarning("Payment log could not be stored. OrderCode={OrderCode}", log.OrderCode); }
        return result;
    }

    private async Task<PaymentConfirmation> ProcessAsync(VnpayCallback callback, PaymentLog log, CancellationToken ct)
    {
        if (!callback.IsSignatureValid || !vnpay.IsOwnMerchant(callback.TmnCode))
            return new PaymentConfirmation("97", "Invalid signature", null);
        if (callback.TxnRef is null) return new PaymentConfirmation("01", "Order not found", null);

        for (var attempt = 1; ; attempt++)
        {
            var order = await orders.GetByCodeForUpdateAsync(callback.TxnRef, ct);
            if (order is null) return new PaymentConfirmation("01", "Order not found", callback.TxnRef);
            log.OrderId = order.Id;
            // A missing or fractional amount is never accepted.
            if (callback.AmountScaled is not { } scaled || scaled % 100 != 0 || scaled / 100 != order.Amount)
                return new PaymentConfirmation("04", "Invalid amount", order.OrderCode);
            if (order.Status is OrderStatus.Paid or OrderStatus.Failed)
                return new PaymentConfirmation("02", "Order already confirmed", order.OrderCode);

            try
            {
                await ApplyAsync(order, callback, ct);
                // Paid changes revenue, Failed changes the status counts: both must be visible immediately.
                await revenueCache.InvalidateAsync(ct);
                return new PaymentConfirmation("00", "Confirm Success", order.OrderCode);
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // The other callback (or a grant/expiry job) changed the order or subscription first: re-read.
                unitOfWork.ResetTracking();
            }
        }
    }

    private async Task<bool> ApplyAsync(Order order, VnpayCallback callback, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        order.VnpResponseCode = callback.ResponseCode;
        order.VnpTransactionNo = callback.TransactionNo;
        order.Version = Guid.NewGuid(); // conditional update: a concurrent confirmation fails and re-reads
        if (!callback.IsPaid)
        {
            order.Status = OrderStatus.Failed;
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("VNPay payment not successful. OrderCode={OrderCode} ResponseCode={Code}",
                order.OrderCode, callback.ResponseCode);
            return false;
        }

        // A Pending order, or one already Expired by time/job, is accepted: the customer paid before
        // vnp_ExpireDate (VNPay enforces it) and the notification may arrive late (Huy, 2026-11-05).
        order.Status = OrderStatus.Paid;
        order.PaidAt = now;
        var plan = await subscriptions.GetPlanAsync(order.PlanId, ct)
            ?? throw new InvalidOperationException("The ordered plan no longer exists.");
        var activation = await activator.ActivateAsync(order.UserId, plan, order.DurationDays, order.UserEmail,
            new ActivationSource(SourceOrderId: order.Id), now, ct);
        var @event = new SubscriptionActivatedEvent(order.UserId, order.UserEmail, order.PlanCode, order.PlanName,
            activation.Subscription.StartDate, activation.Subscription.EndDate, order.OrderCode,
            order.Kind == OrderKind.Renewal) { Id = Guid.NewGuid(), OccurredOn = now };
        outbox.Add(new OutboxMessage
        {
            Id = @event.Id, Type = nameof(SubscriptionActivatedEvent), Payload = JsonSerializer.Serialize(@event),
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("VNPay payment confirmed. OrderCode={OrderCode} SubscriptionId={SubscriptionId}",
            order.OrderCode, activation.Subscription.Id);
        return true;
    }
}
