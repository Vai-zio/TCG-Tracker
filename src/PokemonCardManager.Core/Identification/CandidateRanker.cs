namespace PokemonCardManager.Core.Identification;

/// <summary>A catalog card with how well it matches what was read, and why.</summary>
public sealed record MatchCandidate(CatalogCard Card, double Score, IReadOnlyList<string> Reasons);

/// <summary>
/// Orders catalog cards by how well they fit the text read from the photo. The score is a ranking aid
/// (0–1), not a probability: it says which candidate fits best, not how likely it is to be right.
/// Weights: collector number 0.45 (the most specific clue), name 0.40, set size 0.15.
/// </summary>
public static class CandidateRanker
{
    public const double NumberWeight = 0.45, NameWeight = 0.40, SetTotalWeight = 0.15;

    public static IReadOnlyList<MatchCandidate> Rank(ExtractedCardText read, IEnumerable<CatalogCard> cards, int max = 10) =>
        cards
            .Select(card => Score(read, card))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

    public static MatchCandidate Score(ExtractedCardText read, CatalogCard card)
    {
        var reasons = new List<string>();
        double score = 0;

        if (read.Number is { } number)
        {
            if (CardTextParser.NormalizeNumber(card.CollectorNumber) == number.Normalized)
            {
                score += NumberWeight;
                reasons.Add($"number {card.CollectorNumber} matches");
            }
            else reasons.Add($"number {card.CollectorNumber} differs from {number}");

            if (number.SetTotal is { } total && card.SetOfficialCount is { } count)
            {
                if (total == count) { score += SetTotalWeight; reasons.Add($"set size {count} matches"); }
                else reasons.Add($"set size {count} differs from {total}");
            }
        }

        if (read.Name is { } name)
        {
            var similarity = CardTextParser.NameSimilarity(name, card.Name);
            score += NameWeight * similarity;
            reasons.Add($"name {similarity:P0} similar");
        }

        return new MatchCandidate(card, Math.Round(score, 4), reasons);
    }
}
