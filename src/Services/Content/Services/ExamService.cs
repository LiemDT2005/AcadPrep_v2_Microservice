using AutoMapper;
using BuildingBlocks.Common.Pagination;
using BuildingBlocks.Common.Results;
using Content.DTOs;
using Content.Repositories.Interfaces;
using Content.Services.Interfaces;

namespace Content.Services;

public class ExamService(
    IExamRepository exams,
    IExamAttemptChecker attemptChecker,
    IMapper mapper) : IExamService
{
    public async Task<Result<GetExamListResponse>> GetExamListAsync(
        string? search,
        string? seriesName,
        int? year,
        int pageIndex = PaginationConstants.DefaultPageIndex,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken ct = default)
    {
        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, PaginationConstants.MaxPageSize);

        var seriesNames = await exams.GetSeriesNamesAsync(ct);
        var years = await exams.GetSeriesYearsAsync(ct);
        var page = await exams.GetPublishedPageAsync(search, seriesName, year, pageIndex, pageSize, ct);

        foreach (var item in page.Items)
            item.AttemptCount = await attemptChecker.GetAttemptCountAsync(item.Id, ct);

        return Result.Success(new GetExamListResponse
        {
            Exams = page,
            SeriesFilters = ["All", .. seriesNames],
            YearFilters = ["All", .. years.Select(y => y.ToString())]
        });
    }

    public async Task<Result<GetExamDetailDto>> GetExamDetailAsync(
        int id,
        CancellationToken ct = default)
    {
        var exam = await exams.GetPublishedByIdAsync(id, ct);
        if (exam is null)
            return Result.Failure<GetExamDetailDto>("Exam not found or has been deleted.");

        var totalAttempts = await attemptChecker.GetAttemptCountAsync(id, ct);
        var questionsOfExam = exam.Questions;
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

        var detail = mapper.Map<GetExamDetailDto>(exam);
        detail.TotalQuestions = partsSummary.Sum(p => p.QuestionCount);
        detail.TotalAttempts = totalAttempts;
        detail.Parts = partsSummary;

        return Result.Success(detail);
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
