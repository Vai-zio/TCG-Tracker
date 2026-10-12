namespace PokemonCardManager.Core.Common;

/// <summary>The current time. An interface so tests can use a fixed time.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
