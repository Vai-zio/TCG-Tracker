# Tests

Small programs that check the real code does what we expect. Run them all from the repo root:
```
dotnet test
```
They run on Windows, Linux and macOS.

## What a test looks like (xUnit)
```csharp
[Fact]
public void Refuses_to_combine_different_currencies()   // simplified from Domain/MoneyTests.cs
{
    var dkk = new Money(10m, "DKK");          // Arrange
    var eur = new Money(5m, "EUR");
    Assert.Throws<InvalidOperationException>(() => dkk + eur);  // Act + Assert
}
```
`[Fact]` marks a test. The pattern is **Arrange** (set up), **Act** (do it), **Assert** (check).

## The three kinds here
| Folder | Tests | Speed |
|---|---|---|
| `Domain/` | Pure rules: `Money`, centering, ranking, assessment, valuation, submission workflow | instant |
| `Imaging/` | [[OpenCV]] code on generated pictures | fast |
| `Persistence/` | The database: migrations, WAL, audit log, history protection | fast |
| `Services/` | `CollectionService`, `ImageService`, storage locations | fast |

## `TestEnvironment.cs`
Database tests use the **real** wiring (`AddInfrastructure`, see [[Dependency Injection]]) pointed at a **temporary SQLite file** that is deleted afterwards. Your real collection is never touched.

It also swaps in a **`FakeClock`** with a fixed time, so a test can check that `CreatedAt` is exactly what it expects ([[Interceptors]]).

## Testing images without photos
`Imaging/SyntheticPhotos.cs` **draws** test pictures in code: a sharp checkerboard, the same one blurred, a dark one, a card shape lying on a table, and an empty picture. The tests check that the [[OpenCV]] code rates the blur correctly, finds the card, and reports when there's no card. No real photos needed.

## Testing online services offline
The TCGdex tests feed the providers a **fake HTTP handler** that returns the documented JSON. No internet needed, and the tests always give the same answer.

## Scale: `Services/LargeCollectionTests.cs`
Inserts **20,000 cards and 10,000 prices**, then times every screen's query. Each must finish within 3 seconds (on the build machine they take under 1). The point is to catch a query that accidentally loads everything.

## What isn't tested yet
The WPF screens themselves. They compile everywhere, but must be tried by hand on Windows.
