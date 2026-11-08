using AutoMapper;
using BuildingBlocks.Common.Results;
using Learning.API.Data;
using Learning.API.DTOs;
using Learning.API.Entities;
using Learning.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Services;

public class VocabularyService
{
    private readonly ILearningDbContext _db;
    private readonly GamificationService _gamificationService;
    private readonly IVocabularyRepository? _vocabRepository;
    private readonly IMapper? _mapper;

    public VocabularyService(
        ILearningDbContext db,
        GamificationService gamificationService,
        IVocabularyRepository? vocabRepository = null,
        IMapper? mapper = null)
    {
        _db = db;
        _gamificationService = gamificationService;
        _vocabRepository = vocabRepository;
        _mapper = mapper;
    }

    // UC-11.1 Review Flashcards (Due for spaced repetition review)
    public async Task<Result<PaginatedListDto<SavedVocabularyDto>>> GetReviewFlashcardsAsync(string userId, int pageNumber = 1, int pageSize = 10, bool all = false, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        if (_vocabRepository != null && _mapper != null)
        {
            var (entities, total) = await _vocabRepository.GetDueFlashcardsAsync(userId, now, pageNumber, pageSize, all, ct);
            var dtos = _mapper.Map<List<SavedVocabularyDto>>(entities);
            var pages = (int)Math.Ceiling(total / (double)pageSize);
            return Result.Success(new PaginatedListDto<SavedVocabularyDto>(
                dtos, pageNumber, pages, total, pageNumber > 1, pageNumber < pages));
        }

        var query = _db.SavedVocabularies.AsNoTracking()
            .Include(sv => sv.Vocabulary)
            .Where(sv => sv.UserId == userId && (all || sv.NextReviewDate <= now) && sv.Vocabulary != null);

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
    public async Task<Result<RateVocabularyResponse>> RateMemorizationAsync(
        string userId, RateVocabularyRequest request, CancellationToken ct = default)
    {
        SavedVocabulary? saved;
        if (_vocabRepository != null)
        {
            saved = await _vocabRepository.GetSavedVocabularyAsync(userId, request.VocabularyId, includeVocabulary: true, ct);
        }
        else
        {
            saved = await _db.SavedVocabularies
                .Include(sv => sv.Vocabulary)
                .FirstOrDefaultAsync(sv => sv.UserId == userId && sv.VocabularyId == request.VocabularyId, ct);
        }

        if (saved is null)
            return Result.Failure<RateVocabularyResponse>("Tu vung chua duoc luu trong so tay.");

        // Spaced Repetition SRS algorithm (SuperMemo SM-2 modified)
        if (request.IsRemembered)
        {
            saved.Interval = saved.Interval switch
            {
                1 => 3,
                3 => 7,
                7 => 14,
                14 => 30,
                _ => (int)Math.Round(saved.Interval * 1.8)
            };
        }
        else
        {
            saved.Interval = 1;
        }

        saved.NextReviewDate = DateTime.UtcNow.AddDays(saved.Interval);

                if (_vocabRepository != null)
            await _vocabRepository.SaveChangesAsync(ct);
        else
            await _db.SaveChangesAsync(ct);

        await _gamificationService.RecordStudyActivityAsync(userId, ct);

        var msg = request.IsRemembered
            ? $"Tuyet voi! Tu se duoc on lai sau {saved.Interval} ngay."
            : "Khong sao, tu se duoc on lai vao ngay mai!";

        return Result.Success(new RateVocabularyResponse(
            saved.VocabularyId,
            saved.Interval,
            saved.NextReviewDate,
            msg
        ));
    }

    // UC-11.3 Save Vocabulary (Add to notebook)
    public async Task<Result<SaveVocabularyResponse>> SaveVocabularyAsync(
        string userId, SaveVocabularyRequest request, CancellationToken ct = default)
    {
        Vocabulary? vocab;
        bool exists;
        if (_vocabRepository != null)
        {
            vocab = await _vocabRepository.GetByIdAsync(request.VocabularyId, ct);
            exists = await _vocabRepository.IsVocabularySavedAsync(userId, request.VocabularyId, ct);
        }
        else
        {
            vocab = await _db.Vocabularies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VocabularyId, ct);
            exists = await _db.SavedVocabularies.AnyAsync(sv => sv.UserId == userId && sv.VocabularyId == request.VocabularyId, ct);
        }

        if (vocab is null)
            return Result.Failure<SaveVocabularyResponse>("Tu vung khong ton tai.");

        if (exists)
            return Result.Failure<SaveVocabularyResponse>("Tu vung nay da duoc luu vao so tay truoc do.");

        var saved = new SavedVocabulary
        {
            UserId = userId,
            VocabularyId = request.VocabularyId,
            Interval = 1,
            NextReviewDate = DateTime.UtcNow.Date.AddDays(1),
            DateSaved = DateTime.UtcNow
        };

        if (_vocabRepository != null)
        {
            await _vocabRepository.AddSavedVocabularyAsync(saved, ct);
            await _vocabRepository.SaveChangesAsync(ct);
        }
        else
        {
            _db.SavedVocabularies.Add(saved);
            await _db.SaveChangesAsync(ct);
        }

        await _gamificationService.CheckAndGrantAchievementsAsync(userId, ct);

        return Result.Success(new SaveVocabularyResponse(
            vocab.Id,
            vocab.Word,
            vocab.Meaning,
            saved.DateSaved,
            saved.NextReviewDate
        ));
    }

    // UC-11.4 View Vocabulary List (Notebook)
    public async Task<Result<PaginatedListDto<SavedVocabularyDto>>> GetSavedVocabularyListAsync(
        string userId, string? searchTerm, int pageNumber = 1, int pageSize = 10, CancellationToken ct = default)
    {
        if (_vocabRepository != null && _mapper != null)
        {
            var (entities, total) = await _vocabRepository.GetSavedVocabulariesAsync(userId, searchTerm, pageNumber, pageSize, ct);
            var dtos = _mapper.Map<List<SavedVocabularyDto>>(entities);
            var pages = (int)Math.Ceiling(total / (double)pageSize);
            return Result.Success(new PaginatedListDto<SavedVocabularyDto>(
                dtos, pageNumber, pages, total, pageNumber > 1, pageNumber < pages));
        }

        var query = _db.SavedVocabularies.AsNoTracking()
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
        string userId, int vocabId, CancellationToken ct = default)
    {
        SavedVocabulary? saved;
        if (_vocabRepository != null)
        {
            saved = await _vocabRepository.GetSavedVocabularyAsync(userId, vocabId, includeVocabulary: false, ct);
        }
        else
        {
            saved = await _db.SavedVocabularies
                .FirstOrDefaultAsync(sv => sv.UserId == userId && sv.VocabularyId == vocabId, ct);
        }

        if (saved is null)
            return Result.Failure<bool>("Tu vung khong co trong so tay.");

        if (_vocabRepository != null)
        {
            _vocabRepository.RemoveSavedVocabulary(saved);
            await _vocabRepository.SaveChangesAsync(ct);
        }
        else
        {
            _db.SavedVocabularies.Remove(saved);
            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(true);
    }

    // UC-12.1 View Vocabulary Passage (Contextual reading)
    public async Task<Result<VocabContextDto>> GetVocabularyPassagesAsync(
        int vocabId, CancellationToken ct = default)
    {
        Vocabulary? vocab;
        if (_vocabRepository != null)
        {
            vocab = await _vocabRepository.GetByIdWithPassagesAsync(vocabId, ct);
        }
        else
        {
            vocab = await _db.Vocabularies.AsNoTracking()
                .Include(v => v.Passages)
                .FirstOrDefaultAsync(v => v.Id == vocabId, ct);
        }

        if (vocab is null)
            return Result.Failure<VocabContextDto>("Khong tim thay tu vung.");

        var dto = new VocabContextDto(
            vocab.Id,
            vocab.Word,
            vocab.Phonetic,
            vocab.Meaning,
            vocab.Example,
            vocab.Passages.Select(p => new VocabPassageDto(p.Id, p.Content, p.VocabularyId)).ToList()
        );

        return Result.Success(dto);
    }

    // UC-12.2 Look Up Vocabulary (Dictionary lookup)
    public async Task<Result<VocabularyDetailDto>> LookupVocabularyAsync(
        string keyword, CancellationToken ct = default)
    {
        Vocabulary? vocab;
        if (_vocabRepository != null)
        {
            vocab = await _vocabRepository.LookupByKeywordAsync(keyword, ct);
        }
        else
        {
            var term = keyword.Trim().ToLowerInvariant();
            vocab = await _db.Vocabularies.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Word.ToLower() == term, ct);

            if (vocab is null)
            {
                vocab = await _db.Vocabularies.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Word.ToLower().StartsWith(term), ct);
            }
        }

        if (vocab is null)
            return Result.Failure<VocabularyDetailDto>($"Khong tim thay tu vung cho tu khoa '{keyword}'.");

        return Result.Success(new VocabularyDetailDto(
            vocab.Id,
            vocab.Word,
            vocab.Phonetic,
            vocab.Meaning,
            vocab.Example
        ));
    }
}

