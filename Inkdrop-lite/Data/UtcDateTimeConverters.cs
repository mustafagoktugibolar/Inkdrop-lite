using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inkdrop_lite.Data
{
    internal static class UtcDateTime
    {
        // Unspecified is treated as already-UTC: SQLite drops Kind, so values read back are Unspecified.
        public static DateTime Normalize(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => UtcDateTime.Normalize(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        value => value.HasValue ? UtcDateTime.Normalize(value.Value) : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
}
