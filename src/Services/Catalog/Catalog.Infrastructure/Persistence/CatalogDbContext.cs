using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Course>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Lessons).WithOne(x => x.Course!).HasForeignKey(x => x.CourseId);
        });

        modelBuilder.Entity<Lesson>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        });
    }
}
