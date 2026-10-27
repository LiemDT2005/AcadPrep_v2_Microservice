using Microsoft.EntityFrameworkCore;
using Progress.Application.Interfaces;
using Progress.Domain.Entities;

namespace Progress.Infrastructure.Persistence;

public class ProgressDbContext(DbContextOptions<ProgressDbContext> options) : DbContext(options), IProgressDbContext
{
    public DbSet<LearningRecord> LearningRecords => Set<LearningRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LearningRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.AttemptId).IsUnique();
        });
    }
}
