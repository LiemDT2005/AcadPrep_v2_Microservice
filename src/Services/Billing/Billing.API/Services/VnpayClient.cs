using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Billing.API.Services;

// Secrets (TmnCode, HashSecret) come only from user-secrets/environment; never log or return them.
public class VnpayOptions
{
    public const string SectionName = "Vnpay";
    public string? TmnCode { get; set; }
    public string? HashSecret { get; set; }
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    // Browser return (through the Gateway): /billing/api/payments/vnpay-return.
    public string? ReturnUrl { get; set; }
    public string Version { get; set; } = "2.1.0";
    public string Locale { get; set; } = "vn";
    public string OrderType { get; set; } = "other";
    public int OrderExpiryMinutes { get; set; } = 15;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret) &&
        Uri.TryCreate(ReturnUrl, UriKind.Absolute, out _) && Uri.TryCreate(PaymentUrl, UriKind.Absolute, out _);
}

public class FrontendOptions
{
    public const string SectionName = "Frontend";
    public string? PaymentResultUrl { get; set; }
}

public record VnpayPaymentRequest(string OrderCode, decimal AmountVnd, string OrderInfo, string ClientIp,
    DateTime CreatedAtUtc, DateTime ExpiresAtUtc);

public record VnpayCallback(bool IsSignatureValid, string? TmnCode, string? TxnRef, long? AmountScaled,
    string? ResponseCode, string? TransactionStatus, string? TransactionNo)
{
    public bool IsPaid => ResponseCode == "00" && TransactionStatus == "00";
}

// VNPay PAY API v2.1.0 (sandbox docs, checked 2026-11-05): parameters sorted by name (ordinal), key and value
// URL-encoded, signed with HMAC-SHA512 over the HashSecret; vnp_SecureHash/vnp_SecureHashType are not signed.
public class VnpayClient(IOptions<VnpayOptions> options)
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public bool IsConfigured => options.Value.IsConfigured;

    public string CreatePaymentUrl(VnpayPaymentRequest request)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) throw new InvalidOperationException("VNPay is not configured.");
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = settings.Version,
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = settings.TmnCode!,
            ["vnp_Amount"] = decimal.ToInt64(decimal.Round(request.AmountVnd, 0) * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = request.OrderCode,
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_OrderType"] = settings.OrderType,
            ["vnp_Locale"] = settings.Locale,
            ["vnp_ReturnUrl"] = settings.ReturnUrl!,
            ["vnp_IpAddr"] = request.ClientIp,
            ["vnp_CreateDate"] = Vietnam(request.CreatedAtUtc),
            ["vnp_ExpireDate"] = Vietnam(request.ExpiresAtUtc)
        };
        var data = Encode(parameters);
        return $"{settings.PaymentUrl}?{data}&vnp_SecureHash={Sign(settings.HashSecret!, data)}";
    }

    public VnpayCallback Validate(IEnumerable<KeyValuePair<string, string?>> query)
    {
        var settings = options.Value;
        var values = query.Where(item => item.Key.StartsWith("vnp_", StringComparison.Ordinal))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value ?? string.Empty, StringComparer.Ordinal);
        values.TryGetValue("vnp_SecureHash", out var hash);
        var signed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
            if (key is not ("vnp_SecureHash" or "vnp_SecureHashType") && value.Length > 0)
                signed[key] = value;

        var valid = settings.IsConfigured && !string.IsNullOrWhiteSpace(hash) &&
            FixedTimeEqualsHex(Sign(settings.HashSecret!, Encode(signed)), hash);
        long? amount = values.TryGetValue("vnp_Amount", out var rawAmount) &&
            long.TryParse(rawAmount, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return new VnpayCallback(valid, Get("vnp_TmnCode"), Get("vnp_TxnRef"), amount, Get("vnp_ResponseCode"),
            Get("vnp_TransactionStatus"), Get("vnp_TransactionNo"));

        string? Get(string key) => values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
    }

    public bool IsOwnMerchant(string? tmnCode) =>
        tmnCode is not null && string.Equals(tmnCode, options.Value.TmnCode, StringComparison.Ordinal);

    public static string Encode(SortedDictionary<string, string> parameters) =>
        string.Join('&', parameters.Select(item => $"{WebUtility.UrlEncode(item.Key)}={WebUtility.UrlEncode(item.Value)}"));

    public static string Sign(string secret, string data) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private static bool FixedTimeEqualsHex(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(actual.Trim().ToLowerInvariant()));

    private static string Vietnam(DateTime utc) =>
        (DateTime.SpecifyKind(utc, DateTimeKind.Utc) + VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
}
