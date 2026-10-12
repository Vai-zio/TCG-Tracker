# TCG Tracker Docs

Open this `docs/` folder as an Obsidian vault. Each note explains one idea in plain words; follow the links to go deeper. For how to *use* the app, see [[USER-GUIDE]].

## Start here
1. [[How the App Fits Together]]: the big picture in one page
2. [[Code Map]]: which folder and file holds what
3. [[Startup]]: what happens when the app opens

## The features, in the order you'd use them
1. [[Data Model]]: what we store (printings, your copies, photos, history)
2. [[Searching the Collection]]: one search, from the screen down to SQL
3. [[Photos]]: importing and showing pictures
4. [[Card Identification]]: photo → OCR → catalog match
5. [[Inspection]]: photo quality, centering, marking defects
6. [[Grading Assessment]]: "the best grade this card could still get"
7. [[Valuation]]: price history, currencies, CSV import
8. [[Grading Profit]]: "should I grade it?", with a worked example
9. [[Submissions and Reports]]: tracking batches, predictions vs real grades
10. [[Backups and Restore]]: keeping your data safe

## Building blocks
- [[Navigation]]: how pages switch
- [[Themes]]: dark and light mode
- [[Validation]]: how bad input is caught
- [[Money and Decimals]]: why prices are never floats
- [[Interceptors]]: automatic timestamps, audit log and history protection
- [[Configuration and Logging]]: `appsettings.json` and log files
- [[OpenCV]]: the image-processing library

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
