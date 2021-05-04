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

using Avalonia.Input;
using DiffPlex.Model;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MDEdit
{
    internal class InputHandler
    {
        private readonly Editor OwnerEditor;
        private readonly MarkdownSourceEditorControl EditorControl;
        private bool IsCtrlPressed = false;

        public InputHandler(Editor owner, MarkdownSourceEditorControl editorControl)
        {
            OwnerEditor = owner;
            EditorControl = editorControl;
            EditorControl.TextChanged += OnDocumentChanged;

            EditorControl.OnPreviewKeyDown = OnPreviewKeyDown;
            EditorControl.OnPreviewKeyUp = OnPreviewKeyUp;

            EditorControl.OnPreviewPointerWheelChanged += OnWheelChanged;
        }

        private void OnWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (IsCtrlPressed)
            {
                if (e.Delta.Y > 0)
                {
                    OwnerEditor.StatusBar.IncreaseFontSize();
                }
                else
                {
                    OwnerEditor.StatusBar.DecreaseFontSize();
                }
                e.Handled = true;
            }
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            OwnerEditor.CompilationErrorChecker.LastEditHandle.Set();
        }

        public async Task OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                IsCtrlPressed = true;
            }
            else if (e.Key == Key.S && e.KeyModifiers == Utils.ControlCmdModifier)
            {
                this.OwnerEditor.Save();
            }
            else if ((e.Key == Key.L && e.KeyModifiers == Utils.ControlCmdModifier) || e.Key == Key.F5)
            {
                e.Handled = true;
                OwnerEditor.PreviewPanel.Document = null;
                await OwnerEditor.CompilationErrorChecker.UpdatePreview();
            }
        }

        public Task OnPreviewKeyUp(KeyEventArgs e)
        {
            string fullSource = OwnerEditor.Text;

            SourceText currentSourceText = SourceText.From(fullSource);

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                IsCtrlPressed = false;
            }

            if (EditorControl.ShowLineChanges)
            {
                DiffResult diffResultFromLastSaved = OwnerEditor.Differ.CreateLineDiffs(OwnerEditor.LastSavedText, fullSource, false);
                IEnumerable<int> changesFromLastSaved = (from el in diffResultFromLastSaved.DiffBlocks select Enumerable.Range(el.InsertStartB, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                DiffResult diffResultFromOriginal = OwnerEditor.Differ.CreateLineDiffs(OwnerEditor.OriginalText, fullSource, false);
                IEnumerable<int> changesFromOriginal = (from el in diffResultFromOriginal.DiffBlocks select Enumerable.Range(el.InsertStartB, Math.Max(1, el.InsertCountB))).Aggregate(Enumerable.Empty<int>(), (a, b) => a.Concat(b));

                OwnerEditor.SetLineDiff(changesFromLastSaved, changesFromOriginal);
            }

            return Task.CompletedTask;
        }
    }

}
