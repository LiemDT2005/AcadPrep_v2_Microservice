using BuildingBlocks.Common.Pagination;
using BuildingBlocks.Common.Results;
using Exam.Application.Common.Models;
using Exam.Application.DTOs;
using Exam.Application.Interfaces;
using Exam.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Exam.Application.Services;

public class ExamService(IExamDbContext db)
{
    public async Task<Result<GetExamListResponse>> GetExamListAsync(
        string? search,
        string? seriesName,
        int? year,
        int pageIndex = PaginationConstants.DefaultPageIndex,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken ct = default)
    {
        var seriesNames = await db.ExamSeries
            .Where(s => !s.IsDeleted)
            .Select(s => s.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

        var years = await db.ExamSeries
            .Where(s => !s.IsDeleted)
            .Select(s => s.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .Select(y => y.ToString())
            .ToListAsync(ct);

        var seriesFilters = new List<string> { "All" };
        seriesFilters.AddRange(seriesNames);

        var yearFilters = new List<string> { "All" };
        yearFilters.AddRange(years);

        var query = db.Exams.AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status == ExamStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(seriesName) && !seriesName.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.ExamSeries.Name == seriesName);
        }

        if (year.HasValue)
        {
            query = query.Where(e => e.ExamSeries.Year == year.Value);
        }

        query = query.OrderByDescending(e => e.CreatedAt);

        var dtoQuery = query.Select(e => new GetExamDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Duration = e.Duration,
            SeriesName = e.ExamSeries.Name,
            Year = e.ExamSeries.Year,
            CoverImageUrl = e.ExamSeries.CoverImageUrl,
            QuestionCount = e.Questions.Count(),
            AttemptCount = e.ExamAttempts.Count()
        });

        var paginatedResult = await PaginatedList<GetExamDto>.CreateAsync(
            dtoQuery,
            pageIndex,
            pageSize
        );

        var response = new GetExamListResponse
        {
            Exams = paginatedResult,
            SeriesFilters = seriesFilters,
            YearFilters = yearFilters
        };

        return Result.Success(response);
    }

    public async Task<Result<GetExamDetailDto>> GetExamDetailAsync(
        int id,
        string? userId = null,
        CancellationToken ct = default)
    {
        var exam = await db.Exams
            .AsNoTracking()
            .Where(e => e.Id == id && !e.IsDeleted && e.Status == ExamStatus.Published)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.Duration,
                SeriesName = e.ExamSeries.Name,
                Year = e.ExamSeries.Year,
                CoverImageUrl = e.ExamSeries.CoverImageUrl
            })
            .FirstOrDefaultAsync(ct);

        if (exam is null)
        {
            return Result.Failure<GetExamDetailDto>("Exam not found or has been deleted.");
        }

        var totalAttempts = await db.ExamAttempts
            .CountAsync(ea => ea.ExamId == id, ct);

        var questionsOfExam = await db.Questions
            .AsNoTracking()
            .Where(q => q.ExamId == id)
            .Select(q => new { q.Part, q.TopicTag })
            .ToListAsync(ct);

        var partsSummary = questionsOfExam
            .GroupBy(q => q.Part)
            .Select(g => new PartSummaryDto
            {
                PartNumber = g.Key,
                PartName = GetPartName(g.Key),
                QuestionCount = g.Count(),
                Tags = g.Where(q => !string.IsNullOrEmpty(q.TopicTag))
                        .Select(q => q.TopicTag!)
                        .Distinct()
                        .OrderBy(t => t)
                        .ToList()
            })
            .OrderBy(p => p.PartNumber)
            .ToList();

        var totalQuestions = partsSummary.Sum(p => p.QuestionCount);

        var examDetailDto = new GetExamDetailDto
        {
            Id = exam.Id,
            Title = exam.Title,
            Description = exam.Description,
            Duration = exam.Duration,
            SeriesName = exam.SeriesName,
            Year = exam.Year,
            CoverImageUrl = exam.CoverImageUrl,
            TotalQuestions = totalQuestions,
            TotalAttempts = totalAttempts,
            Parts = partsSummary
        };

        if (!string.IsNullOrEmpty(userId))
        {
            var history = await db.ExamAttempts
                .AsNoTracking()
                .Where(ea => ea.ExamId == id && ea.UserId == userId)
                .OrderByDescending(ea => ea.StartedAt)
                .Select(ea => new UserAttemptHistoryDto
                {
                    AttemptId = ea.Id,
                    ListeningScore = ea.ListeningScore,
                    ReadingScore = ea.ReadingScore,
                    TotalScore = ea.TotalScore,
                    StartedAt = ea.StartedAt,
                    CompletedAt = ea.CompletedAt,
                    RemainingTime = ea.RemainingTime,
                    IsSubmitted = ea.IsSubmitted
                })
                .ToListAsync(ct);

            examDetailDto.AttemptHistory = history;
        }

        return Result.Success(examDetailDto);
    }

    private static string GetPartName(int partNumber)
    {
        return partNumber switch
        {
            1 => "Part 1: Photographs",
            2 => "Part 2: Question - Response",
            3 => "Part 3: Conversations",
            4 => "Part 4: Talks",
            5 => "Part 5: Incomplete Sentences",
            6 => "Part 6: Text Completion",
            7 => "Part 7: Reading Comprehension",
            _ => $"Part {partNumber}"
        };
    }
}
