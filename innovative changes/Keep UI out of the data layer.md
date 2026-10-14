---
tags: [innovative-changes, architecture, mvvm]
---
# Keep UI out of the data layer

**Old way:** `del_inst` deletes a row *and* shows `messagebox.showinfo("Success")`. Your own comment wished you could pick the item from a list instead of typing an id.

**Why it hurts:** the database code can't be tested or reused without a window popping up.

**Better way (MVVM):**
- **Repository/service** — only talks to the database. Returns results or throws.
- **ViewModel** — calls the service, holds the list of cards, decides what message to show.
- **View (XAML)** — a `DataGrid` bound to the list. The user clicks a row, the ViewModel knows the selected `Card`. No ids typed by hand.

```csharp
// ViewModel
public async Task DeleteSelectedAsync()
{
    await _cards.DeleteAsync(SelectedCard.Id);
    Cards.Remove(SelectedCard);   // list updates itself, can't delete twice
    StatusMessage = "Card deleted";
}
```

**How it works:** WPF data binding watches `ObservableCollection` and properties. When the ViewModel changes them, the screen updates by itself, which also solves the "refresh the label" TODO.

Related: [[Real tests instead of main blocks]]
