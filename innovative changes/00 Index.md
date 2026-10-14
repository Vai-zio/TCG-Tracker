---
tags: [innovative-changes, index]
---
# Innovative Changes

Ideas taken from my old Python password manager (`Datalag.py` + `Cryptography.py`) and how to do them smarter in TCG Tracker (C#, WPF, SQLite, EF Core).

Each note is short: **what the old code did → why it hurts → the better way → how it works.**

## Database layer (from Datalag.py)
- [[EF Core instead of raw SQL]]
- [[Migrations instead of drop and create]]
- [[Keep UI out of the data layer]]
- [[Partial updates with change tracking]]
- [[Automatic timestamps and history]]
- [[Fast queries for 20k cards]]
- [[Logging without leaking secrets]]

## Encryption (from Cryptography.py)
- [[Derive the key once per session]]
- [[Envelope encryption]]
- [[Versioned ciphertext format]]
- [[Bind ciphertext to its row]]
- [[Field encryption vs whole database]]

## Big picture
- [[Convert instead of mixing Python]]
- [[Real tests instead of main blocks]]

> [!note] These are ideas, not code
> The actual code lives in the app project. These notes explain the *why* so the code makes sense.
