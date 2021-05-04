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
using Avalonia.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace MDEdit
{
    internal class PreviewUpdater
    {
        public Editor Editor { get; }
        public EventWaitHandle ExitHandle { get; }
        public EventWaitHandle LastEditHandle { get; }
        public int MillisecondsInterval { get; set; } = 250;
        
        public bool IsRunning { get; private set; } = false;

        private Thread LoopThread;
        private readonly object StatusObject = new object();

        public static PreviewUpdater Attach(Editor editor)
        {
            PreviewUpdater checker = new PreviewUpdater(editor);

            editor.DetachedFromLogicalTree += (s, e) =>
            {
                checker.ExitHandle.Set();
                checker.LoopThread.Join();
            };

            return checker;
        }

        public void Resume()
        {
            lock (StatusObject)
            {
                ExitHandle.Reset();
                LoopThread.Join();
                LoopThread = new Thread(UpdaterLoop);
                LoopThread.Start();
            }
        }

        public void Stop()
        {
            lock (StatusObject)
            {
                ExitHandle.Set();
                LoopThread.Join();
            }
        }

        private PreviewUpdater(Editor editor)
        {
            this.Editor = editor;
            this.ExitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            this.LastEditHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

            LoopThread = new Thread(UpdaterLoop);
            LoopThread.Start();
        }

        private async void UpdaterLoop()
        {
            EventWaitHandle[] handles = new EventWaitHandle[] { LastEditHandle, ExitHandle };

            bool editSinceLastCompilation = true;

            IsRunning = true;

            while (true)
            {
                int handle = EventWaitHandle.WaitAny(handles, MillisecondsInterval);

                if (handle == 0)
                {
                    LastEditHandle.Reset();
                    editSinceLastCompilation = true;
                }
                else if (handle == 1)
                {
                    break;
                }
                else
                {
                    if (editSinceLastCompilation && Editor.AccessType != Editor.AccessTypes.ReadOnly)
                    {
                        await UpdatePreview();
                        editSinceLastCompilation = false;
                    }
                }
            }

            IsRunning = false;
        }

        public async Task UpdatePreview()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Avalonia.Vector prevOffset = this.Editor.PreviewPanel.FindControl<ScrollViewer>("ScrollViewer").Offset;
                this.Editor.PreviewPanel.Document = this.Editor.EditorControl.ParsedDocument;
                this.Editor.PreviewPanel.FindControl<ScrollViewer>("ScrollViewer").Offset = prevOffset;
                this.Editor.InvokePreviewRendered(new PreviewRenderedEventArgs(this.Editor.EditorControl.ParsedDocument));
            });
        }
    }
}
