namespace Learning.Application.Interfaces;

public interface ILearningCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task InvalidateUserAsync(string userId, CancellationToken ct = default);
    Task InvalidateAdminReportsAsync(CancellationToken ct = default);
}
