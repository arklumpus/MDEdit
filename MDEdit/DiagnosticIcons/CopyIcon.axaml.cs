﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MDEdit.DiagnosticIcons
{
    internal class CopyIcon : UserControl
    {
        public CopyIcon()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
