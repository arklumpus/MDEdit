using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MDEdit.DiagnosticIcons
{
    internal class SettingsIcon : UserControl
    {
        public SettingsIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
