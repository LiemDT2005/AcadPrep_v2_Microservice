using BuildingBlocks.Common.Results;

namespace Notifications.API.Services.Interfaces;

public interface INotificationCommandService
{
    Task MarkAllReadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<Result> DeleteAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct = default);
    Task<Result> MarkReadAsync(Guid notificationId, Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct = default);
}
