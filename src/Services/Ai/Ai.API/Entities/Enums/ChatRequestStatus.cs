namespace Ai.API.Entities.Enums;

public static class ChatRequestStatus
{
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    // Rejected before generating (quota); Failed = Gemini/config error. Both may be retried with the same RequestId.
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";
}
