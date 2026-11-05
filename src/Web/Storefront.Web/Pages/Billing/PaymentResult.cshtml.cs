using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Billing;

// UC-21.2 result page. The status always comes from GET /api/orders/{orderCode}; query parameters are never
// trusted as proof of payment (Billing confirms only signed VNPay callbacks).
public class PaymentResultModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    protected override string RequiredRole => "Learner";
    [BindProperty(SupportsGet = true)] public string? OrderCode { get; set; }
    [BindProperty(SupportsGet = true, Name = "error")] public string? ErrorCode { get; set; }
    public BillingOrderDetail? Order { get; private set; }
    public string? Warning { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Warning = ErrorCode switch
        {
            "invalid_signature" => "Không xác minh được phản hồi từ VNPay. Trạng thái bên dưới là trạng thái thật của đơn.",
            "invalid_amount" => "Số tiền phản hồi không khớp với đơn hàng. Vui lòng liên hệ hỗ trợ nếu đã bị trừ tiền.",
            "order_not_found" => "Không tìm thấy đơn hàng tương ứng.",
            "processing_error" => "Hệ thống đang xử lý kết quả thanh toán. Vui lòng tải lại sau ít phút.",
            null => null,
            _ => "Có lỗi khi nhận kết quả thanh toán."
        };
        if (string.IsNullOrWhiteSpace(OrderCode)) return Page();
        var result = await Billing.GetMyOrderAsync(OrderCode.Trim(), ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success) Order = result.Data;
        else if (result.Status == 404) Warning ??= "Không tìm thấy đơn hàng.";
        else Error = "Chưa thể tải trạng thái đơn hàng. Vui lòng tải lại trang.";
        return Page();
    }
}
