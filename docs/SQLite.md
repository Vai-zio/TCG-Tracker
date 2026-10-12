# SQLite

SQLite is a full database stored in **one file** on your computer. No server, no internet.

## Why it fits
- Works offline, which is a core goal of the app.
- Backing up is as simple as copying the file (while the app is closed).
- Easily handles tens of thousands of cards.

## Where it lives
The app keeps the `.db` file in your user data folder. The exact path will be noted in [[Code Map]] once the code lands.

## Peeking inside
Open the file with a free tool like **DB Browser for SQLite** to see tables and rows. Look, but don't edit by hand while the app is running.

Related: [[Entity Framework Core]], [[Migrations]]
