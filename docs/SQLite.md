# SQLite

SQLite is a full database stored in **one file** on your computer. No server, no internet.

## Where it lives
`%LocalAppData%\PokemonCardManager\collection.db` (change it in [[Configuration and Logging]]). Photos are in the `images\` folder next to it, logs in `logs\`.

## Why it fits
- Works offline.
- Handles tens of thousands of cards easily.
- No install for the user.

## WAL mode
At [[Startup]], `DatabaseInitializer` runs `PRAGMA journal_mode=WAL;`. Write-Ahead Logging writes changes to a side file first, which makes the database safer if the app crashes, and lets reads happen during a write. You'll see `collection.db-wal` and `collection.db-shm` files next to the database; that's normal.

## Quirks the code handles
- No exact decimal type → stored as scaled whole numbers ([[Money and Decimals]]).
- `LIKE` is case-insensitive for English letters, used for search ([[Searching the Collection]]).
- Storage location names use `NOCASE`, so "Binder" and "binder" count as the same name.

## Peeking inside
Open the file with **DB Browser for SQLite** to see tables and rows. Close the app first, and don't edit by hand.

Related: [[Entity Framework Core]], [[Migrations]]
