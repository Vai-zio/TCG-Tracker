using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Market;

/// <summary>
/// An explicitly recorded rate: 1 <see cref="FromCurrency"/> = <see cref="Rate"/> <see cref="ToCurrency"/>.
/// Conversions always use a stored rate, so it's clear which rate and date a number came from.
/// </summary>
public class ExchangeRate : Entity
{
    public required string FromCurrency { get; set; }
    public required string ToCurrency { get; set; }
    public decimal Rate { get; set; }
    public DateOnly AsOf { get; set; }
    public required string Source { get; set; }
}
