# Valuation

What cards are worth. Screen: `MarketView` + `MarketViewModel`. Service: `Infrastructure/Services/MarketService.cs`. Maths: `Core/Valuation/`.

## Prices are observations
Every price is **its own row** (`PriceObservation`), never overwritten. Each says what kind it is:
- **Completed sale**: someone actually paid this.
- **Asking price**: someone wants this. Not proof of value.
- **Estimate**: a guess or aggregate.

These are **never mixed**. Raw and each graded tier (e.g. PSA 9) are kept apart too.

## Summaries: `PriceStatistics.cs`
For one card, one tier and one kind, within the last 365 days:
- **Median** (the middle value; one crazy sale can't drag it like an average would).
- **Low/High**: the 25th and 75th percentiles once there are 4+ prices, else min and max.
- **Warnings**: old data is marked *stale* (90 days), fewer than 3 prices is "a rough guide".

These numbers come from `appsettings.json` → `"Valuation"`.

## Currencies: `ExchangeRateTable.cs`
You record rates yourself (Market → Exchange rates). Nothing is fetched or guessed.
- It uses the rate **closest to each price's date**.
- An EUR→DKK rate also works backwards (1 ÷ rate), marked "inverted".
- No rate? The price is **left out with a warning**, never silently mixed. `Money` would refuse anyway ([[Money and Decimals]]).

## Getting prices in
- **By hand** in the Market screen ([[Validation]] checks each one).
- **CSV import** (`PriceCsv.cs`): required columns `Set, Number, Price, Currency, Type, Date`. Comma or semicolon separated, so Danish Excel works. The preview lists bad lines; the good ones are saved in one transaction. Month-first dates are refused as ambiguous (is 03/04 March or April?).
- **Online**: `IMarketPriceProvider`, currently `TcgdexPriceProvider`. TCGdex includes Cardmarket (EUR) and TCGplayer (USD) figures. They're stored as **estimates**, because they're marketplace aggregates of unknown condition.

Next: [[Grading Profit]]
