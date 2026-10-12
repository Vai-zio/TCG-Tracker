# PokemonCardManager.Security

Encryption for TCG Tracker, ported from `Cryptography.py` to C# (`System.Security.Cryptography`, no extra packages).

## How it works

1. **Master password → key (`PasswordCrypto`).** PBKDF2-SHA256 stretches the password into a 256-bit key. It is deliberately slow (600,000 rounds, up from 200,000 in the Python version) so guessing passwords is expensive. A random salt makes every result unique.
2. **Key → locked data (`AesGcmCipher`).** AES-256-GCM encrypts the data and adds a tag. If the key is wrong or one bit was changed, decryption throws instead of returning garbage. Every message gets a fresh random nonce.
3. **Envelope (`FileKeyVault`).** The app has one random *data key*. The master password only encrypts ("wraps") that key into `vault.key`. So:
   - the slow PBKDF2 step runs once, at unlock, not once per secret;
   - changing the password rewrites one small file, not every secret and backup.
4. **Big files (`StreamCipher`).** Backups are encrypted in 64 KB chunks so they never need to fit in memory. Each chunk is tied to its position and a "last chunk" flag, so reordering or cutting off the file is detected.

`IEncryptionService` (implemented by `AesGcmEncryptionService`) is what the rest of the app uses: `EncryptString` for secrets, `EncryptFileAsync` for backups.

## What gets encrypted, and why

| Data | Encrypted? | Why |
| --- | --- | --- |
| API keys and other secrets | Yes, `EncryptString` | Small and valuable; stored as `v1:<base64>` in config or the database. |
| Backups | Yes, `EncryptStreamAsync` per file in the zip | Backups leave the PC (USB, cloud), so they are the most exposed copy. `manifest.json` stays readable so the backup list loads fast. |
| CSV exports | No | They are made for you to open in a spreadsheet. |
| Live SQLite database | Not yet | Encrypting it needs SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`) in the data project. The vault's data key can be its password. Planned as a follow-up so it doesn't clash with the schema work. |
| Card photos | No | Not sensitive, and encrypting 20k+ images would slow browsing. They are encrypted when included in a backup. |

## Format compatibility

- New blobs start with a version byte (and strings with `v1:`) so the format can change later without breaking old data.
- `PasswordCrypto.DecryptLegacyPythonString` reads strings made by the original `Cryptography.py` (salt + nonce + ciphertext, 200k rounds).

## How the app uses it

`App.xaml.cs` registers `FileKeyVault` (file: `vault.key` in the data folder) and `AesGcmEncryptionService`, then shows `UnlockWindow` before the main window:

- **First run** (`Exists` is false): the user picks a master password (rules in `MasterPasswordRules`, at least 8 characters, typed twice) and `CreateAsync` makes the data key.
- **Every later start:** `TryUnlockAsync` derives the key from the password and unwraps the data key. A wrong password just shows an error.
- The data key stays in memory until the app closes; the host disposes the vault on exit, which wipes it. Closing the lock screen quits the app.

Forgetting the master password means the encrypted data cannot be recovered. Every backup also carries a copy of `vault.key`. It is itself encrypted with the master password, so it's safe there, and it means the backup plus your password is enough to recover. (Restoring onto a PC with a *different* vault isn't automated yet; for now, copy the backup's `vault.key` into the data folder first.)

## Tests

```
dotnet test tests/PokemonCardManager.Security.Tests
```
