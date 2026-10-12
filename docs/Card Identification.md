# Card Identification

"Add from photos": the app works out which card is in a photo. Screen: `IdentifyView` + `IdentifyViewModel`. Pipeline: `Infrastructure/Services/IdentificationService.cs`.

## The pipeline
```
photo ─► 1 quality ─► 2 find + straighten ─► 3 OCR ─► 4 parse ─► 5 search catalogs ─► 6 rank ─► you confirm
```
**Each step can fail on its own.** A failure becomes a warning, and the next step carries on with what it has. Worst case you type the name and number yourself.

### 1. Quality
Same check as in [[Inspection]]. A poor photo gives a warning, not a stop.

### 2. Find and straighten: `Imaging/OpenCvCardDetector.cs`
Blur → find edges → find outlines → keep the biggest one with **four corners** → warp it onto a flat 630 × 880 px rectangle (63 × 88 mm card at 10 px/mm). See [[OpenCV]].

### 3. Read the text: `App/Ocr/WindowsOcrEngine.cs`
Uses the OCR built into Windows 10/11: free and offline. It returns lines **with their positions**. It lives in the App project because it needs Windows APIs.

### 4. Parse: `Core/Identification/CardTextParser.cs`
- **Number**: looks in the bottom 20% of the card for patterns like `136/189`, `TG05/TG30` or promo `SWSH050`, using regular expressions. OCR often reads `/` as `|`, so both are accepted.
- **Name**: the first line near the top that's mostly letters and isn't a label like "BASIC" or "HP 120".

### 5. Search catalogs
Catalogs are behind `ICardCatalogProvider`, and there can be several:
- `LocalCatalogProvider`: **your own collection**, searched first, works offline.
- `TcgdexCatalogProvider`: the free TCGdex web API. If it can't be reached, it throws `CatalogUnavailableException`, which becomes a warning.

The service receives them all as `IEnumerable<ICardCatalogProvider>`. That's [[Dependency Injection]] handing in *every* registered implementation.

### 6. Rank: `Core/Identification/CandidateRanker.cs`
Each candidate gets a score 0–1:
| Clue | Weight |
|---|---|
| collector number matches | 0.45 |
| name similarity (0–100%) | 0.40 |
| set size matches (the "189") | 0.15 |

It's a **ranking aid, not a probability**. Each candidate lists its reasons, e.g. *"number 136 matches, name 92% similar"*.

## Confirming
Nothing is added until you click. Then the card is created, the original photo is imported unchanged, the straightened crop is stored as a **derived image** pointing at the original ([[Photos]]), and the TCGdex id is saved so prices can be fetched later ([[Valuation]]).

## Not on Windows?
`UnavailableOcrEngine` reports OCR as unavailable. The tests use it, so the pipeline is still tested on Linux.
