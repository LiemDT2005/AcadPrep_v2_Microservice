using BuildingBlocks.Common.Results;
using Content.DTOs;

namespace Content.Services.Interfaces;

public interface IContentService
{
    Task<Result<PagedResultDto<ExamListItemDto>>> GetPagedExamsAsync(
        string? keyword,
        string? status,
        string? examType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<ExamDetailDto>> GetExamByIdAsync(int id, CancellationToken ct = default);

    Task<Result<ExamDetailDto>> CreateExamAsync(CreateExamDto dto, CancellationToken ct = default);

    Task<Result> UpdateExamAsync(int id, UpdateExamDto dto, CancellationToken ct = default);

    Task<Result> ChangeStatusAsync(int id, string status, CancellationToken ct = default);

    Task<Result<DeleteExamResultDto>> DeleteExamAsync(int id, CancellationToken ct = default);

    Task<Result> RestoreExamAsync(int id, CancellationToken ct = default);

    Task<Result<List<ExamSeriesDto>>> GetExamSeriesAsync(CancellationToken ct = default);

    // ==========================================
    // QUESTION BANK & EXAM CONTENT (UC-17.1 - UC-17.7)
    // ==========================================

    Task<Result<PagedResultDto<QuestionListItemDto>>> GetPagedQuestionsAsync(
        string? keyword,
        int? part,
        string? difficulty,
        int? examId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<QuestionBankDetailDto>> GetQuestionDetailAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateQuestionAsync(UpsertQuestionRequestDto dto, CancellationToken ct = default);

    Task<Result> UpdateQuestionAsync(int id, UpsertQuestionRequestDto dto, CancellationToken ct = default);

    Task<Result<DeleteQuestionResultDto>> DeleteQuestionAsync(int id, CancellationToken ct = default);

    Task<Result<int>> SaveListeningQuestionAsync(SaveListeningQuestionRequestDto dto, CancellationToken ct = default);

    Task<Result<int>> SaveListeningGroupAsync(SaveListeningGroupRequestDto dto, CancellationToken ct = default);

    Task<Result<int>> SavePart5QuestionAsync(SavePart5QuestionRequestDto dto, CancellationToken ct = default);

    Task<Result<int>> SavePart6SetAsync(SavePart6SetRequestDto dto, CancellationToken ct = default);

    Task<Result<int>> SavePart7SetAsync(SavePart7SetRequestDto dto, CancellationToken ct = default);

    Task<Result<ExamStatsSummaryDto>> GetExamStatsAsync(CancellationToken ct = default);

    Task<Result<QuestionStatsSummaryDto>> GetQuestionStatsAsync(CancellationToken ct = default);

    Task<Result<DeleteQuestionResultDto>> DeleteExamContentAsync(int examId, string contentType, int targetId, CancellationToken ct = default);

    // ==========================================
    // READING PASSAGES CMS (UC-18.1 - UC-18.5)
    // ==========================================

    Task<Result<PagedResultDto<PassageListItemDto>>> GetPagedPassagesAsync(
        string? keyword,
        int? examId,
        int? part,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<PassageStatsSummaryDto>> GetPassageStatsAsync(CancellationToken ct = default);

    Task<Result<PassageCmsDetailDto>> GetPassageDetailAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreatePassageAsync(UpsertPassageRequestDto dto, CancellationToken ct = default);

    Task<Result> UpdatePassageAsync(int id, UpsertPassageRequestDto dto, CancellationToken ct = default);

    Task<Result<DeletePassageResultDto>> DeletePassageAsync(int id, bool deleteWithQuestions = false, CancellationToken ct = default);
}
