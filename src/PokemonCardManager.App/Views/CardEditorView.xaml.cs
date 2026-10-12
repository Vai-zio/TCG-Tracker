using System.Windows.Controls;

namespace PokemonCardManager.App.Views;

public partial class CardEditorView : UserControl
{
    public CardEditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => SetBox.Focus();
    }
}
