using Learning.API.Entities;

namespace Learning.API.Repositories.Interfaces;

public interface IVocabularyRepository
{
    Task<Vocabulary?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Vocabulary?> GetByIdWithPassagesAsync(int id, CancellationToken ct = default);
    Task<Vocabulary?> LookupByKeywordAsync(string keyword, CancellationToken ct = default);

    Task<(List<SavedVocabulary> Items, int TotalCount)> GetDueFlashcardsAsync(
        string userId, DateTime now, int pageNumber, int pageSize, bool all = false, CancellationToken ct = default);

    Task<(List<SavedVocabulary> Items, int TotalCount)> GetSavedVocabulariesAsync(
        string userId, string? searchTerm, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<SavedVocabulary?> GetSavedVocabularyAsync(
        string userId, int vocabularyId, bool includeVocabulary = false, CancellationToken ct = default);

    Task<bool> IsVocabularySavedAsync(string userId, int vocabularyId, CancellationToken ct = default);
    Task<int> CountSavedVocabulariesByUserAsync(string userId, CancellationToken ct = default);

    Task AddSavedVocabularyAsync(SavedVocabulary entity, CancellationToken ct = default);
    void RemoveSavedVocabulary(SavedVocabulary entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

