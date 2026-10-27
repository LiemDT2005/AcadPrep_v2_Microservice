using AutoMapper;
using AutoMapper.QueryableExtensions;
using Content.Data;
using Content.DTOs;
using Content.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Content.Repositories;

public class ExamSeriesRepository(IContentDbContext db, IMapper mapper) : IExamSeriesRepository
{
    public Task<bool> ActiveExistsAsync(int id, CancellationToken ct = default) =>
        db.ExamSeries.AnyAsync(s => s.Id == id && !s.IsDeleted, ct);

    public Task<List<ExamSeriesDto>> GetActiveAsync(CancellationToken ct = default) =>
        db.ExamSeries.AsNoTracking().Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name).ThenByDescending(s => s.Year)
            .ProjectTo<ExamSeriesDto>(mapper.ConfigurationProvider).ToListAsync(ct);
}
