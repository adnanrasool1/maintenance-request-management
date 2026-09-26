using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mra.Infrastructure.Persistence;

// All timestamps are stored in UTC (architecture §6). Values read back get DateTimeKind.Utc,
// so they serialise with a trailing "Z" (API contract D-7).
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
