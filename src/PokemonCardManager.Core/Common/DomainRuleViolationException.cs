namespace PokemonCardManager.Core.Common;

/// <summary>Thrown when an operation would break a data rule, e.g. editing a historical assessment.</summary>
public class DomainRuleViolationException(string message) : InvalidOperationException(message);
