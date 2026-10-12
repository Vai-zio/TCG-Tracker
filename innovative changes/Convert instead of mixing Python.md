---
tags: [innovative-changes, architecture]
---
# Convert instead of mixing Python

**Question:** join the Python code with the C# app, or convert it?

**Answer: convert.** .NET already has everything the Python files use:

| Python | .NET (built in) |
|---|---|
| `sqlite3` | EF Core + `Microsoft.EntityFrameworkCore.Sqlite` |
| `PBKDF2HMAC` | `Rfc2898DeriveBytes.Pbkdf2(...)` |
| `AESGCM` | `System.Security.Cryptography.AesGcm` |
| `os.urandom` | `RandomNumberGenerator.GetBytes(...)` |
| `base64` | `Convert.ToBase64String(...)` |

**Why not join them?** Running Python from C# (e.g. Python.NET) means shipping a Python runtime with the app, two languages to debug, and two places for security bugs. Nothing here needs Python.

Back to [[00 Index]]
