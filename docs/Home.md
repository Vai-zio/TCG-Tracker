# TCG Tracker Docs

Open this `docs/` folder as an Obsidian vault. Each note explains one idea in plain words; follow the links to go deeper.

## Start here
1. [[How the App Fits Together]]: the big picture in one page
2. [[Code Map]]: which folder and file holds what
3. [[Startup]]: what happens when the app opens

## Follow the code
- [[Data Model]]: what we store (printings, your copies, photos, history)
- [[Searching the Collection]]: one search, from the screen down to SQL
- [[Navigation]]: how pages switch
- [[Photos]]: how pictures are imported and shown
- [[Validation]]: how bad input is caught
- [[Money and Decimals]]: why prices are never floats
- [[Interceptors]]: automatic timestamps, audit log and history protection
- [[Configuration and Logging]]: `appsettings.json` and log files

## Core ideas
- [[MVVM]]: how screens are kept separate from logic
- [[WPF and XAML]]: how the screens are drawn
- [[Data Binding]]: how a screen and its data stay in sync
- [[Dependency Injection]]: how classes get the things they need
- [[Entity Framework Core]]: how C# objects become database rows
- [[SQLite]]: the database file itself
- [[Migrations]]: how the database shape changes safely
- [[Tests]]: how we check the code works

## Reference
- [[Glossary]]: short definitions of every term
