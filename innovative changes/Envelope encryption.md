---
tags: [innovative-changes, security, encryption]
---
# Envelope encryption

**Old way:** the master password directly makes the key that encrypts data.

**Why it hurts:** changing the master password means decrypting and re-encrypting *everything*.

**Better way:** two keys.
1. **DEK** (data key): 32 random bytes. Encrypts all data. Never changes.
2. **KEK** (key-encryption key): derived from the master password. Only encrypts the DEK.

Store the *wrapped* (encrypted) DEK in the database.

```
master password --PBKDF2--> KEK --AES-GCM--> wrapped DEK (stored)
                                    DEK --AES-GCM--> card data
```

**How it works:**
- **Unlock:** derive KEK, unwrap DEK, keep DEK in memory.
- **Change password:** unwrap DEK with old KEK, re-wrap with new KEK. One tiny write, done.
- **Wrong password:** AES-GCM unwrapping fails its tag check, so you know the password was wrong without storing any hash of it.

Bonus: you could wrap the same DEK a second time with a recovery key you print and keep safe.

Related: [[Derive the key once per session]], [[Versioned ciphertext format]]
