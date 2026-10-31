using AutoMapper;
using BuildingBlocks.Common.Results;
using Content.Data;
using Content.DTOs;
using Content.Entities;
using Content.Entities.Enums;
using Content.Repositories;
using Content.Repositories.Interfaces;
using Content.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Content.Services;

public class ContentService(
    IExamRepository exams,
    IExamSeriesRepository series,
    IExamAttemptChecker attemptChecker,
    IMapper mapper,
    IContentDbContext? dbContext = null) : IContentService
{
    private IContentDbContext db => dbContext ?? ((ExamRepository)exams).DbContext;

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

        var attemptCount = await attemptChecker.GetAttemptCountAsync(id, ct);

        if (attemptCount > 0)
        {
            exam.SoftDelete();
            await exams.UpdateAsync(exam, ct);
            return Result.Success(new DeleteExamResultDto(
                $"Exam has {attemptCount} learner attempt(s). It was soft-deleted (hidden) to protect learner history.",
                true
            ));
        }
        else
        {
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

    // ==========================================
    // QUESTION BANK & EXAM CONTENT (UC-17.1 - UC-17.7)
    // ==========================================

    public async Task<Result<PagedResultDto<QuestionListItemDto>>> GetPagedQuestionsAsync(
        string? keyword,
        int? part,
        string? difficulty,
        int? examId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var query = db.Questions.AsNoTracking()
            .Include(q => q.Exam)
            .Include(q => q.QuestionGroup)
            .Include(q => q.QuestionOptions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(q =>
                (q.QuestionText != null && q.QuestionText.ToLower().Contains(k)) ||
                (q.Explanation != null && q.Explanation.ToLower().Contains(k)) ||
                (q.QuestionType != null && q.QuestionType.ToLower().Contains(k)) ||
                (q.TopicTag != null && q.TopicTag.ToLower().Contains(k)) ||
                (q.Exam != null && q.Exam.Title.ToLower().Contains(k)) ||
                q.QuestionOptions.Any(o => o.OptionText.ToLower().Contains(k)));
        }

        if (part.HasValue && part.Value >= 1 && part.Value <= 7)
        {
            query = query.Where(q => q.Part == part.Value);
        }

        if (!string.IsNullOrWhiteSpace(difficulty) && !difficulty.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var d = difficulty.Trim().ToLower();
            if (d == "medium")
            {
                query = query.Where(q => q.Difficulty == null || q.Difficulty == "" || q.Difficulty.ToLower() == "medium");
            }
            else
            {
                query = query.Where(q => q.Difficulty != null && q.Difficulty.ToLower() == d);
            }
        }

        if (examId.HasValue && examId.Value > 0)
        {
            query = query.Where(q => q.ExamId == examId.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var rawItems = await query
            .OrderBy(q => q.ExamId)
            .ThenBy(q => q.Part)
            .ThenBy(q => q.QuestionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var list = rawItems.Select(MapToQuestionListItemDto).ToList();

        return Result.Success(new PagedResultDto<QuestionListItemDto>(list, totalCount, page, pageSize, totalPages));
    }

    public async Task<Result<QuestionBankDetailDto>> GetQuestionDetailAsync(int id, CancellationToken ct = default)
    {
        var q = await db.Questions.AsNoTracking()
            .Include(x => x.Exam)
                .ThenInclude(e => e.ExamSeries)
            .Include(x => x.Passage)
            .Include(x => x.QuestionGroup)
            .Include(x => x.QuestionOptions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (q == null)
            return Result.Failure<QuestionBankDetailDto>("Question not found.");

        var examStatus = q.Exam == null ? "Draft" : (q.Exam.IsDeleted ? "Hidden" : q.Exam.Status.ToString());
        var isExamActive = q.Exam != null && !q.Exam.IsDeleted && q.Exam.Status == ExamStatus.Published;
        var diff = string.IsNullOrWhiteSpace(q.Difficulty) ? "Medium" : q.Difficulty;

        var embeddedExams = new List<EmbeddedExamDto>();
        if (q.Exam != null)
        {
            embeddedExams.Add(new EmbeddedExamDto(
                q.Exam.Id,
                q.Exam.Title,
                q.Exam.ExamType,
                examStatus,
                isExamActive,
                q.Exam.Duration,
                q.Exam.ExamSeries?.Name
            ));
        }

        if (!string.IsNullOrWhiteSpace(q.QuestionText) && q.QuestionText.Length > 10 && !q.QuestionText.StartsWith("Blank ("))
        {
            var otherExams = await db.Questions.AsNoTracking()
                .Include(x => x.Exam)
                    .ThenInclude(e => e.ExamSeries)
                .Where(x => x.Id != q.Id && x.ExamId != q.ExamId && x.QuestionText == q.QuestionText)
                .Select(x => x.Exam)
                .Distinct()
                .ToListAsync(ct);

            foreach (var oe in otherExams)
            {
                if (oe != null && embeddedExams.All(e => e.ExamId != oe.Id))
                {
                    var st = oe.IsDeleted ? "Hidden" : oe.Status.ToString();
                    var act = !oe.IsDeleted && oe.Status == ExamStatus.Published;
                    embeddedExams.Add(new EmbeddedExamDto(oe.Id, oe.Title, oe.ExamType, st, act, oe.Duration, oe.ExamSeries?.Name));
                }
            }
        }

        var detail = new QuestionBankDetailDto(
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
            diff,
            q.ExamId,
            q.Exam?.Title,
            examStatus,
            isExamActive,
            q.PassageId,
            q.Passage?.Content,
            q.Passage?.ImageUrl,
            q.QuestionGroupId,
            q.QuestionGroup?.Name,
            q.QuestionOptions.OrderBy(o => o.OptionLetter).Select(o => new QuestionOptionDto(o.Id, o.OptionLetter, o.OptionText)).ToList(),
            embeddedExams
        );

        return Result.Success(detail);
    }

    public async Task<Result<int>> CreateQuestionAsync(UpsertQuestionRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Target Exam does not exist.");

        if (dto.Part < 1 || dto.Part > 7)
            return Result.Failure<int>("Part must be between 1 and 7.");

        if (dto.QuestionNumber <= 0)
            return Result.Failure<int>("Question Number must be greater than 0.");

        var duplicateNum = await db.Questions.AnyAsync(q => q.ExamId == dto.ExamId && q.QuestionNumber == dto.QuestionNumber, ct);
        if (duplicateNum)
            return Result.Failure<int>($"Question #{dto.QuestionNumber} already exists in exam '{exam.Title}'.");

        var q = new Question
        {
            ExamId = dto.ExamId,
            Part = dto.Part,
            QuestionNumber = dto.QuestionNumber,
            QuestionText = dto.QuestionText?.Trim(),
            CorrectOption = NormalizeCorrectOption(dto.CorrectOption, dto.Part),
            Explanation = dto.Explanation?.Trim(),
            AudioUrl = dto.AudioUrl?.Trim(),
            AudioStartSecond = dto.AudioStartSecond,
            AudioEndSecond = dto.AudioEndSecond,
            ImageUrl = dto.ImageUrl?.Trim(),
            QuestionType = dto.QuestionType?.Trim(),
            TopicTag = dto.TopicTag?.Trim(),
            Difficulty = NormalizeDifficulty(dto.Difficulty),
            PassageId = dto.PassageId,
            QuestionGroupId = dto.QuestionGroupId,
            QuestionOptions = BuildOptionsList(dto.Part, dto.OptionA, dto.OptionB, dto.OptionC, dto.OptionD)
        };

        db.Questions.Add(q);
        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(q.Id);
    }

    public async Task<Result> UpdateQuestionAsync(int id, UpsertQuestionRequestDto dto, CancellationToken ct = default)
    {
        var q = await db.Questions
            .Include(x => x.QuestionOptions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (q == null)
            return Result.Failure("Question not found.");

        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure("Target Exam does not exist.");

        var duplicateNum = await db.Questions.AnyAsync(x => x.ExamId == dto.ExamId && x.QuestionNumber == dto.QuestionNumber && x.Id != id, ct);
        if (duplicateNum)
            return Result.Failure($"Question #{dto.QuestionNumber} already exists in exam '{exam.Title}'.");

        q.ExamId = dto.ExamId;
        q.Part = dto.Part;
        q.QuestionNumber = dto.QuestionNumber;
        q.QuestionText = dto.QuestionText?.Trim();
        q.CorrectOption = NormalizeCorrectOption(dto.CorrectOption, dto.Part);
        q.Explanation = dto.Explanation?.Trim();
        q.AudioUrl = dto.AudioUrl?.Trim();
        q.AudioStartSecond = dto.AudioStartSecond;
        q.AudioEndSecond = dto.AudioEndSecond;
        q.ImageUrl = dto.ImageUrl?.Trim();
        q.QuestionType = dto.QuestionType?.Trim();
        q.TopicTag = dto.TopicTag?.Trim();
        q.Difficulty = NormalizeDifficulty(dto.Difficulty);
        if (dto.PassageId.HasValue) q.PassageId = dto.PassageId;
        if (dto.QuestionGroupId.HasValue) q.QuestionGroupId = dto.QuestionGroupId;

        db.QuestionOptions.RemoveRange(q.QuestionOptions);
        q.QuestionOptions = BuildOptionsList(dto.Part, dto.OptionA, dto.OptionB, dto.OptionC, dto.OptionD);

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<DeleteQuestionResultDto>> DeleteQuestionAsync(int id, CancellationToken ct = default)
    {
        var q = await db.Questions
            .Include(x => x.Exam)
            .Include(x => x.QuestionOptions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (q == null)
            return Result.Failure<DeleteQuestionResultDto>("Question not found.");

        var wasActive = q.Exam != null && !q.Exam.IsDeleted && q.Exam.Status == ExamStatus.Published;
        var examTitle = q.Exam?.Title;

        db.QuestionOptions.RemoveRange(q.QuestionOptions);
        db.Questions.Remove(q);

        if (q.Exam != null)
        {
            q.Exam.LastModifiedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var msg = wasActive
            ? $"Question #{q.QuestionNumber} (Part {q.Part}) was deleted from active exam '{examTitle}'."
            : $"Question #{q.QuestionNumber} (Part {q.Part}) was deleted successfully.";

        return Result.Success(new DeleteQuestionResultDto(msg, wasActive, examTitle));
    }

    public async Task<Result<int>> SaveListeningQuestionAsync(SaveListeningQuestionRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Exam not found.");

        var duplicate = await db.Questions.AnyAsync(q =>
            q.ExamId == dto.ExamId &&
            q.QuestionNumber == dto.QuestionNumber &&
            (!dto.QuestionId.HasValue || q.Id != dto.QuestionId.Value), ct);

        if (duplicate)
            return Result.Failure<int>($"Question #{dto.QuestionNumber} already exists in this exam.");

        Question q;
        if (dto.QuestionId.HasValue && dto.QuestionId.Value > 0)
        {
            var existing = await db.Questions
                .Include(x => x.QuestionOptions)
                .FirstOrDefaultAsync(x => x.Id == dto.QuestionId.Value && x.ExamId == dto.ExamId, ct);
            if (existing == null)
                return Result.Failure<int>("Question to edit was not found.");

            q = existing;
            db.QuestionOptions.RemoveRange(q.QuestionOptions);
        }
        else
        {
            q = new Question { ExamId = dto.ExamId };
            db.Questions.Add(q);
        }

        q.Part = dto.Part;
        q.QuestionNumber = dto.QuestionNumber;
        q.QuestionText = dto.QuestionText?.Trim();
        q.CorrectOption = NormalizeCorrectOption(dto.CorrectOption, dto.Part);
        q.Explanation = dto.Explanation?.Trim();
        q.AudioUrl = dto.AudioUrl?.Trim();
        q.AudioStartSecond = dto.AudioStartSecond;
        q.AudioEndSecond = dto.AudioEndSecond;
        q.ImageUrl = dto.ImageUrl?.Trim();
        q.Difficulty = NormalizeDifficulty(dto.Difficulty);
        q.QuestionType = dto.Part == 1 ? "Photograph" : "Question-Response";
        q.QuestionOptions = BuildOptionsList(dto.Part, dto.OptionA, dto.OptionB, dto.OptionC, dto.OptionD);

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(q.Id);
    }

    public async Task<Result<int>> SaveListeningGroupAsync(SaveListeningGroupRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Exam not found.");

        if (dto.Questions == null || dto.Questions.Count == 0)
            return Result.Failure<int>("At least one question is required in the listening group.");

        var qNums = dto.Questions.Select(x => x.QuestionNumber).ToList();
        if (qNums.Distinct().Count() != qNums.Count)
            return Result.Failure<int>("Question numbers inside the group must be unique.");

        QuestionGroup group;
        if (dto.GroupId.HasValue && dto.GroupId.Value > 0)
        {
            var existingGroup = await db.QuestionGroups
                .Include(g => g.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(g => g.Id == dto.GroupId.Value && g.ExamId == dto.ExamId, ct);

            if (existingGroup == null)
                return Result.Failure<int>("Question group not found.");

            group = existingGroup;
            var existingIds = group.Questions.Select(q => q.Id).ToList();
            var clash = await db.Questions.AnyAsync(q =>
                q.ExamId == dto.ExamId &&
                qNums.Contains(q.QuestionNumber) &&
                !existingIds.Contains(q.Id), ct);
            if (clash)
                return Result.Failure<int>("One or more question numbers already exist in this exam.");

            foreach (var oldQ in group.Questions)
            {
                db.QuestionOptions.RemoveRange(oldQ.QuestionOptions);
            }
            db.Questions.RemoveRange(group.Questions);
        }
        else
        {
            var clash = await db.Questions.AnyAsync(q =>
                q.ExamId == dto.ExamId &&
                qNums.Contains(q.QuestionNumber), ct);
            if (clash)
                return Result.Failure<int>("One or more question numbers already exist in this exam.");

            group = new QuestionGroup { ExamId = dto.ExamId };
            db.QuestionGroups.Add(group);
        }

        group.Name = string.IsNullOrWhiteSpace(dto.Name) ? $"Part {dto.Part} Listening Set" : dto.Name.Trim();
        group.AudioUrl = dto.AudioUrl?.Trim();
        group.AudioStartSecond = dto.AudioStartSecond;
        group.AudioEndSecond = dto.AudioEndSecond;
        group.ImageUrl = dto.ImageUrl?.Trim();
        group.Explanation = dto.Explanation?.Trim();

        await db.SaveChangesAsync(ct);

        foreach (var item in dto.Questions)
        {
            var newQ = new Question
            {
                ExamId = dto.ExamId,
                Part = dto.Part,
                QuestionGroupId = group.Id,
                QuestionNumber = item.QuestionNumber,
                QuestionText = item.QuestionText?.Trim(),
                CorrectOption = NormalizeCorrectOption(item.CorrectOption, dto.Part),
                Explanation = string.IsNullOrWhiteSpace(item.Explanation) ? group.Explanation : item.Explanation.Trim(),
                AudioUrl = group.AudioUrl,
                AudioStartSecond = group.AudioStartSecond,
                AudioEndSecond = group.AudioEndSecond,
                ImageUrl = group.ImageUrl,
                Difficulty = NormalizeDifficulty(item.Difficulty),
                QuestionType = dto.Part == 3 ? "Short Conversation" : "Short Talk",
                QuestionOptions = BuildOptionsList(dto.Part, item.OptionA, item.OptionB, item.OptionC, item.OptionD)
            };
            db.Questions.Add(newQ);
        }

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }

    public async Task<Result<int>> SavePart5QuestionAsync(SavePart5QuestionRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Exam not found.");

        var duplicate = await db.Questions.AnyAsync(q =>
            q.ExamId == dto.ExamId &&
            q.QuestionNumber == dto.QuestionNumber &&
            (!dto.QuestionId.HasValue || q.Id != dto.QuestionId.Value), ct);

        if (duplicate)
            return Result.Failure<int>($"Question #{dto.QuestionNumber} already exists in this exam.");

        Question q;
        if (dto.QuestionId.HasValue && dto.QuestionId.Value > 0)
        {
            var existing = await db.Questions
                .Include(x => x.QuestionOptions)
                .FirstOrDefaultAsync(x => x.Id == dto.QuestionId.Value && x.ExamId == dto.ExamId, ct);
            if (existing == null)
                return Result.Failure<int>("Part 5 question not found.");

            q = existing;
            db.QuestionOptions.RemoveRange(q.QuestionOptions);
        }
        else
        {
            q = new Question { ExamId = dto.ExamId };
            db.Questions.Add(q);
        }

        q.Part = 5;
        q.QuestionNumber = dto.QuestionNumber;
        q.QuestionText = dto.QuestionText.Trim();
        q.CorrectOption = NormalizeCorrectOption(dto.CorrectOption, 5);
        q.Explanation = dto.Explanation?.Trim();
        q.QuestionType = dto.QuestionType?.Trim();
        q.TopicTag = dto.TopicTag?.Trim();
        q.Difficulty = NormalizeDifficulty(dto.Difficulty);
        q.QuestionOptions = BuildOptionsList(5, dto.OptionA, dto.OptionB, dto.OptionC, dto.OptionD);

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(q.Id);
    }

    public async Task<Result<int>> SavePart6SetAsync(SavePart6SetRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Exam not found.");

        if (dto.Questions == null || dto.Questions.Count == 0)
            return Result.Failure<int>("Part 6 passage requires questions.");

        var qNums = dto.Questions.Select(x => x.QuestionNumber).ToList();
        if (qNums.Distinct().Count() != qNums.Count)
            return Result.Failure<int>("Question numbers in Part 6 passage must be unique.");

        Passage passage;
        if (dto.PassageId.HasValue && dto.PassageId.Value > 0)
        {
            var existingPassage = await db.Passages
                .Include(p => p.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(p => p.Id == dto.PassageId.Value && p.ExamId == dto.ExamId, ct);

            if (existingPassage == null)
                return Result.Failure<int>("Part 6 passage not found.");

            passage = existingPassage;
            var existingQIds = passage.Questions.Select(q => q.Id).ToList();
            var clash = await db.Questions.AnyAsync(q =>
                q.ExamId == dto.ExamId &&
                qNums.Contains(q.QuestionNumber) &&
                !existingQIds.Contains(q.Id), ct);

            if (clash)
                return Result.Failure<int>("One or more question numbers already exist in this exam.");

            foreach (var oldQ in passage.Questions)
            {
                db.QuestionOptions.RemoveRange(oldQ.QuestionOptions);
            }
            db.Questions.RemoveRange(passage.Questions);
        }
        else
        {
            var clash = await db.Questions.AnyAsync(q =>
                q.ExamId == dto.ExamId &&
                qNums.Contains(q.QuestionNumber), ct);
            if (clash)
                return Result.Failure<int>("One or more question numbers already exist in this exam.");

            passage = new Passage { ExamId = dto.ExamId };
            db.Passages.Add(passage);
        }

        passage.Content = dto.Content?.Trim();
        passage.ImageUrl = dto.ImageUrl?.Trim();
        passage.Explanation = dto.Explanation?.Trim();
        passage.DisplayOrder = dto.DisplayOrder > 0 ? dto.DisplayOrder : 1;

        await db.SaveChangesAsync(ct);

        foreach (var item in dto.Questions)
        {
            var newQ = new Question
            {
                ExamId = dto.ExamId,
                Part = 6,
                PassageId = passage.Id,
                QuestionNumber = item.QuestionNumber,
                QuestionText = string.IsNullOrWhiteSpace(item.QuestionText) ? $"Blank ({item.QuestionNumber})" : item.QuestionText.Trim(),
                CorrectOption = NormalizeCorrectOption(item.CorrectOption, 6),
                Explanation = string.IsNullOrWhiteSpace(item.Explanation) ? passage.Explanation : item.Explanation.Trim(),
                Difficulty = NormalizeDifficulty(item.Difficulty),
                QuestionType = "Text Completion",
                QuestionOptions = BuildOptionsList(6, item.OptionA, item.OptionB, item.OptionC, item.OptionD)
            };
            db.Questions.Add(newQ);
        }

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(passage.Id);
    }

    public async Task<Result<int>> SavePart7SetAsync(SavePart7SetRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Exam not found.");

        if (dto.Passages == null || dto.Passages.Count == 0)
            return Result.Failure<int>("At least one reading passage is required for Part 7.");

        if (dto.Questions == null || dto.Questions.Count == 0)
            return Result.Failure<int>("Questions are required for Part 7 reading set.");

        var qNums = dto.Questions.Select(x => x.QuestionNumber).ToList();
        if (qNums.Distinct().Count() != qNums.Count)
            return Result.Failure<int>("Question numbers in Part 7 reading set must be unique.");

        QuestionGroup group;
        if (dto.GroupId.HasValue && dto.GroupId.Value > 0)
        {
            var existingGroup = await db.QuestionGroups
                .Include(g => g.Passages)
                .Include(g => g.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(g => g.Id == dto.GroupId.Value && g.ExamId == dto.ExamId, ct);

            if (existingGroup == null)
                return Result.Failure<int>("Part 7 reading set not found.");

            group = existingGroup;
            var existingQIds = group.Questions.Select(q => q.Id).ToList();
            var clash = await db.Questions.AnyAsync(q =>
                q.ExamId == dto.ExamId &&
                qNums.Contains(q.QuestionNumber) &&
                !existingQIds.Contains(q.Id), ct);

            if (clash)
                return Result.Failure<int>("One or more question numbers already exist in this exam.");

            foreach (var oldQ in group.Questions)
            {
                db.QuestionOptions.RemoveRange(oldQ.QuestionOptions);
            }
            db.Questions.RemoveRange(group.Questions);
            db.Passages.RemoveRange(group.Passages);
        }
        else
        {
            var clash = await db.Questions.AnyAsync(q =>
                q.ExamId == dto.ExamId &&
                qNums.Contains(q.QuestionNumber), ct);
            if (clash)
                return Result.Failure<int>("One or more question numbers already exist in this exam.");

            group = new QuestionGroup { ExamId = dto.ExamId };
            db.QuestionGroups.Add(group);
        }

        group.Name = string.IsNullOrWhiteSpace(dto.Name) ? "Part 7 Reading Set" : dto.Name.Trim();
        group.Explanation = dto.Explanation?.Trim();
        await db.SaveChangesAsync(ct);

        Passage? firstPassage = null;
        int order = 1;
        foreach (var pItem in dto.Passages)
        {
            var p = new Passage
            {
                ExamId = dto.ExamId,
                QuestionGroupId = group.Id,
                Content = pItem.Content?.Trim(),
                ImageUrl = pItem.ImageUrl?.Trim(),
                Explanation = string.IsNullOrWhiteSpace(pItem.Explanation) ? group.Explanation : pItem.Explanation.Trim(),
                DisplayOrder = pItem.DisplayOrder > 0 ? pItem.DisplayOrder : order++
            };
            db.Passages.Add(p);
            await db.SaveChangesAsync(ct);
            firstPassage ??= p;
        }

        foreach (var item in dto.Questions)
        {
            var newQ = new Question
            {
                ExamId = dto.ExamId,
                Part = 7,
                QuestionGroupId = group.Id,
                PassageId = firstPassage?.Id,
                QuestionNumber = item.QuestionNumber,
                QuestionText = item.QuestionText.Trim(),
                CorrectOption = NormalizeCorrectOption(item.CorrectOption, 7),
                Explanation = string.IsNullOrWhiteSpace(item.Explanation) ? group.Explanation : item.Explanation.Trim(),
                Difficulty = NormalizeDifficulty(item.Difficulty),
                QuestionType = "Reading Comprehension",
                QuestionOptions = BuildOptionsList(7, item.OptionA, item.OptionB, item.OptionC, item.OptionD)
            };
            db.Questions.Add(newQ);
        }

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(group.Id);
    }

    public async Task<Result<DeleteQuestionResultDto>> DeleteExamContentAsync(
        int examId,
        string contentType,
        int targetId,
        CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId, ct);
        if (exam == null)
            return Result.Failure<DeleteQuestionResultDto>("Exam not found.");

        var wasActive = !exam.IsDeleted && exam.Status == ExamStatus.Published;
        var normType = (contentType ?? "").Trim().ToLowerInvariant();

        if (normType == "question")
        {
            var q = await db.Questions
                .Include(x => x.QuestionOptions)
                .FirstOrDefaultAsync(x => x.Id == targetId && x.ExamId == examId, ct);
            if (q == null)
                return Result.Failure<DeleteQuestionResultDto>("Question not found in this exam.");

            db.QuestionOptions.RemoveRange(q.QuestionOptions);
            db.Questions.Remove(q);
            exam.LastModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Result.Success(new DeleteQuestionResultDto(
                $"Question #{q.QuestionNumber} deleted successfully.",
                wasActive,
                exam.Title));
        }
        else if (normType == "group" || normType == "questiongroup" || normType == "listeninggroup" || normType == "readingset")
        {
            var group = await db.QuestionGroups
                .Include(g => g.Passages)
                .Include(g => g.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(g => g.Id == targetId && g.ExamId == examId, ct);

            if (group == null)
                return Result.Failure<DeleteQuestionResultDto>("Question group not found in this exam.");

            foreach (var q in group.Questions)
            {
                db.QuestionOptions.RemoveRange(q.QuestionOptions);
            }
            db.Questions.RemoveRange(group.Questions);
            db.Passages.RemoveRange(group.Passages);
            db.QuestionGroups.Remove(group);
            exam.LastModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Result.Success(new DeleteQuestionResultDto(
                $"Group '{group.Name}' and its questions were deleted.",
                wasActive,
                exam.Title));
        }
        else if (normType == "passage" || normType == "textcompletion")
        {
            var passage = await db.Passages
                .Include(p => p.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(p => p.Id == targetId && p.ExamId == examId, ct);

            if (passage == null)
                return Result.Failure<DeleteQuestionResultDto>("Passage not found in this exam.");

            foreach (var q in passage.Questions)
            {
                db.QuestionOptions.RemoveRange(q.QuestionOptions);
            }
            db.Questions.RemoveRange(passage.Questions);
            db.Passages.Remove(passage);
            exam.LastModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Result.Success(new DeleteQuestionResultDto(
                $"Passage #{passage.Id} and its questions were deleted.",
                wasActive,
                exam.Title));
        }

        return Result.Failure<DeleteQuestionResultDto>($"Unsupported content type: {contentType}");
    }

    public async Task<Result<ExamStatsSummaryDto>> GetExamStatsAsync(CancellationToken ct = default)
    {
        var totalExams = await db.Exams.CountAsync(ct);
        var activeExams = await db.Exams.CountAsync(e => !e.IsDeleted && e.Status == ExamStatus.Published, ct);
        var hiddenExams = await db.Exams.CountAsync(e => e.IsDeleted || e.Status == ExamStatus.Hidden, ct);

        var examIds = await db.Exams.Select(e => e.Id).ToListAsync(ct);
        var attemptTasks = examIds.Select(id => attemptChecker.GetAttemptCountAsync(id, ct));
        var attemptCounts = await Task.WhenAll(attemptTasks);
        var totalAttempts = attemptCounts.Sum();

        return Result.Success(new ExamStatsSummaryDto(totalExams, activeExams, hiddenExams, totalAttempts));
    }

    public async Task<Result<QuestionStatsSummaryDto>> GetQuestionStatsAsync(CancellationToken ct = default)
    {
        var total = await db.Questions.CountAsync(ct);
        var listening = await db.Questions.CountAsync(q => q.Part <= 4, ct);
        var reading = await db.Questions.CountAsync(q => q.Part >= 5, ct);
        var active = await db.Questions.CountAsync(q => q.Exam != null && !q.Exam.IsDeleted && q.Exam.Status == ExamStatus.Published, ct);

        return Result.Success(new QuestionStatsSummaryDto(total, listening, reading, active));
    }

    // ==========================================
    // READING PASSAGES CMS (UC-18.1 - UC-18.5)
    // ==========================================

    public async Task<Result<PagedResultDto<PassageListItemDto>>> GetPagedPassagesAsync(
        string? keyword,
        int? examId,
        int? part,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var query = db.Passages.AsNoTracking()
            .Include(p => p.Exam)
            .Include(p => p.QuestionGroup)
            .Include(p => p.Questions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(p =>
                (p.Content != null && p.Content.ToLower().Contains(k)) ||
                (p.Explanation != null && p.Explanation.ToLower().Contains(k)) ||
                (p.Exam != null && p.Exam.Title.ToLower().Contains(k)) ||
                (p.QuestionGroup != null && p.QuestionGroup.Name.ToLower().Contains(k)));
        }

        if (examId.HasValue && examId.Value > 0)
        {
            query = query.Where(p => p.ExamId == examId.Value);
        }

        if (part.HasValue && (part.Value == 6 || part.Value == 7))
        {
            if (part.Value == 6)
            {
                query = query.Where(p => p.QuestionGroupId == null || p.Questions.Any(q => q.Part == 6));
            }
            else
            {
                query = query.Where(p => p.QuestionGroupId != null || p.Questions.Any(q => q.Part == 7));
            }
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var rawItems = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rawItems.Select(p =>
        {
            var examStatus = p.Exam == null ? "Draft" : (p.Exam.IsDeleted ? "Hidden" : p.Exam.Status.ToString());
            var isExamActive = p.Exam != null && !p.Exam.IsDeleted && p.Exam.Status == ExamStatus.Published;
            var inferredPart = p.Questions.Any(q => q.Part == 6) ? 6 : (p.QuestionGroupId.HasValue || p.Questions.Any(q => q.Part == 7) ? 7 : 6);

            return new PassageListItemDto(
                p.Id,
                p.ExamId,
                p.Exam?.Title,
                examStatus,
                isExamActive,
                inferredPart,
                p.Content,
                p.ImageUrl,
                p.Explanation,
                p.DisplayOrder,
                p.QuestionGroupId,
                p.QuestionGroup?.Name,
                p.Questions.Count
            );
        }).ToList();

        return Result.Success(new PagedResultDto<PassageListItemDto>(items, totalCount, page, pageSize, totalPages));
    }

    public async Task<Result<PassageStatsSummaryDto>> GetPassageStatsAsync(CancellationToken ct = default)
    {
        var total = await db.Passages.CountAsync(ct);
        var part7 = await db.Passages.CountAsync(p => p.QuestionGroupId != null || p.Questions.Any(q => q.Part == 7), ct);
        var part6 = Math.Max(0, total - part7);
        var active = await db.Passages.CountAsync(p => p.Exam != null && !p.Exam.IsDeleted && p.Exam.Status == ExamStatus.Published, ct);

        return Result.Success(new PassageStatsSummaryDto(total, part6, part7, active));
    }

    public async Task<Result<PassageCmsDetailDto>> GetPassageDetailAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Passages.AsNoTracking()
            .Include(x => x.Exam)
            .Include(x => x.QuestionGroup)
            .Include(x => x.Questions)
                .ThenInclude(q => q.QuestionOptions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p == null)
            return Result.Failure<PassageCmsDetailDto>("Reading passage not found.");

        var examStatus = p.Exam == null ? "Draft" : (p.Exam.IsDeleted ? "Hidden" : p.Exam.Status.ToString());
        var isExamActive = p.Exam != null && !p.Exam.IsDeleted && p.Exam.Status == ExamStatus.Published;
        var inferredPart = p.Questions.Any(q => q.Part == 6) ? 6 : (p.QuestionGroupId.HasValue || p.Questions.Any(q => q.Part == 7) ? 7 : 6);

        var questionDtos = p.Questions
            .OrderBy(q => q.QuestionNumber)
            .Select(q =>
            {
                q.Exam = p.Exam!;
                q.QuestionGroup = p.QuestionGroup;
                return MapToQuestionListItemDto(q);
            })
            .ToList();

        var detail = new PassageCmsDetailDto(
            p.Id,
            p.ExamId,
            p.Exam?.Title,
            examStatus,
            isExamActive,
            inferredPart,
            p.Content,
            p.ImageUrl,
            p.Explanation,
            p.DisplayOrder,
            p.QuestionGroupId,
            p.QuestionGroup?.Name,
            questionDtos
        );

        return Result.Success(detail);
    }

    public async Task<Result<int>> CreatePassageAsync(UpsertPassageRequestDto dto, CancellationToken ct = default)
    {
        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure<int>("Target Exam does not exist.");

        if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.ImageUrl))
            return Result.Failure<int>("Passage content or image URL is required.");

        if (dto.QuestionGroupId.HasValue && dto.QuestionGroupId.Value > 0)
        {
            var groupExists = await db.QuestionGroups.AnyAsync(g => g.Id == dto.QuestionGroupId.Value && g.ExamId == dto.ExamId, ct);
            if (!groupExists)
                return Result.Failure<int>("Selected Question Group does not exist in this exam.");
        }

        var passage = new Passage
        {
            ExamId = dto.ExamId,
            Content = dto.Content?.Trim(),
            ImageUrl = dto.ImageUrl?.Trim(),
            Explanation = dto.Explanation?.Trim(),
            DisplayOrder = dto.DisplayOrder > 0 ? dto.DisplayOrder : 1,
            QuestionGroupId = dto.QuestionGroupId.HasValue && dto.QuestionGroupId.Value > 0 ? dto.QuestionGroupId.Value : null
        };

        db.Passages.Add(passage);
        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success(passage.Id);
    }

    public async Task<Result> UpdatePassageAsync(int id, UpsertPassageRequestDto dto, CancellationToken ct = default)
    {
        var passage = await db.Passages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (passage == null)
            return Result.Failure("Reading passage not found.");

        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == dto.ExamId, ct);
        if (exam == null)
            return Result.Failure("Target Exam does not exist.");

        if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.ImageUrl))
            return Result.Failure("Passage content or image URL is required.");

        passage.ExamId = dto.ExamId;
        passage.Content = dto.Content?.Trim();
        passage.ImageUrl = dto.ImageUrl?.Trim();
        passage.Explanation = dto.Explanation?.Trim();
        passage.DisplayOrder = dto.DisplayOrder > 0 ? dto.DisplayOrder : 1;
        passage.QuestionGroupId = dto.QuestionGroupId.HasValue && dto.QuestionGroupId.Value > 0 ? dto.QuestionGroupId.Value : null;

        exam.LastModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<DeletePassageResultDto>> DeletePassageAsync(int id, bool deleteWithQuestions = false, CancellationToken ct = default)
    {
        var passage = await db.Passages
            .Include(p => p.Exam)
            .Include(p => p.Questions)
                .ThenInclude(q => q.QuestionOptions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (passage == null)
            return Result.Failure<DeletePassageResultDto>("Reading passage not found.");

        var questionCount = passage.Questions.Count;
        // UC-18.5 Constraint: Do not allow Part 6-7 questions to become orphan!
        if (questionCount > 0 && !deleteWithQuestions)
        {
            return Result.Failure<DeletePassageResultDto>(
                $"Cannot delete passage #{passage.Id} because it has {questionCount} attached Part 6-7 question(s). Deleting the passage alone would leave orphan questions. Please remove its questions first or confirm 'Delete Passage & Attached Questions'.");
        }

        var wasActive = passage.Exam != null && !passage.Exam.IsDeleted && passage.Exam.Status == ExamStatus.Published;
        var examTitle = passage.Exam?.Title;

        if (questionCount > 0)
        {
            foreach (var q in passage.Questions)
            {
                db.QuestionOptions.RemoveRange(q.QuestionOptions);
            }
            db.Questions.RemoveRange(passage.Questions);
        }

        db.Passages.Remove(passage);

        if (passage.Exam != null)
        {
            passage.Exam.LastModifiedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        var msg = questionCount > 0
            ? $"Passage #{id} and its {questionCount} attached Part 6-7 question(s) were deleted (no orphan questions left)."
            : $"Passage #{id} was deleted successfully.";

        return Result.Success(new DeletePassageResultDto(msg, questionCount, wasActive, examTitle));
    }

    private static QuestionListItemDto MapToQuestionListItemDto(Question q)
    {
        var examStatus = q.Exam == null ? "Draft" : (q.Exam.IsDeleted ? "Hidden" : q.Exam.Status.ToString());
        var isExamActive = q.Exam != null && !q.Exam.IsDeleted && q.Exam.Status == ExamStatus.Published;
        var diff = string.IsNullOrWhiteSpace(q.Difficulty) ? "Medium" : q.Difficulty;

        return new QuestionListItemDto(
            q.Id,
            q.QuestionNumber,
            q.Part,
            q.QuestionText,
            q.CorrectOption,
            q.Explanation,
            q.AudioUrl,
            q.ImageUrl,
            q.QuestionType,
            q.TopicTag,
            diff,
            q.ExamId,
            q.Exam?.Title,
            examStatus,
            isExamActive,
            q.PassageId,
            q.QuestionGroupId,
            q.QuestionGroup?.Name,
            q.QuestionOptions.Count,
            q.QuestionOptions.OrderBy(o => o.OptionLetter).Select(o => new QuestionOptionDto(o.Id, o.OptionLetter, o.OptionText)).ToList()
        );
    }

    private static string NormalizeCorrectOption(string? opt, int part)
    {
        var norm = (opt ?? "A").Trim().ToUpperInvariant();
        if (part == 2)
        {
            return norm is "A" or "B" or "C" ? norm : "A";
        }
        return norm is "A" or "B" or "C" or "D" ? norm : "A";
    }

    private static string NormalizeDifficulty(string? difficulty)
    {
        var norm = (difficulty ?? "Medium").Trim();
        if (norm.Equals("Easy", StringComparison.OrdinalIgnoreCase)) return "Easy";
        if (norm.Equals("Hard", StringComparison.OrdinalIgnoreCase)) return "Hard";
        return "Medium";
    }

    private static List<QuestionOption> BuildOptionsList(int part, string? optA, string? optB, string? optC, string? optD)
    {
        var list = new List<QuestionOption>
        {
            new() { OptionLetter = "A", OptionText = string.IsNullOrWhiteSpace(optA) ? "Option A" : optA.Trim() },
            new() { OptionLetter = "B", OptionText = string.IsNullOrWhiteSpace(optB) ? "Option B" : optB.Trim() },
            new() { OptionLetter = "C", OptionText = string.IsNullOrWhiteSpace(optC) ? "Option C" : optC.Trim() }
        };

        if (part != 2)
        {
            list.Add(new QuestionOption
            {
                OptionLetter = "D",
                OptionText = string.IsNullOrWhiteSpace(optD) ? "Option D" : optD.Trim()
            });
        }

        return list;
    }
}
