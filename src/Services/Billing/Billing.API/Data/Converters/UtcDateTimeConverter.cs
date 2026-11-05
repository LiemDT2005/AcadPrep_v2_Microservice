using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Billing.API.Data.Converters;

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    timestamp => RequireUtc(timestamp),
    timestamp => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc))
{
    private static DateTime RequireUtc(DateTime timestamp) => timestamp.Kind == DateTimeKind.Utc
        ? timestamp
        : throw new ArgumentException("Persisted timestamps must use DateTimeKind.Utc.", nameof(timestamp));
}

public class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    timestamp => timestamp.HasValue ? RequireUtc(timestamp.Value) : null,
    timestamp => timestamp.HasValue ? DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc) : null)
{
    private static DateTime RequireUtc(DateTime timestamp) => timestamp.Kind == DateTimeKind.Utc
        ? timestamp
        : throw new ArgumentException("Persisted timestamps must use DateTimeKind.Utc.", nameof(timestamp));
}
