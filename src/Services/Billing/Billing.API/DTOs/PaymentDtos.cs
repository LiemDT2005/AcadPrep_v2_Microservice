using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Billing.API.Validation;

namespace Billing.API.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CheckoutRequest
{
    [Required, NotEmptyGuid]
    public Guid? PlanId { get; init; }
}

// Reused = an unexpired Pending order for the same plan already existed and is returned unchanged.
public record CheckoutResultDto(string OrderCode, string PaymentUrl, DateTime ExpiresAt, bool Reused);

// VNPay requires exactly these property names in the IPN acknowledgement.
public record VnpayIpnResponse(
    [property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message);
