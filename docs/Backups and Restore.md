# Backups and Restore

Service: `Infrastructure/Services/BackupService.cs`. Screen: Backup & export.

## Making a backup
One `.zip` file containing:
1. `collection.db`: a consistent copy made with **SQLite's online backup** (`source.BackupDatabase(target)`). Safe while the app is running, unlike copying the file.
2. Optionally every photo.
3. `manifest.json`: app version, database version (the latest [[Migrations|migration]] name), and a **SHA-256 per file**.

A database-only backup is made **automatically once a week** at [[Startup]] (`"Backup"` → `"AutomaticIntervalDays"`). If it fails, it's logged and the app carries on.

## Checking a backup
"Check" re-computes every file's SHA-256 against the manifest and runs `PRAGMA integrity_check` on the database copy. Any mismatch means the backup is damaged.

## Restoring, step by step
1. **Verify** the backup. Damaged → refused.
2. Made by a **newer** app version (unknown migration)? → refused, update the app first.
3. Make a **safety backup** of the current state ("before-restore").
4. Extract the database to a temporary file, close all connections, then swap it in.
5. Put back **missing** photos. Existing photos are left alone; nothing is deleted.
6. Run the database initializer, which upgrades an **older** backup to the current schema.
7. Restart the app.

## Security details
- **Zip slip**: a zip entry named `../../Windows/x` could write outside the target folder. `IsSafeEntryName` rejects those, and `ImageFileStore` refuses paths outside the images folder ([[Photos]]).
- Backups aren't encrypted. Keep them somewhere private.

## CSV export
`ExportService` writes the collection and the prices to CSV. The price file can be re-imported in Market ([[Valuation]]).
