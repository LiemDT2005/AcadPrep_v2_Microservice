using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Notifications.Infrastructure.Persistence.Converters;

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    timestamp => RequireUtc(timestamp),
    timestamp => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc))
{
    private static DateTime RequireUtc(DateTime timestamp) => timestamp.Kind == DateTimeKind.Utc
        ? timestamp
        : throw new ArgumentException("Persisted timestamps must use DateTimeKind.Utc.", nameof(timestamp));
}
