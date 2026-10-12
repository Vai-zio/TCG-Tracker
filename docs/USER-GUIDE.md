# User guide

A short walk through the app, in the order you'd normally use it. Each step says what the app does behind the
scenes, so the numbers it shows make sense.

## 1. Add cards

- **Collection → Add card** for one card typed by hand. A card is identified by set + collector number +
  language + finish + edition, not by name alone, because the same name exists in many printings.
- **Add from photos** for a stack of photos. For each photo the app checks quality, finds the card's outline and
  straightens it, reads the text with Windows' built-in OCR, and searches your own collection and TCGdex.
  You confirm the match; nothing is added on a guess. The original photo is kept unchanged.

## 2. Inspect condition

**Inspection** shows a card's photos. Mark defects by dragging a box, drag the guides to measure centering, and
tick which areas (centering, corners, edges, surface) you actually looked at. Saving makes a permanent record:
later inspections are added next to it, never over it.

**Assess** on a saved inspection runs the rules. With the PSA profile it tells you the **best grade still
possible** from centering (PSA's published limits) and why. It never predicts a grade or a probability: there
isn't enough real data behind that yet. Missing photos or areas give "insufficient evidence" plus what to add.

## 3. Prices and "should I grade it?"

**Market & profitability**:

- Record prices by hand, import a CSV, or fetch TCGdex estimates. Each price is a completed sale, an asking price
  or an estimate, and these are never mixed: an asking price isn't what a card sold for.
- Summaries show the median and the middle half of prices (25th–75th percentile), so one odd sale doesn't swing
  the number. Old data is marked stale.
- Other currencies are converted only with rates you record under **Exchange rates**.
- **Should I grade it?** compares selling graded with selling raw after every cost. The break-even price is the
  graded sale price where both come out the same. Type your own chance for each grade to get an expected value;
  leave them empty and you only get per-grade results.

## 4. Send cards for grading

**Submissions → New submission**, fill in company, service level and costs, save, then tick cards to add. Each card
keeps its latest assessment as the prediction. Mark the submission shipped, received and so on as it goes. When
grades come back, type them in with the certificate numbers and mark it returned. Cards then show as graded.

## 5. Reports

- **Predictions vs real grades**: did the "best grade possible" limit hold? This is how the rules get checked
  against reality. Under 20 cards, treat it as anecdotal.
- **Collection value**: the median sale price for each card you own at its grade (or raw). Cards without a price
  are counted, not guessed. The dashboard's value tile shows the same total.
- Grades received, and cost per submission and per card. Tables export to CSV.

## 6. Backups

**Backup & export → Back up now** writes one `.zip` (database plus photos if ticked) with a checksum for every file.
A database-only backup is also made automatically once a week at startup. **Check** confirms a backup is intact.
**Restore** first saves your current state as another backup, so a restore can always be undone.
Backups are encrypted with your master password, so a copied backup is useless to anyone without it. Backups made before encryption still restore.
Copy backups to another drive or a cloud folder: a backup on the same disk doesn't survive that disk failing.

## Where things are kept

`%LocalAppData%\PokemonCardManager\`: `collection.db` (the database), `images\` (photos), `logs\` (one log file per
day, kept 14 days), `backups\`. Settings shows the exact paths.
