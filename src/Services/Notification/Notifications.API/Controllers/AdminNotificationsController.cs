using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.API.Validation;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;

namespace Notifications.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notifications")]
public class AdminNotificationsController(NotificationAdminService notifications, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [AdminNotificationValidationFilter]
    public async Task<IActionResult> Create(CreateAdminNotificationRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } adminId || adminId == Guid.Empty)
            return Unauthorized();
        var result = await notifications.CreateAsync(request, adminId, ct);
        // There is no resource GET URI in N5; do not advertise a fabricated Location.
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Problem(statusCode: 422, title: "Invalid notification request.");
    }
}
