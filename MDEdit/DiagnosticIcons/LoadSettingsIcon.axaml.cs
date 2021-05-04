﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MDEdit.DiagnosticIcons
{
    internal class LoadSettingsIcon : UserControl
    {
        public LoadSettingsIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
