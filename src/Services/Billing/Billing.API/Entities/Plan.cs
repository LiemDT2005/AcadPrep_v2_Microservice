namespace Billing.API.Entities;

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public ICollection<PlanLimit> Limits { get; set; } = [];
}
