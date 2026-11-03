using AutoMapper;
using BuildingBlocks.Common.Results;
using Content.DTOs;
using Content.Entities;
using Content.Entities.Enums;
using Content.Repositories.Interfaces;
using Content.Services.Interfaces;

namespace Content.Services;

public class ContentService(
    IExamRepository exams,
    IExamSeriesRepository series,
    IExamAttemptChecker attemptChecker,
    IMapper mapper) : IContentService
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
        var result = await exams.GetPagedAsync(keyword, status, examType, page, pageSize, ct);
        var items = new List<ExamListItemDto>();
        foreach (var item in result.Items)
        {
            var attempts = await attemptChecker.GetAttemptCountAsync(item.Id, ct);
            items.Add(item with { AttemptCount = attempts });
        }

        return Result.Success(result with { Items = items });
    }

    public async Task<Result<ExamDetailDto>> GetExamByIdAsync(int id, CancellationToken ct = default)
    {
        var exam = await exams.GetDetailByIdAsync(id, ct);
        if (exam is null)
            return Result.Failure<ExamDetailDto>("Exam not found.");

        var attemptCount = await attemptChecker.GetAttemptCountAsync(exam.Id, ct);
        var dto = mapper.Map<ExamDetailDto>(exam) with { AttemptCount = attemptCount };
        return Result.Success(dto);
    }

    public async Task<Result<ExamDetailDto>> CreateExamAsync(CreateExamDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result.Failure<ExamDetailDto>("Exam title cannot be empty.");

        if (dto.Duration <= 0)
            return Result.Failure<ExamDetailDto>("Exam duration must be greater than 0 minutes.");

        var seriesExists = await series.ActiveExistsAsync(dto.ExamSeriesId, ct);
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

        await exams.AddAsync(exam, ct);

        return await GetExamByIdAsync(exam.Id, ct);
    }

    public async Task<Result> UpdateExamAsync(int id, UpdateExamDto dto, CancellationToken ct = default)
    {
        var exam = await exams.GetByIdAsync(id, ct);
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

        await exams.UpdateAsync(exam, ct);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(int id, string status, CancellationToken ct = default)
    {
        var exam = await exams.GetByIdAsync(id, ct);
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
        await exams.UpdateAsync(exam, ct);
        return Result.Success();
    }

    public async Task<Result<DeleteExamResultDto>> DeleteExamAsync(int id, CancellationToken ct = default)
    {
        var exam = await exams.GetByIdAsync(id, ct);

        if (exam == null)
            return Result.Failure<DeleteExamResultDto>("Exam not found.");

        // Check attempts through Exam service / attempt checker
        var attemptCount = await attemptChecker.GetAttemptCountAsync(id, ct);

        if (attemptCount > 0)
        {
            // Soft delete: exam has learner attempts, keep history intact
            exam.SoftDelete();
            await exams.UpdateAsync(exam, ct);
            return Result.Success(new DeleteExamResultDto(
                $"Exam has {attemptCount} learner attempt(s). It was soft-deleted (hidden) to protect learner history.",
                true
            ));
        }
        else
        {
            // Hard delete: exam has 0 attempts, permanently remove
            await exams.DeleteAsync(exam, ct);
            return Result.Success(new DeleteExamResultDto(
                "Exam has 0 attempts and was permanently deleted.",
                false
            ));
        }
    }

    public async Task<Result> RestoreExamAsync(int id, CancellationToken ct = default)
    {
        var exam = await exams.GetByIdAsync(id, ct);
        if (exam == null)
            return Result.Failure("Exam not found.");

        exam.Restore();
        await exams.UpdateAsync(exam, ct);
        return Result.Success();
    }

    public async Task<Result<List<ExamSeriesDto>>> GetExamSeriesAsync(CancellationToken ct = default)
    {
        var result = await series.GetActiveAsync(ct);
        return Result.Success(result);
    }
}
