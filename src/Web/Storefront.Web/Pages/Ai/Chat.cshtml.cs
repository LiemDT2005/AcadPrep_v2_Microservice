using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Ai;

// JSON handlers behind the AI chat widget (Ajax). POST handlers are protected by antiforgery (header
// RequestVerificationToken). Only whitelisted fields are relayed; upstream diagnostics never reach the page.
public class ChatModel(AiApiClient ai) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnGetStateAsync(CancellationToken ct)
    {
        var usage = await ai.UsageAsync(ct);
        if (Fail(usage) is { } failure) return failure;
        var sessions = await ai.SessionsAsync(ct);
        return Json(200, new { usage = usage.Body, sessions = sessions.Success ? sessions.Body : null });
    }

    public async Task<IActionResult> OnGetDailyAsync(CancellationToken ct) => Relay(await ai.DailyAsync(ct));

    public async Task<IActionResult> OnGetMessagesAsync(Guid sessionId, CancellationToken ct) =>
        sessionId == Guid.Empty ? Json(404, new { message = "Không tìm thấy cuộc trò chuyện." }) : Relay(await ai.MessagesAsync(sessionId, ct));

    public async Task<IActionResult> OnPostDeleteAsync(Guid sessionId, CancellationToken ct)
    {
        var result = await ai.DeleteSessionAsync(sessionId, ct);
        return result.Status == 204 ? Json(200, new { deleted = true }) : Relay(result);
    }

    public async Task<IActionResult> OnPostSendAsync([FromBody] SendInput input, CancellationToken ct)
    {
        if (!TryValidateModel(input))
            return Json(400, new { message = "Tin nhắn cần từ 1 đến 2000 ký tự." });
        var result = await ai.ChatAsync(new { requestId = input.RequestId, sessionId = input.SessionId, message = input.Message!.Trim() }, ct);
        return Relay(result);
    }

    private IActionResult Relay(AiApiResult result)
    {
        if (Fail(result) is { } failure) return failure;
        return result.Status == 204 ? Json(200, new { }) : Json(result.Status, result.Body);
    }

    // Maps upstream errors to friendly, diagnostic-free JSON the widget understands.
    private IActionResult? Fail(AiApiResult result)
    {
        if (result.Success) return null;
        if (result.Status == 401) { HttpContext.Session.Remove("jwt"); }
        var usage = result.Status == 429 && result.Body is { } body && body.TryGetProperty("usage", out var value) ? value : (JsonElement?)null;
        var code = result.Status == 429 && result.Body is { } quota && quota.TryGetProperty("code", out var c) ? c.GetString() : null;
        var message = result.Status switch
        {
            401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục dùng trợ lý AI.",
            403 => "Trợ lý AI chỉ dành cho tài khoản Learner.",
            404 => "Không tìm thấy cuộc trò chuyện.",
            409 => "Tin nhắn trước vẫn đang được trả lời. Vui lòng đợi một chút.",
            429 when code == "MessageTooLarge" => "Tin nhắn quá dài so với hạn mức. Hãy rút gọn hoặc bắt đầu cuộc trò chuyện mới.",
            429 => "Bạn đã dùng hết hạn mức hôm nay. Thử lại sau.",
            400 => "Tin nhắn không hợp lệ.",
            503 => "Trợ lý AI đang tạm thời không khả dụng.",
            _ => "AI đang bận hoặc mất kết nối. Vui lòng thử lại sau."
        };
        return Json(result.Status, new { message, usage, code });
    }

    private JsonResult Json(int status, object? value) => new(value) { StatusCode = status };

    public class SendInput
    {
        [Required] public Guid? RequestId { get; set; }
        public Guid? SessionId { get; set; }
        [Required, StringLength(2000, MinimumLength = 1)] public string? Message { get; set; }
    }
}
