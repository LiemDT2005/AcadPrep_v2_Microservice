using Learning.API.Entities;

namespace Learning.API.Repositories.Interfaces;

public interface IUserProfileRepository
{
    Task<List<UserProfileSnapshot>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
}
