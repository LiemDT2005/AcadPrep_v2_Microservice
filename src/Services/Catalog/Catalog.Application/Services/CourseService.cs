using BuildingBlocks.Common.Results;
using Catalog.Application.DTOs;
using Catalog.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Services;

public class CourseService(ICatalogDbContext db)
{
    public async Task<Result<List<CourseListItemDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.Courses
            .AsNoTracking()
            .Where(c => c.IsPublished)
            .OrderBy(c => c.TargetScore)
            .Select(c => new CourseListItemDto(
                c.Id, c.Title, c.Level, c.TargetScore, c.Lessons.Count, c.Description))
            .ToListAsync(ct);
        return Result.Success(items);
    }

    public async Task<Result<CourseDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var course = await db.Courses
            .AsNoTracking()
            .Include(c => c.Lessons.OrderBy(l => l.Order))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course is null)
            return Result.Failure<CourseDetailDto>("Course not found.");

        var dto = new CourseDetailDto(
            course.Id, course.Title, course.Description, course.Level, course.TargetScore,
            course.Lessons.Select(l => new LessonDto(l.Id, l.Title, l.Skill, l.Order, l.DurationMinutes, l.Content)).ToList());

        return Result.Success(dto);
    }
}
