namespace Learning.Domain.Entities;

public class SavedVocabulary
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int VocabularyId { get; set; }
    public int Interval { get; set; } = 1;
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    public DateTime DateSaved { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Vocabulary? Vocabulary { get; set; }
}
