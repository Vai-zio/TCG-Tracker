using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Tests.Domain;

public class CollectionCsvTests
{
    [Fact]
    public void Reads_friendly_spellings_and_danish_excel_files()
    {
        var rows = CollectionCsv.Parse(
            "Set;Number;Name;Finish;Condition;Status;Acquired;PurchasePrice;PurchaseCurrency;Quantity\n" +
            "Base Set;4;Charizard;Reverse holo;NM;Grading candidate;31.03.2026;1250,50;dkk;2\n");
        var row = Assert.Single(rows);
        Assert.Empty(row.Errors);
        Assert.Equal(CardFinish.ReverseHolo, row.Input!.Finish);
        Assert.Equal(RawCondition.NearMint, row.Input.RawCondition);
        Assert.Equal(WorkflowStatus.GradingCandidate, row.Input.Status);
        Assert.Equal(new DateOnly(2026, 3, 31), row.Input.AcquiredOn);
        Assert.Equal(1250.50m, row.Input.PurchasePrice);
        Assert.Equal("DKK", row.Input.PurchaseCurrency);
        Assert.Equal(2, row.Quantity);
        Assert.Equal("en", row.Input.Language);
    }

    [Fact]
    public void Line_problems_are_listed_and_header_problems_throw()
    {
        var rows = CollectionCsv.Parse("Set,Number,Name,Finish,Quantity,Acquired\nBase Set,,Charizard,Shiny,0,03/31/2026\n");
        var errors = Assert.Single(rows).Errors;
        Assert.Contains(errors, e => e.Contains("Number is empty"));
        Assert.Contains(errors, e => e.Contains("Finish 'Shiny'"));
        Assert.Contains(errors, e => e.Contains("Quantity '0'"));
        Assert.Contains(errors, e => e.Contains("Acquired '03/31/2026'")); // month-first is refused, never guessed

        Assert.Throws<FormatException>(() => CollectionCsv.Parse("Set,Number\nBase Set,4\n"));
        Assert.Throws<FormatException>(() => CollectionCsv.Parse("Set,Number,Name,Colour\n"));
    }
}
