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
| `Domain/` | Pure rules: `Money`, validation, defect severity | instant |
| `Persistence/` | The database: migrations, WAL, audit log, history protection | fast |
| `Services/` | `CollectionService`, `ImageService`, storage locations | fast |

## `TestEnvironment.cs`
Database tests use the **real** wiring (`AddInfrastructure`, see [[Dependency Injection]]) pointed at a **temporary SQLite file** that is deleted afterwards. Your real collection is never touched.

It also swaps in a **`FakeClock`** with a fixed time, so a test can check that `CreatedAt` is exactly what it expects ([[Interceptors]]).

## What isn't tested yet
The WPF screens themselves. They compile everywhere, but must be tried by hand on Windows.
