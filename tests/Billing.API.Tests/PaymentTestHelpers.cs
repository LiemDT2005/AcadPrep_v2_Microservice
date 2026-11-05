using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Billing.API.Tests;

// Test-only VNPay simulator: signs callbacks with the factory's test secret, computed independently of the
// production VnpayClient (sorted ordinal, URL-encoded key=value joined by '&', HMAC-SHA512 hex).
internal static class PaymentTestHelpers
{
    public static string Sign(string secret, IDictionary<string, string> parameters)
    {
        var data = string.Join('&', parameters.Where(p => p.Key is not ("vnp_SecureHash" or "vnp_SecureHashType"))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));
        return Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    public static Dictionary<string, string> Callback(BillingApiFactory factory, string orderCode, long amountVnd,
        string responseCode = "00", string transactionStatus = "00", string? tmnCode = null, bool includeAmount = true)
    {
        var parameters = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = tmnCode ?? factory.VnpayTmnCode, ["vnp_TxnRef"] = orderCode,
            ["vnp_ResponseCode"] = responseCode, ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TransactionNo"] = "14226112", ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261105170112",
            ["vnp_OrderInfo"] = $"Thanh toan goi PRO_MONTH don {orderCode}", ["vnp_CardType"] = "ATM"
        };
        if (includeAmount) parameters["vnp_Amount"] = (amountVnd * 100).ToString();
        parameters["vnp_SecureHash"] = Sign(factory.VnpayHashSecret, parameters);
        return parameters;
    }

    public static string Url(string path, IDictionary<string, string> parameters) =>
        QueryHelpers.AddQueryString(path, parameters!);

    public static async Task<(string RspCode, string Message)> Ipn(HttpClient client, IDictionary<string, string> parameters)
    {
        using var response = await client.GetAsync(Url("/api/payments/vnpay-ipn", parameters));
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException($"IPN returned {response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("RspCode").GetString()!, json.RootElement.GetProperty("Message").GetString()!);
    }

    public static async Task<JsonElement> Checkout(HttpClient client, Guid planId, HttpStatusCode expected,
        string path = "/api/orders/checkout")
    {
        using var response = await client.PostAsJsonAsync(path, new { planId });
        if (response.StatusCode != expected)
            throw new InvalidOperationException($"Checkout returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
