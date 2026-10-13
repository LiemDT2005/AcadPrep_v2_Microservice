namespace Content.Domain.Entities;

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string OptionLetter { get; set; } = null!; // A, B, C, D
    public string OptionText { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}
