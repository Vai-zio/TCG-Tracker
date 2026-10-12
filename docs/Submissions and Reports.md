# Submissions and Reports

## Submissions
A **submission** is a batch of cards sent to one grading company. Screen: `SubmissionsView`. Service: `Infrastructure/Services/SubmissionService.cs`.

It stores the service level, planned costs, the real invoiced total, order and tracking numbers.

### Status only moves forward: `Core/Submissions/SubmissionWorkflow.cs`
```
Draft ─► Shipped ─► Received ─► Grading ─► Completed ─► Returned
   └────────────── Cancelled (any time before Completed) ──┘
```
- Steps can be skipped, since not every company reports each one.
- Cards can only be added or removed while it's a **Draft**.
- Grades can be recorded once the company has received the cards.
- Returned and Cancelled are final. Sent submissions can't be deleted; they're history.

`CanMove(from, to)` holds these rules in one small, tested function. The cards' own status follows: *submitted*, then *graded*.

### Prediction vs real grade
Adding a card links its latest [[Grading Assessment]] for that company as the **prediction**. The **real grade** and certificate number are stored separately when cards come back. A card can only be in one open submission.

## Reports: `Infrastructure/Services/ReportService.cs`
Screen: `ReportsView`, each with CSV export.

**Predictions vs real grades** (`Core/Reports/PredictionComparison.cs`). The engine only predicts an upper limit, so the honest question is **"did the limit hold?"**, not "was the grade right?". Grouped per assessment version, so a rule change shows up as its own line. Under 20 cards, it says the result is anecdotal.

**Grades per company**, and **cost per submission and per card**.

**Collection value**: for each card you own, the median of recorded **sales** for its tier (raw, or the grade it came back with). Estimates only when there are no sales; **asking prices never**. Cards without a usable price are counted, not guessed. The dashboard's value tile uses this.

This report was rewritten from one subquery per card into three flat queries so it stays fast at 20,000 cards ([[Tests]]).
