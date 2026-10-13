using BuildingBlocks.Common.Results;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Services;

public class VocabularyService(ILearningDbContext db, GamificationService gamificationService)
{
    // UC-11.1 Review Flashcards
    public async Task<Result<PaginatedListDto<SavedVocabularyDto>>> GetReviewFlashcardsAsync(
        string userId, int pageNumber = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = db.SavedVocabularies.AsNoTracking()
            .Include(sv => sv.Vocabulary)
            .Where(sv => sv.UserId == userId && sv.NextReviewDate <= now && sv.Vocabulary != null);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(sv => sv.NextReviewDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(sv => new SavedVocabularyDto(
                sv.VocabularyId,
                sv.Vocabulary!.Word,
                sv.Vocabulary.Phonetic,
                sv.Vocabulary.Meaning,
                sv.Vocabulary.Example,
                sv.Interval,
                sv.NextReviewDate,
                sv.DateSaved
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedListDto<SavedVocabularyDto>(
            items, pageNumber, totalPages, totalCount, pageNumber > 1, pageNumber < totalPages
        ));
    }

    // UC-11.2 Rate Memorization (Spaced Repetition SRS Algorithm)
    public async Task<Result<SavedVocabularyDto>> RateMemorizationAsync(
        string userId, RateVocabularyRequest request, CancellationToken ct = default)
    {
        var saved = await db.SavedVocabularies
            .Include(sv => sv.Vocabulary)
            .FirstOrDefaultAsync(sv => sv.UserId == userId && sv.VocabularyId == request.VocabularyId, ct);

        if (saved is null)
        {
            return Result.Failure<SavedVocabularyDto>("Từ vựng không tồn tại trong sổ tay của bạn.");
        }

        // Spaced Repetition Algorithm
        if (request.IsRemembered)
        {
            saved.Interval = Math.Max(2, saved.Interval * 2);
        }
        else
        {
            saved.Interval = 1;
        }

        saved.NextReviewDate = DateTime.UtcNow.Date.AddDays(saved.Interval);
        await db.SaveChangesAsync(ct);

        var dto = new SavedVocabularyDto(
            saved.VocabularyId,
            saved.Vocabulary?.Word ?? string.Empty,
            saved.Vocabulary?.Phonetic,
            saved.Vocabulary?.Meaning ?? string.Empty,
            saved.Vocabulary?.Example,
            saved.Interval,
            saved.NextReviewDate,
            saved.DateSaved
        );

        return Result.Success(dto);
    }

    // UC-11.3 Save Vocabulary
    public async Task<Result<SavedVocabularyDto>> SaveVocabularyAsync(
        string userId, SaveVocabularyRequest request, CancellationToken ct = default)
    {
        var vocab = await db.Vocabularies.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VocabularyId, ct);

        if (vocab is null)
        {
            return Result.Failure<SavedVocabularyDto>("Từ vựng không tồn tại trong từ điển.");
        }

        var exists = await db.SavedVocabularies.AnyAsync(
            sv => sv.UserId == userId && sv.VocabularyId == request.VocabularyId, ct);

        if (exists)
        {
            return Result.Failure<SavedVocabularyDto>("Từ vựng này đã có trong sổ tay cá nhân.");
        }

        var entity = new SavedVocabulary
        {
            UserId = userId,
            VocabularyId = request.VocabularyId,
            Interval = 1,
            NextReviewDate = DateTime.UtcNow.Date.AddDays(1),
            DateSaved = DateTime.UtcNow
        };

        db.SavedVocabularies.Add(entity);
        await db.SaveChangesAsync(ct);

        // Check and grant achievements (e.g. Word Collector)
        await gamificationService.CheckAndGrantAchievementsAsync(userId, ct);

        var dto = new SavedVocabularyDto(
            vocab.Id,
            vocab.Word,
            vocab.Phonetic,
            vocab.Meaning,
            vocab.Example,
            entity.Interval,
            entity.NextReviewDate,
            entity.DateSaved
        );

        return Result.Success(dto);
    }

    // UC-11.4 View Vocabulary List
    public async Task<Result<PaginatedListDto<SavedVocabularyDto>>> GetSavedVocabularyListAsync(
        string userId, string? searchTerm = null, int pageNumber = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var query = db.SavedVocabularies.AsNoTracking()
            .Include(sv => sv.Vocabulary)
            .Where(sv => sv.UserId == userId && sv.Vocabulary != null);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(sv =>
                sv.Vocabulary!.Word.ToLower().Contains(term) ||
                sv.Vocabulary.Meaning.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(sv => sv.DateSaved)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(sv => new SavedVocabularyDto(
                sv.VocabularyId,
                sv.Vocabulary!.Word,
                sv.Vocabulary.Phonetic,
                sv.Vocabulary.Meaning,
                sv.Vocabulary.Example,
                sv.Interval,
                sv.NextReviewDate,
                sv.DateSaved
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedListDto<SavedVocabularyDto>(
            items, pageNumber, totalPages, totalCount, pageNumber > 1, pageNumber < totalPages
        ));
    }

    // UC-11.5 Remove Saved Vocabulary
    public async Task<Result<bool>> RemoveSavedVocabularyAsync(
        string userId, int vocabularyId, CancellationToken ct = default)
    {
        var saved = await db.SavedVocabularies
            .FirstOrDefaultAsync(sv => sv.UserId == userId && sv.VocabularyId == vocabularyId, ct);

        if (saved is null)
        {
            return Result.Failure<bool>("Từ vựng không tồn tại trong sổ tay.");
        }

        db.SavedVocabularies.Remove(saved);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    // UC-12.1 View Vocabulary Passage
    public async Task<Result<VocabContextDto>> GetVocabularyPassagesAsync(
        int vocabularyId, CancellationToken ct = default)
    {
        var vocab = await db.Vocabularies.AsNoTracking()
            .Include(v => v.Passages)
            .FirstOrDefaultAsync(v => v.Id == vocabularyId, ct);

        if (vocab is null)
        {
            return Result.Failure<VocabContextDto>("Từ vựng không tồn tại.");
        }

        var passages = vocab.Passages.Select(p => new VocabPassageDto(
            p.Id, p.VocabularyId, p.Content
        )).ToList();

        var dto = new VocabContextDto(
            vocab.Id,
            vocab.Word,
            vocab.Phonetic,
            vocab.Meaning,
            vocab.Example,
            passages
        );

        return Result.Success(dto);
    }

    // UC-12.2 Look Up Vocabulary
    public async Task<Result<VocabularyDetailDto>> LookupVocabularyAsync(
        string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Result.Failure<VocabularyDetailDto>("Từ khóa tìm kiếm không hợp lệ.");
        }

        var term = keyword.Trim().ToLowerInvariant();
        var vocab = await db.Vocabularies.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Word.ToLower() == term, ct);

        if (vocab is null)
        {
            // Try prefix search if exact match not found
            vocab = await db.Vocabularies.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Word.ToLower().StartsWith(term), ct);
        }

        if (vocab is null)
        {
            return Result.Failure<VocabularyDetailDto>($"Không tìm thấy từ vựng '{keyword}' trong hệ thống.");
        }

        return Result.Success(new VocabularyDetailDto(
            vocab.Id,
            vocab.Word,
            vocab.Phonetic,
            vocab.Meaning,
            vocab.Example
        ));
    }
}
