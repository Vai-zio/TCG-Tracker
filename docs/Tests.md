# Tests

Tests are small programs that check the real code does what we expect. Run them all with:

```
dotnet test
```

## What a test looks like
```csharp
[Fact]
public void New_card_starts_with_quantity_one()
{
    var card = new Card();
    Assert.Equal(1, card.Quantity);
}
```
- `[Fact]` marks a test (xUnit style).
- **Arrange** (make a card), **Act** (do something), **Assert** (check the result).

## What we test
- **Core rules**: pure logic, fast and easy.
- **ViewModels**: possible without a window, thanks to [[MVVM]].
- **Data**: against a throwaway in-memory [[SQLite]] database, so real data is never touched.

[[Dependency Injection]] makes this possible by letting tests pass in fakes.
