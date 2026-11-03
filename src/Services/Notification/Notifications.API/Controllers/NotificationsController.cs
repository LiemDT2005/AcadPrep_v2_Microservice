using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;

namespace Notifications.API.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(NotificationQueryService notifications, NotificationCommandService commands,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId || userId == Guid.Empty)
            return Unauthorized();
        if (!Guid.TryParse(id, out var notificationId) || notificationId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "A non-empty GUID is required.");
            return ValidationProblem(ModelState);
        }
        var result = await commands.DeleteAsync(notificationId, userId, currentUser.Roles, ct);
        return result.IsSuccess ? NoContent() : Problem(statusCode: 404, title: "Notification not found.");
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(string id, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId || userId == Guid.Empty)
            return Unauthorized();
        if (!Guid.TryParse(id, out var notificationId) || notificationId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(id), "A non-empty GUID is required.");
            return ValidationProblem(ModelState);
        }
        var result = await commands.MarkReadAsync(notificationId, userId, currentUser.Roles, ct);
        return result.IsSuccess ? NoContent() : Problem(statusCode: 404, title: "Notification not found.");
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct,
        [FromQuery, Range(1, int.MaxValue)] int pageIndex = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery] bool unreadOnly = false)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId || userId == Guid.Empty)
            return Unauthorized();
        var result = await notifications.GetAsync(userId, currentUser.Roles, pageIndex, pageSize, unreadOnly, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(statusCode: 400, detail: result.Error);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId || userId == Guid.Empty)
            return Unauthorized();
        var result = await notifications.GetUnreadCountAsync(userId, currentUser.Roles, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(statusCode: 400, detail: result.Error);
    }
}
