---
tags: [innovative-changes, security, encryption]
---
# Field encryption vs whole database

Two ways to protect the collection:

| | **Field-level** (your Python style) | **Whole database** (SQLCipher) |
|---|---|---|
| What's hidden | only chosen columns | everything, incl. names and indexes |
| Search/sort encrypted fields | no | yes |
| Setup | your own code | swap SQLite package, set a key |
| Risk of forgetting a column | yes | no |

**Suggestion:** encrypt the **whole database** with SQLCipher, using the DEK from [[Envelope encryption]] as its key. Add field-level encryption only for the most sensitive things if you want two layers. Encrypt **photo files** on disk with the same AES-GCM approach, since they live outside the database.

**How SQLCipher works:** it encrypts every 4 KB page of the database file with AES before writing to disk. The app sees normal SQLite; the file on disk is noise.

> [!tip] "Remember me" on Windows
> Windows DPAPI (`ProtectedData.Protect`) can store the DEK tied to your Windows login, so you don't type the master password every time. Optional convenience; weaker than always asking.

Related: [[Derive the key once per session]]
