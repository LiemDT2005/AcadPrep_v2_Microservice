using Learning.API.Data;
using Learning.API.Entities;
using Learning.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Repositories;

public class UserProfileRepository(LearningDbContext db) : IUserProfileRepository
{
    public async Task<List<UserProfileSnapshot>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.UserProfiles.AsNoTracking().ToListAsync(ct);
    }

    public async Task<int> CountAllAsync(CancellationToken ct = default)
    {
        return await db.UserProfiles.CountAsync(ct);
    }

    public async Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
    {
        return await db.UserProfiles.CountAsync(u => u.CreatedAt >= sinceUtc, ct);
    }
}
