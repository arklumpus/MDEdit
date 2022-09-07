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
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DiffPlex;
using DiffPlex.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using VectSharp.MarkdownCanvas;

namespace MDEdit
{
    public partial class Editor : UserControl
    {
        #region Static
        internal static VectSharp.FontFamily RobotoMonoRegular;
        internal static VectSharp.FontFamily OpenSansRegular;
        internal static VectSharp.FontFamily OpenSansBold;
        internal static VectSharp.FontFamily OpenSansItalic;
        internal static VectSharp.FontFamily OpenSansBoldItalic;

        static Editor()
        {
            RobotoMonoRegular = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("MDEdit.Fonts.RobotoMono-Regular.ttf"), "resm:MDEdit.Fonts.?assembly=MDEdit#Roboto Mono");

            OpenSansRegular = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("MDEdit.Fonts.OpenSans-Regular.ttf"), "resm:MDEdit.Fonts.?assembly=MDEdit#Open Sans");
            OpenSansBold = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("MDEdit.Fonts.OpenSans-Bold.ttf"), "resm:MDEdit.Fonts.?assembly=MDEdit#Open Sans");

            OpenSansItalic = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("MDEdit.Fonts.OpenSans-Italic.ttf"), "resm:MDEdit.Fonts.?assembly=MDEdit#Open Sans");
            OpenSansBoldItalic = new VectSharp.ResourceFontFamily(typeof(Editor).Assembly.GetManifestResourceStream("MDEdit.Fonts.OpenSans-BoldItalic.ttf"), "resm:MDEdit.Fonts.?assembly=MDEdit#Open Sans");
        }
        #endregion

        #region Internal fields
        internal MarkdownSourceEditorControl EditorControl;
        internal InputHandler InputHandler;

        internal StatusBar StatusBar;
        internal SaveHistoryContainer SaveHistoryContainer;
        internal SettingsContainer SettingsContainer;
        internal MarkdownCanvasControl PreviewPanel;

        internal AutoSaver AutoSaver;
        internal PreviewUpdater CompilationErrorChecker;

        internal Document OriginalDocument;
        internal string LastSavedText;
        internal string OriginalText;
        internal long OriginalTimeStamp;
        #endregion

        #region Private fields
        private bool IsBottomPanelOpen = false;
        private double PreviousBottomPanelHeight = double.NaN;

        private bool IsSidePanelOpen = false;
        private double PreviousSidePanelWidth = double.NaN;
        #endregion

        #region Internal properties
        internal double CharacterWidth => EditorControl.CharacterWidth;

        internal Differ Differ { get; } = new Differ();
        #endregion

        /// <summary>
        /// Public constructor. This is only provided for compatibility with Avalonia (<a href="https://github.com/AvaloniaUI/Avalonia/issues/2593">see issue #2593</a>). Please use <see cref="Editor.Create"/> instead.
        /// </summary>
        [Obsolete("Please use the Editor.Create() static method instead", true)]
        public Editor()
        {
            this.InitializeComponent();
        }

        private Editor(bool _)
        {
            this.InitializeComponent();
        }

        private async Task Initialize(string sourceText, string guid, Shortcut[] additionalShortcuts)
        {
            this.Guid = guid;

            EditorControl = this.FindControl<MarkdownSourceEditorControl>("EditorControl");

            await EditorControl.SetText(sourceText);

            OriginalText = EditorControl.Text.ToString();
            OriginalDocument = EditorControl.Document;
            LastSavedText = OriginalText;
            OriginalTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            StatusBar = this.FindControl<StatusBar>("StatusBar");

            SaveHistoryContainer = this.FindControl<SaveHistoryContainer>("SaveHistoryContainer");
            PreviewPanel = this.FindControl<MarkdownCanvasControl>("PreviewPanel");

            this.MarkdownRenderer = PreviewPanel.Renderer;

            this.MarkdownRenderer.ImageUriResolver = (a, b) => AsynchronousImageCache.ImageUriResolverAsynchronous(a, b);

            this.MarkdownRenderer.ImageMultiplier = 1.33;

            AsynchronousImageCache.CacheUpdated += this.CacheUpdated;

            this.DetachedFromVisualTree += (s, e) =>
            {
                AsynchronousImageCache.CacheUpdated -= this.CacheUpdated;
            };

            SettingsContainer = new SettingsContainer(additionalShortcuts) { Margin = new Thickness(10, 0, 0, 10), IsVisible = false };
            Grid.SetRow(SettingsContainer, 2);
            this.FindControl<Grid>("ContainerGrid").Children.Add(SettingsContainer);

            EditorControl.ClearUndoStack();

            this.CompilationErrorChecker = PreviewUpdater.Attach(this);

            string autosaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name);
            Directory.CreateDirectory(Path.Combine(autosaveDirectory, Guid));
            AutoSaveFile = Path.Combine(autosaveDirectory, Guid, "autosave_" + System.Guid.NewGuid().ToString("N") + ".md");
            SaveDirectory = Path.Combine(autosaveDirectory, Guid);
            this.AutoSaver = AutoSaver.Start(this, AutoSaveFile);

            InputHandler = new InputHandler(this, EditorControl);

            OpenSidePanel();
        }

        #region Internal methods

        internal void CloseBottomPanel()
        {
            if (IsBottomPanelOpen)
            {
                IsBottomPanelOpen = false;

                PreviousBottomPanelHeight = this.FindControl<Grid>("ContainerGrid").RowDefinitions[2].Height.Value;
                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(10, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("GridSplitter").IsVisible = false;
            }
        }

        internal void OpenBottomPanel()
        {
            if (!IsBottomPanelOpen)
            {
                IsBottomPanelOpen = true;

                double height = PreviousBottomPanelHeight;
                if (double.IsNaN(height))
                {
                    if (!double.IsNaN(this.Bounds.Width) && !double.IsNaN(this.Bounds.Height) && this.Bounds.Width > 0 && this.Bounds.Height > 0)
                    {
                        PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                        PreviousSidePanelWidth = this.Bounds.Width / 2;
                        height = PreviousBottomPanelHeight;
                    }
                    else
                    {
                        height = 250;
                        void layoutHandler(object s, EventArgs e)
                        {
                            if (double.IsNaN(PreviousBottomPanelHeight))
                            {
                                PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                                PreviousSidePanelWidth = this.Bounds.Width / 2;
                                this.LayoutUpdated -= layoutHandler;
                            }

                            if (IsSidePanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(PreviousSidePanelWidth, GridUnitType.Pixel);
                            }

                            if (IsBottomPanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(PreviousBottomPanelHeight, GridUnitType.Pixel);
                            }
                        }

                        this.LayoutUpdated += layoutHandler;
                    }
                }

                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(height, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("GridSplitter").IsVisible = true;
            }
        }

        internal void CloseSidePanel()
        {
            if (IsSidePanelOpen)
            {
                IsSidePanelOpen = false;

                PreviewPanel.IsVisible = false;
                PreviousSidePanelWidth = this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2].Width.Value;
                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(10, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("VerticalGridSplitter").IsVisible = false;
            }
        }

        internal void OpenSidePanel()
        {
            if (!IsSidePanelOpen)
            {
                IsSidePanelOpen = true;

                double width = PreviousSidePanelWidth;
                if (double.IsNaN(width))
                {
                    if (!double.IsNaN(this.Bounds.Width) && !double.IsNaN(this.Bounds.Height) && this.Bounds.Width > 0 && this.Bounds.Height > 0)
                    {
                        PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                        PreviousSidePanelWidth = this.Bounds.Width / 2;
                        width = PreviousSidePanelWidth;
                    }
                    else
                    {
                        width = 400;
                        void layoutHandler(object s, EventArgs e)
                        {
                            if (double.IsNaN(PreviousBottomPanelHeight))
                            {
                                PreviousBottomPanelHeight = Math.Min(250, this.Bounds.Height * 0.5);
                                PreviousSidePanelWidth = this.Bounds.Width / 2;
                                this.LayoutUpdated -= layoutHandler;
                            }

                            if (IsSidePanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(PreviousSidePanelWidth, GridUnitType.Pixel);
                            }

                            if (IsBottomPanelOpen)
                            {
                                this.FindControl<Grid>("ContainerGrid").RowDefinitions[2] = new RowDefinition(PreviousBottomPanelHeight, GridUnitType.Pixel);
                            }
                        }

                        this.LayoutUpdated += layoutHandler;
                    }
                }

                PreviewPanel.IsVisible = true;
                this.FindControl<Grid>("ContainerGrid").ColumnDefinitions[2] = new ColumnDefinition(width, GridUnitType.Pixel);
                this.FindControl<GridSplitter>("VerticalGridSplitter").IsVisible = true;
            }
        }

        internal void SetLineDiff(IEnumerable<int> changesFromLastSave, IEnumerable<int> changesFromOriginal)
        {
            List<int> yellowLines = changesFromLastSave.ToList();

            IEnumerable<HighlightedLineRange> greenLineSpans = from el in (from el in changesFromOriginal where !yellowLines.Contains(el) select new TextSpan(el, 0)).Join() select new HighlightedLineRange(el, new SolidColorBrush(Color.FromRgb(108, 226, 108)));
            IEnumerable<HighlightedLineRange> yellowLineSpans = from el in (from el in yellowLines select new TextSpan(el, 0)).Join() select new HighlightedLineRange(el, new SolidColorBrush(Color.FromRgb(255, 238, 98)));

            EditorControl.HighlightedLines = ImmutableList.Create(greenLineSpans.Concat(yellowLineSpans).ToArray());
        }

        internal void UpdateLastSavedDocument()
        {
            this.LastSavedText = EditorControl.Text.ToString();

            if (EditorControl.ShowLineChanges)
            {
                DiffResult diffResult = Differ.CreateLineDiffs(this.OriginalText, this.LastSavedText, false);
                IEnumerable<int> changesFromOriginal = (from el in diffResult.DiffBlocks select Enumerable.Range(el.InsertStartB, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                SetLineDiff(new int[0], changesFromOriginal);
            }
        }

        internal void InvokeSaveRequested(SaveEventArgs e)
        {
            SaveRequested?.Invoke(this, e);
        }

        internal void InvokeAutosave(SaveEventArgs e)
        {
            Autosave?.Invoke(this, e);
        }

        internal void InvokePreviewRendered(PreviewRenderedEventArgs e)
        {
            PreviewRendered?.Invoke(this, e);
        }

        internal void SaveSettings()
        {
            SavedSettings settings = new SavedSettings()
            {
                AutosaveInterval = this.AutosaveInterval,
                KeepSaveHistory = this.KeepSaveHistory,
                SyntaxHighlighting = this.SyntaxHighlighting,
                CompilationTimeout = this.PreviewTimeout,
                ShowChangedLines = this.ShowLineChanges,
                ShowScrollbarOverview = this.ShowScrollbarOverview
            };

            string serialized = JsonSerializer.Serialize(settings);

            string settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownEditor");

            Directory.CreateDirectory(settingsDirectory);

            File.WriteAllText(Path.Combine(settingsDirectory, "settings.json"), serialized);
        }

        internal void LoadSettings()
        {
            string settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownEditor");

            if (File.Exists(Path.Combine(settingsDirectory, "settings.json")))
            {
                string serialized = File.ReadAllText(Path.Combine(settingsDirectory, "settings.json"));

                try
                {
                    SavedSettings settings = JsonSerializer.Deserialize<SavedSettings>(serialized);

                    SettingsContainer.FindControl<NumericUpDown>("AutosaveIntervalBox").Value = settings.AutosaveInterval / 1000;
                    SettingsContainer.FindControl<CheckBox>("KeepSaveHistoryBox").IsChecked = settings.KeepSaveHistory;
                    SettingsContainer.FindControl<CheckBox>("SyntaxHighlightingModeBox").IsChecked = settings.SyntaxHighlighting;
                    SettingsContainer.FindControl<NumericUpDown>("CompilationTimeoutBox").Value = settings.CompilationTimeout;
                    SettingsContainer.FindControl<CheckBox>("ShowChangedLinesBox").IsChecked = settings.ShowChangedLines;
                    SettingsContainer.FindControl<CheckBox>("ShowScrollbarOverviewBox").IsChecked = settings.ShowScrollbarOverview;
                }
                catch { }
            }
        }
        #endregion

        #region Private methods
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void CacheUpdated(object sender, EventArgs e)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                this.PreviewPanel.Document = null;
                await this.CompilationErrorChecker.UpdatePreview();
            });
        }

        #endregion
    }
}

