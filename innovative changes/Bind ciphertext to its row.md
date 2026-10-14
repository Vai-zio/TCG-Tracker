---
tags: [innovative-changes, security, encryption]
---
# Bind ciphertext to its row

**Old way:** `aesgcm.encrypt(nonce, data, None)`. The `None` is the **associated data (AAD)** slot, left empty.

**The gap:** someone with file access could copy the encrypted value from card A to card B. It still decrypts fine.

**Better way:** pass something that identifies *where* the value belongs, e.g. `"Card:1234:PurchasePrice"`.

```csharp
byte[] aad = Encoding.UTF8.GetBytes($"Card:{id}:{column}");
aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
```

**How it works:** AAD isn't encrypted or stored, but it's mixed into the tag. Decrypt with a different AAD and the tag check fails. So a value only decrypts in the row and column it was made for.

> [!note] Needs a stable id
> Encrypt *after* the row has its id, or use a GUID chosen up front.

Related: [[Versioned ciphertext format]]
