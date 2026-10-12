# Inspection

Checking a card's condition from its photos. Screen: `InspectionView` + `InspectionViewModel`. Saving: `Infrastructure/Services/InspectionService.cs`.

## 1. Is the photo good enough?
`Core/Inspection/ImageQuality.cs` + `Infrastructure/Imaging/OpenCvImageAnalyzer.cs` ([[OpenCV]]).

The analyzer measures four numbers on a greyscale copy:
| Number | How | Means |
|---|---|---|
| **Sharpness** | Variance of the *Laplacian* (an edge filter) on a copy scaled to 1600 px | Crisp edges give a high number; blur gives a low one |
| **Brightness** | Average grey level, 0–255 | Too dark or overexposed |
| **Clipped pixels** | Share of pure black / pure white pixels | White patches usually mean glare |
| **Size** | Short side in pixels | Too small to see whitening |

`ImageQualityRules.Evaluate` compares them with the limits in `appsettings.json` → `"ImageQuality"` and rates the photo **Good / Acceptable / Inadequate**. Each problem is phrased as what to do, e.g. *"Too blurry: hold the camera steady…"*.

Why scale first? So a 2-megapixel and a 24-megapixel photo give comparable sharpness numbers.

## 2. Centering
`Core/Inspection/Centering.cs`. You drag **eight guide lines**: the card's outer edge and the inner border edge on each side. Positions are stored as **fractions** (0–1) of the photo, so they stay right at any zoom.

Example: left border 30 px, right border 20 px →
```
L/R = 30/(30+20) = 60  →  60/40
```
**Uncertainty**: a hand-placed line can be ~2 px off. With borders adding up to 50 px, that's `100 × 2 / 50 = ±4 points`. Thin borders or small photos give a wider margin. This feeds into [[Grading Assessment]].

## 3. Marking defects
Drag a box on the photo, then pick type (e.g. `CornerWhitening`), severity 0–5 and notes.
- `DefectRegions.Guess` guesses the region from the box's centre: near two edges = a corner, near one = that edge, else surface.
- `DefectCategories.CategoryOf` sorts the defect into **centering, corners, edges or surface**. A crease near a corner counts as a corner problem.

## The overlay control
`App/Controls/InspectionOverlay.cs` is a custom control drawn on top of the photo. It sits inside the same zoom/rotate transform as the photo, so WPF hands it mouse positions already in photo coordinates. Dividing by its width/height gives the fractions.

## Saving
Each saved inspection is a **new permanent record** (`ConditionAssessment`). Old ones can't be edited ([[Interceptors]]). Areas you didn't tick are saved as "not inspected", and the result is flagged "limited evidence" if areas are missing or a photo was inadequate.

An inspection produces **no grade**. That's the job of [[Grading Assessment]].
