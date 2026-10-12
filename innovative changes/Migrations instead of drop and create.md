---
tags: [innovative-changes, database]
---
# Migrations instead of drop and create

**Old way:** `create_tables()` runs `DROP TABLE` then `CREATE TABLE`.

**Why it hurts:** changing the schema deletes all data. Fine for passwords during testing, a disaster with 20,000 cards.

**Better way:** EF Core migrations.

```
dotnet ef migrations add AddGradeColumn
dotnet ef database update
```

**How it works:** EF compares your C# classes with a snapshot of the last schema and writes a small script with only the difference (e.g. `ALTER TABLE ... ADD COLUMN`). The database stores which migrations already ran, so each one runs once. At app start, `db.Database.Migrate()` brings old databases up to date.

> [!tip] Take a backup before migrating
> Copy the `.db` file before `Migrate()` runs. Cheap insurance.

Related: [[EF Core instead of raw SQL]]
