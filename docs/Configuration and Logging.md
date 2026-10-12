# Configuration and Logging

## `appsettings.json`
File: `src/PokemonCardManager.App/appsettings.json`.
```json
"Storage": { "DataDirectory": "", "DatabaseFileName": "collection.db", "MaxImageSizeMegabytes": 100 },
"Collection": { "DefaultCurrency": "DKK" }
```
Empty `DataDirectory` means `%LocalAppData%\PokemonCardManager`.

| Section | Controls | Note |
|---|---|---|
| `Storage` | Folders, database name, max photo size | [[SQLite]], [[Photos]] |
| `Collection` | Default currency | |
| `Catalogs` | TCGdex on/off, timeout | [[Card Identification]] |
| `ImageQuality` | Sharpness/brightness limits | [[Inspection]] |
| `Grading` | Your own defect rules | [[Grading Assessment]] |
| `Valuation` | Look-back, stale-after days | [[Valuation]] |
| `Backup` | Backup folder, auto-backup interval | [[Backups and Restore]] |

Settings changed *inside* the app (like the theme) are saved in the database's `AppSettings` table instead, through `ISettingsService`.

## Options classes
`Infrastructure/Options/StorageOptions.cs` is a C# class with the same shape as the `"Storage"` section. At startup it is **bound** to that section:
```csharp
services.AddOptions<StorageOptions>().Bind(configuration.GetSection("Storage"));
```
Any class can then ask for `IOptions<StorageOptions>` and read typed values instead of raw strings. It also works out the real paths for the database, images and logs.

## Logging
Classes ask for an `ILogger<T>` and write messages like:
```csharp
logger.LogInformation("Applying {MigrationCount} migration(s)", pending.Count);
```
The `{MigrationCount}` placeholder keeps the value as data, not just text.

**Serilog** writes these to `...\PokemonCardManager\logs\app-YYYYMMDD.log`, one file per day, kept 14 days. The `"Logging"` section in `appsettings.json` controls how chatty each part is.

No secrets belong in `appsettings.json`.
