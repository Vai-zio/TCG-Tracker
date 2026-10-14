---
tags: [innovative-changes, testing]
---
# Real tests instead of main blocks

**Old way:** `if __name__ == "__main__":` encrypts and decrypts three times and prints results. You check by eye.

**Better way:** the same idea as automated tests (xUnit), run with `dotnet test`.

```csharp
[Fact]
public void Decrypt_returns_original_text()
{
    var blob = crypto.Encrypt("Charizard");
    Assert.Equal("Charizard", crypto.Decrypt(blob));
}

[Fact]
public void Same_text_encrypts_differently_each_time()
    => Assert.NotEqual(crypto.Encrypt("x"), crypto.Encrypt("x"));

[Fact]
public void Wrong_key_fails()
{
    var blob = crypto.Encrypt("x");
    Assert.ThrowsAny<CryptographicException>(() => otherCrypto.Decrypt(blob));
}
```

**How it works:** each `[Fact]` is a tiny program that passes or fails. They run in seconds, every time you change something, so a broken change is caught before it touches real data.

For the database: run tests against an **in-memory SQLite** database so they never touch your real collection.

Related: [[Keep UI out of the data layer]]
