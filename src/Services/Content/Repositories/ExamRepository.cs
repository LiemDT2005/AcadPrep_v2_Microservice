using AutoMapper;
using Content.Common;
using AutoMapper.QueryableExtensions;
using Content.Data;
using Content.DTOs;
using Content.Entities;
using Content.Entities.Enums;
using Content.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Content.Repositories;

public class ExamRepository(IContentDbContext db, IMapper mapper) : IExamRepository
{
    public Task<List<string>> GetSeriesNamesAsync(CancellationToken ct = default) =>
        db.ExamSeries.AsNoTracking().Where(s => !s.IsDeleted)
            .Select(s => s.Name).Distinct().OrderBy(n => n).ToListAsync(ct);

    public Task<List<int>> GetSeriesYearsAsync(CancellationToken ct = default) =>
        db.ExamSeries.AsNoTracking().Where(s => !s.IsDeleted)
            .Select(s => s.Year).Distinct().OrderByDescending(y => y).ToListAsync(ct);

    public Task<PaginatedList<GetExamDto>> GetPublishedPageAsync(
        string? search, string? seriesName, int? year, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var query = db.Exams.AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status == ExamStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(seriesName) && !seriesName.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.ExamSeries.Name == seriesName);
        if (year.HasValue)
            query = query.Where(e => e.ExamSeries.Year == year.Value);

        var projection = query.OrderByDescending(e => e.CreatedAt)
            .ProjectTo<GetExamDto>(mapper.ConfigurationProvider);
        return PaginatedList<GetExamDto>.CreateAsync(projection, pageIndex, pageSize, ct);
    }

    public Task<Exam?> GetPublishedByIdAsync(int id, CancellationToken ct = default) =>
        db.Exams.AsNoTracking().Include(e => e.ExamSeries).Include(e => e.Questions)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted && e.Status == ExamStatus.Published, ct);

    public async Task<PagedResultDto<ExamListItemDto>> GetPagedAsync(
        string? keyword, string? status, string? examType, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Exams.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(k)
                || (e.Description != null && e.Description.ToLower().Contains(k)));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                query = query.Where(e => e.IsDeleted || e.Status == ExamStatus.Hidden);
            else if (status.Equals("Published", StringComparison.OrdinalIgnoreCase))
                query = query.Where(e => !e.IsDeleted && e.Status == ExamStatus.Published);
            else if (status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
                query = query.Where(e => !e.IsDeleted && e.Status == ExamStatus.Draft);
        }

        if (!string.IsNullOrWhiteSpace(examType) && !examType.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(e => e.ExamType == examType);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ProjectTo<ExamListItemDto>(mapper.ConfigurationProvider).ToListAsync(ct);
        return new PagedResultDto<ExamListItemDto>(items, totalCount, page, pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public Task<Exam?> GetDetailByIdAsync(int id, CancellationToken ct = default) =>
        db.Exams.AsNoTracking()
            .Include(e => e.ExamSeries)
            .Include(e => e.Questions).ThenInclude(q => q.QuestionOptions)
            .Include(e => e.Passages)
            .Include(e => e.QuestionGroups)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Exam?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Exams.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(Exam exam, CancellationToken ct = default)
    {
        db.Exams.Add(exam);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Exam exam, CancellationToken ct = default)
    {
        db.Exams.Update(exam);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Exam exam, CancellationToken ct = default)
    {
        db.Exams.Remove(exam);
        await db.SaveChangesAsync(ct);
    }
}
