# Code Map

Where each kind of code lives. See [[How the App Fits Together]] for why it is split this way.

| Folder | Layer | What goes here |
|---|---|---|
| `TcgTracker.Core` | Core | Card classes (the *model*), business rules, interfaces |
| `TcgTracker.Data` | Data | The `DbContext`, database setup, [[Migrations]] |
| `TcgTracker.App` | App | WPF windows (`.xaml`), ViewModels, startup |
| `TcgTracker.Tests` | Tests | Automated [[Tests]] |

> [!note] Work in progress
> The first code is still being built. This map will be filled in with real file names and a walkthrough of each one once it lands.
