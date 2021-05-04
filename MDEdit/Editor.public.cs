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

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VectSharp.Markdown;

namespace MDEdit
{
    /// <summary>
    /// A C# source code editor for Avalonia.
    /// </summary>
    public partial class Editor
    {
        /// <summary>
        /// Event raised when the user uses the keyboard shortcut or presses the button to save the document.
        /// </summary>
        public event EventHandler<SaveEventArgs> SaveRequested;

        /// <summary>
        /// Event raised when the document is automatically saved.
        /// </summary>
        public event EventHandler<SaveEventArgs> Autosave;

        /// <summary>
        /// Event raised when the rendering of the document preview completes.
        /// </summary>
        public event EventHandler<PreviewRenderedEventArgs> PreviewRendered;

        /// <summary>
        /// Event raised when the document text is changed.
        /// </summary>
        public event EventHandler<EventArgs> TextChanged
        {
            add
            {
                EditorControl.TextChanged += value;
            }

            remove
            {
                EditorControl.TextChanged -= value;
            }
        }

        /// <summary>
        /// The source code of the document as a <see cref="string"/>.
        /// </summary>
        public string Text
        {
            get
            {
                VerifyAccess();
                return EditorControl.Text.ToString();
            }
        }

        /// <summary>
        /// The source code of the document as a <see cref="SourceText"/>.
        /// </summary>
        public SourceText SourceText
        {
            get
            {
                VerifyAccess();
                return EditorControl.Text;
            }
        }

        /// <summary>
        /// The <see cref="MarkdownRenderer"/> used to display the document preview.
        /// </summary>
        public MarkdownRenderer MarkdownRenderer { get; private set; }

        /// <summary>
        /// Describes the actions that the user can perform on the code.
        /// </summary>
        public enum AccessTypes
        {
            /// <summary>
            /// The code can be edited freely.
            /// </summary>
            ReadWrite,

            /// <summary>
            /// The code cannot be edited, but the user can load previous versions of the file.
            /// </summary>
            ReadOnlyWithHistory,

            /// <summary>
            /// The code can only be read. No advanced features are provided beyond syntax highlighting.
            /// </summary>
            ReadOnly
        }

        private AccessTypes accessType = AccessTypes.ReadWrite;

        /// <summary>
        /// Determines whether the text of the document can be edited by the user.
        /// </summary>
        public AccessTypes AccessType
        {
            get
            {
                return this.accessType;
            }

            set
            {
                this.accessType = value;
                this.EditorControl.IsReadOnly = value != AccessTypes.ReadWrite;

                if (value == AccessTypes.ReadOnly)
                {
                    this.StatusBar.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsVisible = false;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsVisible = false;
                }
                else if (value == AccessTypes.ReadWrite)
                {
                    this.StatusBar.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsVisible = true;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsVisible = true;
                }
                else
                {
                    this.StatusBar.FindControl<ToggleButton>("ToggleSettingsContainerButton").IsVisible = false;
                    this.StatusBar.FindControl<ToggleButton>("ToggleSaveHistoryContainerButton").IsVisible = true;
                }
            }
        }

        /// <summary>
        /// A unique identifier for the document being edited.
        /// </summary>
        public string Guid { get; private set; }

        /// <summary>
        /// The full path to the directory where the autosave file and the save history for the current document are kept.
        /// </summary>
        public string SaveDirectory { get; private set; }

        /// <summary>
        /// The full path to the autosave file.
        /// </summary>
        public string AutoSaveFile { get; private set; }

        /// <summary>
        /// A boolean value indicating whether a history of the saved versions of the document is kept.
        /// </summary>
        public bool KeepSaveHistory { get; internal set; } = true;

        /// <summary>
        /// A boolean value indicating whether syntax highlighting is enabled.
        /// </summary>
        public bool SyntaxHighlighting => this.EditorControl.SyntaxHighlighting;

        /// <summary>
        /// A boolean value indicating whether changed lines are highlighted on the left side of the control.
        /// </summary>
        public bool ShowLineChanges => this.EditorControl.ShowLineChanges;

        /// <summary>
        /// A boolean value indicating whether a summary of the changed lines, errors/warning, search results, breakpoints and the position of the caret should be shown over the vertical scrollbar.
        /// </summary>
        public bool ShowScrollbarOverview => this.EditorControl.ShowScrollbarOverview;

        /// <summary>
        /// The timeout between consecutive autosaves, in milliseconds.
        /// </summary>
        public int AutosaveInterval => this.AutoSaver.MillisecondsInterval;

        /// <summary>
        /// The timeout for updating the preview after the user stops typing, in milliseconds.
        /// </summary>
        public int PreviewTimeout => this.CompilationErrorChecker.MillisecondsInterval;

        /// <summary>
        /// Gets or sets the selected text span.
        /// </summary>
        public TextSpan Selection
        {
            get
            {
                return new TextSpan(EditorControl.SelectionStart, EditorControl.SelectionEnd - EditorControl.SelectionStart);
            }

            set
            {
                EditorControl.SetSelection(value.Start, value.Length);
            }
        }

        /// <summary>
        /// Create a new <see cref="Editor"/> instance.
        /// </summary>
        /// <param name="initialText">The initial text of the editor.</param>
        /// <param name="guid">A unique identifier for the document being edited. If this is <see langword="null"/>, a new <see cref="System.Guid"/> is generated. If the same identifier is used multiple times, the save history of the document will be available, even if the application has been closed between different sessions.</param>
        /// <param name="additionalShortcuts">Additional application-specific shortcuts (for display purposes only - you need to implement your own logic).</param>
        /// <returns>A fully initialised <see cref="Editor"/> instance.</returns>
        public static async Task<Editor> Create(string initialText = "", string guid = null, Shortcut[] additionalShortcuts = null)
        {
            if (string.IsNullOrEmpty(guid))
            {
                guid = System.Guid.NewGuid().ToString("N");
            }
            else
            {
                foreach (char c in Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()))
                {
                    if (guid.Contains(c))
                    {
                        throw new ArgumentException("The provided Guid \"" + guid + "\" is not valid!\nThe Guid must be a valid identifier for a path or a file.", nameof(guid));
                    }
                }
            }

            AsynchronousImageCache.SetExitEventHandler();

            Editor tbr = new Editor(false);
            await tbr.Initialize(initialText, guid, additionalShortcuts ?? new Shortcut[0]);
            return tbr;
        }

        /// <summary>
        /// Sets the text of the document.
        /// </summary>
        /// <param name="text">The new text of the document.</param>
        /// <returns>A <see cref="Task"/> that completes when the text has been updated.</returns>
        public async Task SetText(string text)
        {
            await EditorControl.SetText(text);
        }

        /// <summary>
        /// Sets the text of the document.
        /// </summary>
        /// <param name="text">The new text of the document.</param>
        /// <returns>A <see cref="Task"/> that completes when the text has been updated.</returns>
        public async Task SetText(SourceText text)
        {
            await EditorControl.SetText(text);
        }

        /// <summary>
        /// Add the current text of the document to the save history (if enabled) and invoke the <see cref="SaveRequested"/> event.
        /// </summary>
        public void Save()
        {
            string text = this.EditorControl.Text.ToString();

            if (KeepSaveHistory)
            {
                string autosaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly().GetName().Name);
                Directory.CreateDirectory(Path.Combine(autosaveDirectory, this.Guid));
                System.IO.File.WriteAllText(System.IO.Path.Combine(autosaveDirectory, this.Guid, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() + ".md"), text);
            }

            this.UpdateLastSavedDocument();
            if (this.SaveHistoryContainer.IsVisible)
            {
                this.SaveHistoryContainer.Refresh();
            }

            this.InvokeSaveRequested(new SaveEventArgs(text));
        }
    }

    /// <summary>
    /// A class to hold data for an event where the user has requested to save the document.
    /// </summary>
    public class SaveEventArgs : EventArgs
    {
        /// <summary>
        /// The text of the document to save.
        /// </summary>
        public string Text { get; }

        internal SaveEventArgs(string text) : base()
        {
            this.Text = text;
        }
    }

    /// <summary>
    /// A class to hold data for an event where the document preview has been rendered.
    /// </summary>
    public class PreviewRenderedEventArgs : EventArgs
    {
        /// <summary>
        /// The Markdown document that has been rendered.
        /// </summary>
        public Markdig.Syntax.MarkdownDocument Document { get; }

        internal PreviewRenderedEventArgs(Markdig.Syntax.MarkdownDocument document) : base()
        {
            this.Document = document;
        }
    }

    /// <summary>
    /// Represents a keyboard shortcut.
    /// </summary>
    public class Shortcut
    {
        /// <summary>
        /// The name of the action performed by the shortcut.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The keys that have to be pressed together to perform the action.
        /// </summary>
        public string[][] Shortcuts { get; }

        /// <summary>
        /// Creates a new <see cref="Shortcut"/> instance.
        /// </summary>
        /// <param name="name">The name of the action performed by the shortcut (e.g. "Copy").</param>
        /// <param name="shortcuts">The keys that have to be pressed together to perform the action (e.g. [ [ "Ctrl", "C" ], [ "Ctrl", "Ins" ] ] to specify that either <c>Ctrl+C</c> or <c>Ctrl+Ins</c> can be used. "Ctrl" will automatically be converted to "Cmd" on macOS.</param>
        public Shortcut(string name, string[][] shortcuts)
        {
            this.Name = name;
            this.Shortcuts = shortcuts;
        }
    }
}
