namespace Billing.API.Entities;

public static class PaymentLogType
{
    public const string Create = "Create";
    public const string Return = "Return";
    public const string Ipn = "Ipn";
}

// Reconciliation trail for every VNPay interaction. RawQuery holds only the parameters VNPay sent or the
// generated link (signature included, never the HashSecret).
public class PaymentLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrderId { get; set; }
    public string? OrderCode { get; set; }
    public required string Type { get; set; }
    public string? RawQuery { get; set; }
    public bool IsSignatureValid { get; set; }
    public string? RspCode { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
}
