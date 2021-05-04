using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MDEdit.DiagnosticIcons
{
    internal class StartingIcon : UserControl
    {
        public StartingIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
