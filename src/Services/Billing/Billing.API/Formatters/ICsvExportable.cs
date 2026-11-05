namespace Billing.API.Formatters;

// Any response type implementing this can be negotiated as text/csv (orders now, revenue later).
public interface ICsvExportable
{
    string FileName { get; }
    IReadOnlyList<string> Header { get; }
    IEnumerable<IReadOnlyList<string?>> Rows { get; }
}
