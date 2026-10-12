using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PokemonCardManager.Infrastructure.Persistence.Conversions;

/// <summary>
/// All timestamps are stored in UTC. SQLite forgets the "kind" of a DateTime,
/// so this marks values read back as UTC and converts any local time to UTC on save.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
