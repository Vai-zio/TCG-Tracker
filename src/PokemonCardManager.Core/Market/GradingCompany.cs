using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Market;

/// <summary>A professional grading service, e.g. PSA, BGS or CGC.</summary>
public class GradingCompany : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Website { get; set; }
}
