# Grading Assessment

Turning an inspection into "what's the best grade this card could still get?". Core logic: `Core/Grading/AssessmentEngine.cs`. Screen: Inspection → History → **Assess**.

## The big rule: upper limits only
The engine **never predicts a grade**. It only says "this card **can't be better than** X, because Y". There's no validated data to predict more than that yet. Every result carries the line *"not an official grade"*.

## Grading profiles: `Core/Grading/GradingProfile.cs`
A profile holds what a company has published. The built-in **PSA** profile has PSA's centering tolerances:

| PSA grade | Front, worst allowed | Back, worst allowed |
|---|---|---|
| 10 | 55/45 | 75/25 |
| 9 | 60/40 | 90/10 |
| 8 | 65/35 | 90/10 |
| … | … | … |

You can add your own **defect rules** in `appsettings.json` → `"Grading"`, e.g. "corner whitening severity 3+ caps at 8". None ship by default, because no grader publishes a mapping from this app's 0–5 scale.

## How a centering limit is found
`CenteringCeiling` takes the worse axis (say front L/R **60/40**) and walks the grades from best down:
- 10 allows 55 → 60 is worse, no.
- 9 allows 60 → 60 fits. **Ceiling: PSA 9.**

**Near limit?** If the result is within the measuring uncertainty (at least 2 points) of a limit, it's flagged, and the engine recommends re-measuring on a sharper photo. Here 60 is exactly on PSA 9's limit, so it's flagged.

## The rest of the outcome
- **Per area**: centering, corners, edges, surface are described separately, using the worst defect in each. They're **never added up** into one score.
- **Status**: `Complete` only if all four areas were inspected, front and back photos exist, and none is inadequate. Otherwise `InsufficientEvidence`.
- **Uncertainties** and **recommendations**: missing areas, no angled-light photo, poor photos, each turned into a next step.
- **`MaxGrade`** = the lowest of all ceilings found.

## Saved for later checking
Each result is saved as its own permanent record with method "rule-based", version `rules-1+psa-centering-2026.10`, and review status "not reviewed". When real grades come back, [[Submissions and Reports]] compares them per version.

Related: [[Inspection]]
