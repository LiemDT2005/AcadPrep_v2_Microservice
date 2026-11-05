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
    [Display(Name = "Title"), Required(ErrorMessage = "Please enter a title."),
     StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be no more than 200 characters.")]
    public string? Title { get => title; set => title = value?.Trim(); }

    [Display(Name = "Content"), Required(ErrorMessage = "Please enter content."),
     StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must be no more than 2000 characters.")]
    public string? Content { get; set; }

    [Display(Name = "Audience"), Required, RegularExpression("^(User|All|Learner|Moderator)$",
        ErrorMessage = "Invalid notification audience.")]
    public string Audience { get; set; } = "All";

    [Display(Name = "Recipient ID"), NotificationAudience]
    public string? TargetUserId { get; set; }
}
