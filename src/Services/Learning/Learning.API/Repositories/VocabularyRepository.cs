using Learning.API.Data;
using Learning.API.Entities;
using Learning.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Repositories;

public class VocabularyRepository(LearningDbContext db) : IVocabularyRepository
{
    public async Task<Vocabulary?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Vocabularies.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<Vocabulary?> GetByIdWithPassagesAsync(int id, CancellationToken ct = default)
    {
        return await db.Vocabularies.AsNoTracking()
            .Include(v => v.Passages)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<Vocabulary?> LookupByKeywordAsync(string keyword, CancellationToken ct = default)
    {
        var term = keyword.Trim().ToLowerInvariant();
        var vocab = await db.Vocabularies.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Word.ToLower() == term, ct);

        if (vocab is null)
        {
            vocab = await db.Vocabularies.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Word.ToLower().StartsWith(term), ct);
        }

        return vocab;
    }

    public async Task<(List<SavedVocabulary> Items, int TotalCount)> GetDueFlashcardsAsync(
        string userId, DateTime now, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = db.SavedVocabularies.AsNoTracking()
            .Include(sv => sv.Vocabulary)
            .Where(sv => sv.UserId == userId && sv.NextReviewDate <= now && sv.Vocabulary != null);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(sv => sv.NextReviewDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(List<SavedVocabulary> Items, int TotalCount)> GetSavedVocabulariesAsync(
        string userId, string? searchTerm, int pageNumber, int pageSize, CancellationToken ct = default)
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

        var items = await query
            .OrderByDescending(sv => sv.DateSaved)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<SavedVocabulary?> GetSavedVocabularyAsync(
        string userId, int vocabularyId, bool includeVocabulary = false, CancellationToken ct = default)
    {
        IQueryable<SavedVocabulary> query = db.SavedVocabularies;
        if (includeVocabulary)
        {
            query = query.Include(sv => sv.Vocabulary);
        }

        return await query.FirstOrDefaultAsync(
            sv => sv.UserId == userId && sv.VocabularyId == vocabularyId, ct);
    }

    public async Task<bool> IsVocabularySavedAsync(string userId, int vocabularyId, CancellationToken ct = default)
    {
        return await db.SavedVocabularies.AnyAsync(
            sv => sv.UserId == userId && sv.VocabularyId == vocabularyId, ct);
    }

    public async Task<int> CountSavedVocabulariesByUserAsync(string userId, CancellationToken ct = default)
    {
        return await db.SavedVocabularies
            .Where(x => x.UserId == userId)
            .CountAsync(ct);
    }

    public async Task AddSavedVocabularyAsync(SavedVocabulary entity, CancellationToken ct = default)
    {
        await db.SavedVocabularies.AddAsync(entity, ct);
    }

    public void RemoveSavedVocabulary(SavedVocabulary entity)
    {
        db.SavedVocabularies.Remove(entity);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await db.SaveChangesAsync(ct);
    }
}
