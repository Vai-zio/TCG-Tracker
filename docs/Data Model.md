# Data Model

The classes in `src/PokemonCardManager.Core/`. Each becomes a table ([[Entity Framework Core]]).

## The key idea: printing vs copy
```
CardSet ──< CardReference ──< PhysicalCard ──< CardImage
 "Base"     Charizard #4       your copy #1      front photo
            en, holo, 1st Ed   your copy #2      back photo
```
- **`CardReference`** = one *printing*. Identified by set + collector number + language + finish + edition. The name alone is **not** enough (there are many Charizards).
- **`PhysicalCard`** = one *copy you own*. Three copies of the same card are three rows, so each keeps its own photos, condition, price paid and history.

## The rest
| Class | Stores |
|---|---|
| `StorageLocation` | Binder › page › pocket. Points at its `Parent`, so they nest. |
| `CardImage` | Photo *info* (path, size, SHA-256 hash). The file itself is on disk ([[Photos]]). |
| `ConditionAssessment` | One inspection at one moment: centering, findings, defects, optional grade estimate. Never edited, only added ([[Interceptors]]). |
| `PriceObservation` | One price seen somewhere: asking, sold, or estimate. |
| `ExchangeRate` | Currency conversion rates. |
| `CardTransaction` | Buying, selling, trading. |
| `GradingSubmission` | A batch sent to PSA/CGC/BGS and what came back. |
| `AuditEntry` | Log of every create/update/delete. |

Most of these tables exist already but have no screens yet. They are ready for later milestones.

## Shared building blocks
- **`Entity`** (`Common/Entity.cs`): every class inherits an `Id`, a GUID made in code with `Guid.CreateVersion7()`. Because the Id exists before saving, records can be linked and exported safely. Version 7 GUIDs sort by time, which keeps indexes tidy.
- **`IHasTimestamps`**: classes with `CreatedAt`/`UpdatedAt`, filled in automatically ([[Interceptors]]).
- **Enums** like `RawCondition.NearMint` are saved as text (`"NearMint"`), so reordering the enum can't scramble data.
