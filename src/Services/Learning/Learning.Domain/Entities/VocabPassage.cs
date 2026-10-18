namespace Learning.Domain.Entities;

public class VocabPassage
{
    public int Id { get; set; }
    public int VocabularyId { get; set; }
    public string Content { get; set; } = string.Empty;

    // Navigation property
    public Vocabulary? Vocabulary { get; set; }
}
