# MVVM

**Model, View, ViewModel.** A pattern that keeps screen code apart from logic.

| Part | Job | Example |
|---|---|---|
| **Model** | The data | A `Card` with a name, set and price |
| **View** | What you see (XAML) | A window with a list of cards |
| **ViewModel** | The middleman | Holds the card list and the "Add card" command |

## How they talk
- The **View** never calls the database. It only *binds* to properties on its ViewModel ([[Data Binding]]).
- The **ViewModel** never touches buttons or windows. It just exposes data and commands.
- The **Model** knows nothing about either.

## Why bother?
You can test a ViewModel without opening a window ([[Tests]]), and you can redesign a screen without rewriting logic.

## Two building blocks you'll see
- `INotifyPropertyChanged`: a ViewModel raises this when a value changes, so the screen redraws.
- `ICommand`: wraps a method so a button can call it.

Related: [[WPF and XAML]]
