using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Domain;

public class CardTextParserTests
{
    [Theory]
    [InlineData("136/189", "136", 189)]
    [InlineData("Illus. Someone   025/165", "025", 165)]
    [InlineData("4 | 102", "4", 102)]
    [InlineData("TG05/TG30", "TG05", null)]
    [InlineData("SWSH050", "SWSH050", null)]
    [InlineData("SVP 085", "SVP085", null)]
    public void Collector_numbers_are_found_in_ocr_text(string text, string number, int? total)
    {
        var reading = CardTextParser.FindNumber(text);
        Assert.NotNull(reading);
        Assert.Equal(number, reading.Number);
        Assert.Equal(total, reading.SetTotal);
    }

    [Theory]
    [InlineData("HP 120")]
    [InlineData("Weakness x2 Resistance -30")]
    [InlineData("")]
    public void Text_without_a_collector_number_gives_none(string text) => Assert.Null(CardTextParser.FindNumber(text));

    [Theory]
    [InlineData("025", "25")]
    [InlineData("TG05", "TG5")]
    [InlineData("swsh050", "SWSH50")]
    [InlineData("000", "0")]
    public void Numbers_are_normalized_for_comparison(string raw, string expected) =>
        Assert.Equal(expected, CardTextParser.NormalizeNumber(raw));

    [Fact]
    public void The_name_is_the_first_top_line_that_is_not_a_label()
    {
        IReadOnlyList<OcrLine> lines =
        [
            new("BASIC", new NormalizedBox(0.05, 0.03, 0.1, 0.03)),
            new("Furret HP 110", new NormalizedBox(0.15, 0.06, 0.6, 0.05)),
            new("Feelin' Fine", new NormalizedBox(0.1, 0.60, 0.5, 0.04)),
            new("136/189", new NormalizedBox(0.05, 0.93, 0.1, 0.02)),
        ];

        var text = CardTextParser.Extract(lines);

        Assert.Equal("Furret", text.Name);
        Assert.Equal("136", text.Number?.Number);
        Assert.Equal(189, text.Number?.SetTotal);
    }

    [Fact]
    public void Name_similarity_ignores_case_accents_and_tolerates_a_typo()
    {
        Assert.Equal(1, CardTextParser.NameSimilarity("Flabébé", "FLABEBE"), 6);
        Assert.True(CardTextParser.NameSimilarity("Charizarb", "Charizard") > 0.85);
        Assert.True(CardTextParser.NameSimilarity("Pikachu", "Snorlax") < 0.3);
        Assert.Equal(0, CardTextParser.NameSimilarity("", "Pikachu"));
    }
}

public class CandidateRankerTests
{
    private static CatalogCard Card(string name, string number, int? setCount, string set = "Darkness Ablaze") =>
        new() { Source = "Test", Name = name, SetName = set, CollectorNumber = number, SetOfficialCount = setCount };

    [Fact]
    public void Number_name_and_set_size_together_rank_first()
    {
        var read = new ExtractedCardText("Furret", new CollectorNumberReading("136", 189), "");
        var ranked = CandidateRanker.Rank(read,
        [
            Card("Furret", "137", 189),            // wrong number
            Card("Furret", "136", 189),            // everything matches
            Card("Furret", "136", 214, "Other"),   // set size differs
            Card("Sentret", "135", 189),
        ]);

        Assert.Equal(("136", 189), (ranked[0].Card.CollectorNumber, ranked[0].Card.SetOfficialCount));
        Assert.Equal(1.0, ranked[0].Score, 6);
        Assert.Contains("number 136 matches", ranked[0].Reasons);
        Assert.Equal("Other", ranked[1].Card.SetName);
    }

    [Fact]
    public void With_only_a_name_the_score_comes_from_name_similarity()
    {
        var match = CandidateRanker.Score(new ExtractedCardText("Furret", null, ""), Card("Furret", "136", 189));
        Assert.Equal(CandidateRanker.NameWeight, match.Score, 6);
    }
}
