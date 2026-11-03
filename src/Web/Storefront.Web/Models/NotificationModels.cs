using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Storefront.Web.Validation;

namespace Storefront.Web.Models;

public record NotificationItem(Guid Id, string Title, string Content, string Type, DateTimeOffset CreatedAt,
    bool IsRead, DateTimeOffset? ReadAt);
public record NotificationPage(IReadOnlyList<NotificationItem> Items, int PageIndex, int PageSize,
    int TotalCount, int TotalPages);
public record NotificationUnreadCount([property: JsonRequired] int UnreadCount);
public record NotificationCreated(Guid Id);
public record NotificationApiResult<T>(int Status, T? Data = default,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public bool Success => Status is >= 200 and < 300;
}

public class NotificationInput
{
    private string? title;
    [Display(Name = "Tiêu đề"), Required(ErrorMessage = "Vui lòng nhập tiêu đề."),
     StringLength(200, MinimumLength = 1, ErrorMessage = "Tiêu đề có tối đa 200 ký tự.")]
    public string? Title { get => title; set => title = value?.Trim(); }

    [Display(Name = "Nội dung"), Required(ErrorMessage = "Vui lòng nhập nội dung."),
     StringLength(2000, MinimumLength = 1, ErrorMessage = "Nội dung có tối đa 2000 ký tự.")]
    public string? Content { get; set; }

    [Display(Name = "Đối tượng"), Required, RegularExpression("^(User|All|Learner|Moderator)$",
        ErrorMessage = "Đối tượng nhận không hợp lệ.")]
    public string Audience { get; set; } = "All";

    [Display(Name = "ID người nhận"), NotificationAudience]
    public string? TargetUserId { get; set; }
}
