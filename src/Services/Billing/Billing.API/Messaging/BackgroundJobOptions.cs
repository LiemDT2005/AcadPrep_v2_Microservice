namespace Billing.API.Messaging;

public class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";
    public bool Enabled { get; set; } = true;
}
