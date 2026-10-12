namespace PokemonCardManager.Core.Services;

/// <summary>Creates the database or upgrades it to the latest schema. Called once at startup.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
