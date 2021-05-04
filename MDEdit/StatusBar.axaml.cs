/*
    MDEdit - A MarkDown source code editor with syntax highlighting and
    real-time preview.
    Copyright (C) 2021  Giorgio Bianchini
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, version 3.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace MDEdit
{
    internal class StatusBar : UserControl
    {
        public StatusBar()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.FindControl<ComboBox>("FontSizeBox").SelectionChanged += FontSizeChanged;
            this.FindControl<ToggleButton>("ToggleSettingsContainerButton").PropertyChanged += ToggleSettingsContainerButtonButtonChanged;
            this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").PropertyChanged += ToggleSaveHistoryContainerButtonChanged;
            this.FindControl<ToggleButton>("TogglePreviewButton").PropertyChanged += TogglePreviewContainerButtonChanged;
        }

        static readonly double[] FontSizes = new double[] { 8, 9, 10, 11, 12, 14, 16, 20, 24, 30, 36 };

        private void FontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            this.FindAncestorOfType<Editor>().MarkdownRenderer.ImageMultiplier = FontSizes[this.FindControl<ComboBox>("FontSizeBox").SelectedIndex] / 14.0 * 1.33;
            this.FindAncestorOfType<Editor>().FontSize = FontSizes[this.FindControl<ComboBox>("FontSizeBox").SelectedIndex];
        }

        public void IncreaseFontSize()
        {
            if (this.FindControl<ComboBox>("FontSizeBox").SelectedIndex < this.FindControl<ComboBox>("FontSizeBox").ItemCount - 1)
            {
                this.FindControl<ComboBox>("FontSizeBox").SelectedIndex++;
            }
        }

        public void DecreaseFontSize()
        {
            if (this.FindControl<ComboBox>("FontSizeBox").SelectedIndex > 0)
            {
                this.FindControl<ComboBox>("FontSizeBox").SelectedIndex--;
            }
        }

        private void TogglePreviewContainerButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("TogglePreviewButton").IsChecked == true;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().OpenSidePanel();
                }
                else
                {
                    this.FindAncestorOfType<Editor>().CloseSidePanel();
                }
            }
        }

        private void ToggleSettingsContainerButtonButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true;

                this.FindAncestorOfType<Editor>().SettingsContainer.IsVisible = isChecked;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().OpenBottomPanel();
                    this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked = false;
                }
                else if (!(this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true))
                {
                    this.FindAncestorOfType<Editor>().CloseBottomPanel();
                }
            }
        }

        private void ToggleSaveHistoryContainerButtonChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                bool isChecked = this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true;

                this.FindAncestorOfType<Editor>().SaveHistoryContainer.IsVisible = isChecked;

                if (isChecked)
                {
                    this.FindAncestorOfType<Editor>().SaveHistoryContainer.Refresh();
                    this.FindAncestorOfType<Editor>().OpenBottomPanel();
                    this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked = false;
                }
                else if (!(this.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsChecked == true) && !(this.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsChecked == true))
                {
                    this.FindAncestorOfType<Editor>().CloseBottomPanel();
                }
            }
        }
    }
}
