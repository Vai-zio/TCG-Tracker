# Money and Decimals

## Never use `float` or `double` for money
They store numbers in binary, so `0.1 + 0.2` becomes `0.30000000000000004`. C#'s `decimal` stores base-10 digits, so it's exact. All prices are `decimal`.

## The `Money` type: `Core/Common/Money.cs`
An amount **plus** a currency:
```csharp
var a = new Money(10m, "DKK");
var b = new Money(5m, "EUR");
a + b; // throws: can't mix currencies without an exchange rate
```
This makes it impossible to add kroner to euros by accident.

## Storing decimals in SQLite
SQLite has no exact decimal column. EF would save it as text, which can't be summed or sorted correctly in SQL.

So `Persistence/Conversions/ScaledDecimalConverter.cs` stores **decimal × 10,000 as a whole number**:

| C# value | Stored as |
|---|---|
| `12.50m` | `125000` |
| `0.0001m` | `1` |

Whole numbers are exact, sortable and summable. A value with more than 4 decimals is **rejected**, never silently rounded. Exchange rates use 8 decimals instead.

This is applied to every `decimal` at once in `PokemonCardDbContext.ConfigureConventions`.
