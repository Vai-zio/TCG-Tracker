---
tags: [innovative-changes, security, encryption]
---
# Versioned ciphertext format

**Old way:** `salt + nonce + ciphertext`, cut apart by fixed lengths (`[:16]`, `[16:28]`). Clever, and it works.

**Why it can hurt later:** if you ever change iterations, algorithm, or tag size, old data can't tell you which rules it was made with.

**Better way:** put a **version byte** first.

```
[version 1 byte][nonce 12][tag 16][ciphertext ...]
```

**How it works:** on decrypt, read byte 0 and pick the matching rules. New data uses version 2, old data still reads with version 1, and you can upgrade rows slowly in the background.

> [!tip] Store bytes, not Base64
> SQLite has a `BLOB` type. Base64 was needed for text columns; with `byte[]` properties in EF Core it isn't, and it saves ~33% space.

Related: [[Bind ciphertext to its row]]
