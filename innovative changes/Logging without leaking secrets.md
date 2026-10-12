---
tags: [innovative-changes, security]
---
# Logging without leaking secrets

**Old way:** `update_inst` prints the plaintext password to the console. Errors print `'Fejl ved sletning af tabel'` and hide the real exception.

**Better way:**
- Use `ILogger` (Microsoft.Extensions.Logging) with levels: Debug, Info, Warning, Error.
- **Never log** secrets, keys, or the master password. Log ids, not values.
- Log the **exception itself** (`_log.LogError(ex, "...")`) so you see the real cause.
- Catch only what you can handle; let the rest bubble up to one global handler that shows a friendly message.

**How it works:** the logger writes to a file or the debugger depending on configuration, and you can turn Debug off in release builds without touching the code.

Related: [[Keep UI out of the data layer]]
