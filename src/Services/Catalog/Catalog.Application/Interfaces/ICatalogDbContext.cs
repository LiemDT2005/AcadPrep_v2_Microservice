using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Interfaces;

public interface ICatalogDbContext
{
    DbSet<Course> Courses { get; }
    DbSet<Lesson> Lessons { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
