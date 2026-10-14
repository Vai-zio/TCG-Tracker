---
tags: [innovative-changes, security, encryption]
---
# Derive the key once per session

**Old way:** every `encrypt_password` / `decrypt_password` call runs PBKDF2 with 200,000 iterations and a new salt.

**Why it hurts:** PBKDF2 is *designed* to be slow (~0.1–0.3 s). Fine for one password. Decrypting 20,000 encrypted fields would take **over an hour**.

**Better way:** run the slow step **once**, when the user unlocks the app. Keep the key in memory until they lock or close it. Each value still gets its own random **nonce**, which is what makes every ciphertext different.

```csharp
// On unlock (slow, once)
byte[] key = Rfc2898DeriveBytes.Pbkdf2(
    password, salt, iterations: 600_000,
    HashAlgorithmName.SHA256, outputLength: 32);

// Per value (fast)
byte[] nonce = RandomNumberGenerator.GetBytes(12);
using var aes = new AesGcm(key, tagSizeInBytes: 16);
aes.Encrypt(nonce, plaintext, ciphertext, tag);
```

**How it works:**
- **Salt** stops attackers from precomputing guesses. One salt per vault is enough; it's stored next to the database.
- **Nonce** must never repeat with the same key. 12 random bytes per encryption is safe for our scale.
- **Iterations:** current OWASP advice for PBKDF2-SHA256 is ~600,000. Argon2id is stronger (needs a library), worth considering.

> [!warning] Clean up
> On lock, wipe the key with `CryptographicOperations.ZeroMemory(key)`.

Next: [[Envelope encryption]]
