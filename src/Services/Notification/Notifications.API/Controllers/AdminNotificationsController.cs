using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notifications.API.Validation;
using Notifications.API.DTOs;
using Notifications.API.Data;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services.Interfaces;
using Notifications.API.Services;

namespace Notifications.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notifications")]
public class AdminNotificationsController(INotificationAdminService notifications, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [AdminNotificationValidationFilter]
    public async Task<IActionResult> Create(CreateAdminNotificationRequest request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } adminId || adminId == Guid.Empty)
            return Unauthorized();
        var result = await notifications.CreateAsync(request, adminId, ct);
        // There is no resource GET URI in N5; do not advertise a fabricated Location.
        if (result.IsSuccess) return StatusCode(StatusCodes.Status201Created, result.Value);
        if (result.Error == NotificationAdminService.TargetNotFound)
        {
            ModelState.AddModelError(nameof(request.TargetUserId), NotificationAdminService.TargetNotFound);
            return ValidationProblem(statusCode: 422, modelStateDictionary: ModelState);
        }
        return result.Error == NotificationAdminService.IdentityUnavailable
            ? Problem(statusCode: 503, title: NotificationAdminService.IdentityUnavailable)
            : Problem(statusCode: 422, title: NotificationAdminService.InvalidRequest);
    }
}
