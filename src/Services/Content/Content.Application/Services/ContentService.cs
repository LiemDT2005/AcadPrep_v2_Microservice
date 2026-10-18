using BuildingBlocks.Common.Results;
using Content.Application.DTOs;
using Content.Application.Interfaces;
using Content.Domain.Entities;
using Content.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Content.Application.Services;

public class ContentService(IContentDbContext db, IExamAttemptChecker attemptChecker) : IContentService
{
    public async Task<Result<PagedResultDto<ExamListItemDto>>> GetPagedExamsAsync(
        string? keyword,
        string? status,
        string? examType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var query = db.Exams.AsNoTracking().Include(e => e.ExamSeries).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(k) || (e.Description != null && e.Description.ToLower().Contains(k)));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => e.IsDeleted || e.Status == ExamStatus.Hidden);
            }
            else if (status.Equals("Published", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => !e.IsDeleted && e.Status == ExamStatus.Published);
            }
            else if (status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e => !e.IsDeleted && e.Status == ExamStatus.Draft);
            }
        }

        if (!string.IsNullOrWhiteSpace(examType) && !examType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.ExamType == examType);
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.ExamType,
                e.Description,
                e.Duration,
                e.AudioUrl,
                Status = e.IsDeleted ? "Hidden" : e.Status.ToString(),
                e.IsDeleted,
                e.CreatedAt,
                QuestionCount = e.Questions.Count,
                e.ExamSeriesId,
                ExamSeriesName = e.ExamSeries.Name
            })
            .ToListAsync(ct);

        var list = new List<ExamListItemDto>();
        foreach (var item in items)
        {
            var attempts = await attemptChecker.GetAttemptCountAsync(item.Id, ct);
            list.Add(new ExamListItemDto(
                item.Id,
                item.Title,
                item.ExamType,
                item.Description,
                item.Duration,
                item.AudioUrl,
                item.Status,
                item.IsDeleted,
                item.CreatedAt,
                attempts,
                item.QuestionCount,
                item.ExamSeriesId,
                item.ExamSeriesName
            ));
        }

        return Result.Success(new PagedResultDto<ExamListItemDto>(list, totalCount, page, pageSize, totalPages));
    }

    public async Task<Result<ExamDetailDto>> GetExamByIdAsync(int id, CancellationToken ct = default)
    {
        var exam = await db.Exams.AsNoTracking()
            .Include(e => e.ExamSeries)
            .Include(e => e.Questions)
                .ThenInclude(q => q.QuestionOptions)
            .Include(e => e.Passages)
            .Include(e => e.QuestionGroups)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (exam == null)
            return Result.Failure<ExamDetailDto>("Exam not found.");

        var attemptCount = await attemptChecker.GetAttemptCountAsync(exam.Id, ct);

        var questionDtos = exam.Questions
            .OrderBy(q => q.QuestionNumber)
            .Select(q => new QuestionDetailDto(
                q.Id,
                q.QuestionNumber,
                q.Part,
                q.QuestionText,
                q.CorrectOption,
                q.Explanation,
                q.AudioUrl,
                q.AudioStartSecond,
                q.AudioEndSecond,
                q.ImageUrl,
                q.QuestionType,
                q.TopicTag,
                q.ExamId,
                exam.Title,
                q.PassageId,
                q.Passage?.Content,
                q.QuestionGroupId,
                q.QuestionGroup?.Name,
                q.QuestionOptions.Select(o => new QuestionOptionDto(o.Id, o.OptionLetter, o.OptionText)).ToList()
            )).ToList();

        var passageDtos = exam.Passages
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new PassageDetailDto(
                p.Id,
                p.ExamId,
                exam.Title,
                p.Content,
                p.ImageUrl,
                p.Explanation,
                p.DisplayOrder,
                p.QuestionGroupId
            )).ToList();

        var groupDtos = exam.QuestionGroups
            .Select(g => new QuestionGroupDetailDto(
                g.Id,
                g.Name,
                g.AudioUrl,
                g.AudioStartSecond,
                g.AudioEndSecond,
                g.ImageUrl,
                g.Explanation,
                g.ExamId,
                g.Passages.Select(p => new PassageDetailDto(p.Id, p.ExamId, exam.Title, p.Content, p.ImageUrl, p.Explanation, p.DisplayOrder, p.QuestionGroupId)).ToList(),
                g.Questions.Select(q => new QuestionDetailDto(
                    q.Id, q.QuestionNumber, q.Part, q.QuestionText, q.CorrectOption, q.Explanation,
                    q.AudioUrl, q.AudioStartSecond, q.AudioEndSecond, q.ImageUrl, q.QuestionType, q.TopicTag,
                    q.ExamId, exam.Title, q.PassageId, q.Passage?.Content, q.QuestionGroupId, g.Name,
                    q.QuestionOptions.Select(o => new QuestionOptionDto(o.Id, o.OptionLetter, o.OptionText)).ToList()
                )).ToList()
            )).ToList();

        var dto = new ExamDetailDto(
            exam.Id,
            exam.Title,
            exam.ExamType,
            exam.Description,
            exam.Duration,
            exam.AudioUrl,
            exam.IsDeleted ? "Hidden" : exam.Status.ToString(),
            exam.IsDeleted,
            exam.CreatedAt,
            exam.LastModifiedAt,
            attemptCount,
            exam.ExamSeriesId,
            exam.ExamSeries?.Name,
            questionDtos,
            passageDtos,
            groupDtos
        );

        return Result.Success(dto);
    }

    public async Task<Result<ExamDetailDto>> CreateExamAsync(CreateExamDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result.Failure<ExamDetailDto>("Exam title cannot be empty.");

        if (dto.Duration <= 0)
            return Result.Failure<ExamDetailDto>("Exam duration must be greater than 0 minutes.");

        var seriesExists = await db.ExamSeries.AnyAsync(s => s.Id == dto.ExamSeriesId && !s.IsDeleted, ct);
        if (!seriesExists)
            return Result.Failure<ExamDetailDto>("Selected Exam Series does not exist.");

        var exam = new Exam
        {
            Title = dto.Title.Trim(),
            ExamType = string.IsNullOrWhiteSpace(dto.ExamType) ? "Full Test" : dto.ExamType.Trim(),
            Description = dto.Description?.Trim(),
            Duration = dto.Duration,
            ExamSeriesId = dto.ExamSeriesId,
            AudioUrl = dto.AudioUrl?.Trim(),
            Status = ExamStatus.Draft,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Exams.Add(exam);
        await db.SaveChangesAsync(ct);

        return await GetExamByIdAsync(exam.Id, ct);
    }

    public async Task<Result> UpdateExamAsync(int id, UpdateExamDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exam == null)
            return Result.Failure("Exam not found.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result.Failure("Exam title cannot be empty.");

        if (dto.Duration <= 0)
            return Result.Failure("Exam duration must be greater than 0 minutes.");

        exam.Title = dto.Title.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ExamType))
            exam.ExamType = dto.ExamType.Trim();
        exam.Description = dto.Description?.Trim();
        exam.Duration = dto.Duration;
        exam.ExamSeriesId = dto.ExamSeriesId;
        exam.AudioUrl = dto.AudioUrl?.Trim();
        exam.LastModifiedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<ExamStatus>(dto.Status, true, out var parsedStatus))
        {
            exam.Status = parsedStatus;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(int id, string status, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exam == null)
            return Result.Failure("Exam not found.");

        if (!Enum.TryParse<ExamStatus>(status, true, out var parsedStatus))
            return Result.Failure($"Invalid status value: {status}");

        exam.Status = parsedStatus;
        if (parsedStatus == ExamStatus.Hidden)
        {
            exam.IsDeleted = true;
            exam.DeletedAt = DateTime.UtcNow;
        }
        else
        {
            exam.IsDeleted = false;
            exam.DeletedAt = null;
        }

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<DeleteExamResultDto>> DeleteExamAsync(int id, CancellationToken ct = default)
    {
        var exam = await db.Exams
            .Include(e => e.Questions)
                .ThenInclude(q => q.QuestionOptions)
            .Include(e => e.Passages)
            .Include(e => e.QuestionGroups)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (exam == null)
            return Result.Failure<DeleteExamResultDto>("Exam not found.");

        // Check attempts through Exam service / attempt checker
        var attemptCount = await attemptChecker.GetAttemptCountAsync(id, ct);

        if (attemptCount > 0)
        {
            // Soft delete: exam has learner attempts, keep history intact
            exam.SoftDelete();
            await db.SaveChangesAsync(ct);
            return Result.Success(new DeleteExamResultDto(
                $"Exam has {attemptCount} learner attempt(s). It was soft-deleted (hidden) to protect learner history.",
                true
            ));
        }
        else
        {
            // Hard delete: exam has 0 attempts, permanently remove
            db.Exams.Remove(exam);
            await db.SaveChangesAsync(ct);
            return Result.Success(new DeleteExamResultDto(
                "Exam has 0 attempts and was permanently deleted.",
                false
            ));
        }
    }

    public async Task<Result> RestoreExamAsync(int id, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exam == null)
            return Result.Failure("Exam not found.");

        exam.Restore();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<List<ExamSeriesDto>>> GetExamSeriesAsync(CancellationToken ct = default)
    {
        var series = await db.ExamSeries.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ThenByDescending(s => s.Year)
            .Select(s => new ExamSeriesDto(s.Id, s.Name, s.Year))
            .ToListAsync(ct);

        return Result.Success(series);
    }
}
