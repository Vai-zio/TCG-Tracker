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
| Backups / exports | Yes, `EncryptFileAsync` | Backups leave the PC (USB, cloud), so they are the most exposed copy. |
| Live SQLite database | Not yet | Encrypting it needs SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`) in the data project. The vault's data key can be its password. Planned as a follow-up so it doesn't clash with the schema work. |
| Card photos | No | Not sensitive, and encrypting 20k+ images would slow browsing. They are encrypted when included in a backup. |

## Format compatibility

- New blobs start with a version byte (and strings with `v1:`) so the format can change later without breaking old data.
- `PasswordCrypto.DecryptLegacyPythonString` reads strings made by the original `Cryptography.py` (salt + nonce + ciphertext, 200k rounds).

## Wiring it up (DI)

```csharp
services.AddSingleton<IKeyVault>(_ => new FileKeyVault(Path.Combine(appDataFolder, "vault.key")));
services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();
```

At startup: `Exists` false → ask for a new master password and `CreateAsync`; otherwise `TryUnlockAsync`. Forgetting the master password means the encrypted data cannot be recovered.

## Tests

```
dotnet test tests/PokemonCardManager.Security.Tests
```
