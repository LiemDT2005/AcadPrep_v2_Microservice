using Microsoft.EntityFrameworkCore;
using Progress.Domain.Entities;

namespace Progress.Application.Interfaces;

public interface IProgressDbContext
{
    DbSet<LearningRecord> LearningRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProgressCache
{
    Task SetUserSummaryAsync(string userId, object summary, CancellationToken ct = default);
    Task<T?> GetUserSummaryAsync<T>(string userId, CancellationToken ct = default);
    Task InvalidateAsync(string userId, CancellationToken ct = default);
}
