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
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace MDEdit
{
    internal class MarkdownSourceEditorText : Control, ILogicalScrollable
    {
        private MarkdownSourceEditor Editor { get; }

        internal MarkdownSourceEditorText(MarkdownSourceEditor editor)
        {
            this.Editor = editor;
            this.IsHitTestVisible = false;
        }

        private bool _visible = true;

        private void CaretTimerTick(object sender, EventArgs e)
        {
            _visible = !_visible;
            this.InvalidateVisual();
        }

        public bool CanHorizontallyScroll { get => ((ILogicalScrollable)Editor).CanHorizontallyScroll; set { } }
        public bool CanVerticallyScroll { get => ((ILogicalScrollable)Editor).CanVerticallyScroll; set { } }

        public bool IsLogicalScrollEnabled => ((ILogicalScrollable)Editor).IsLogicalScrollEnabled;

        public Size ScrollSize => ((ILogicalScrollable)Editor).ScrollSize;

        public Size PageScrollSize => ((ILogicalScrollable)Editor).PageScrollSize;

        public Size Extent => ((IScrollable)Editor).Extent;

        public Vector Offset { get => ((IScrollable)Editor).Offset; set { } }

        public Size Viewport => ((IScrollable)Editor).Viewport;

        public event EventHandler ScrollInvalidated
        {
            add
            {

            }

            remove
            {

            }
        }

        public bool BringIntoView(IControl target, Rect targetRect)
        {
            return false;
        }

        public IControl GetControlInDirection(NavigationDirection direction, IControl from)
        {
            return null;
        }

        public void RaiseScrollInvalidated(EventArgs e)
        {

        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            using (context.PushClip(new Rect(25 + this.Editor.LineNumbersWidth, 0, this.Bounds.Width - (25 + this.Editor.LineNumbersWidth), this.Bounds.Height)))
            {
                double lineHeight = this.Editor.FontSize * this.Editor.LineSpacing;

                int firstLine = Math.Max(0, (int)Math.Floor(this.Offset.Y / lineHeight));
                int lastLine = Math.Min(this.Editor.Text.Lines.Count - 1, (int)Math.Floor((this.Offset.Y + this.Viewport.Height) / lineHeight));

                int firstColumn = Math.Max(0, (int)Math.Floor(this.Offset.X / this.Editor.CharacterWidth));
                int lastColumn = (int)Math.Floor((this.Offset.X + this.Viewport.Width) / this.Editor.CharacterWidth);

                int firstCharacter = this.Editor.Text.Lines.GetPosition(new LinePosition(firstLine, Math.Min(firstColumn, this.Editor.Text.Lines[firstLine].End)));
                int lastCharacter = this.Editor.Text.Lines.GetPosition(new LinePosition(lastLine, Math.Min(lastColumn, this.Editor.Text.Lines[lastLine].End)));

                if (this.Editor.DocumentNeedsParsing)
                {
                    this.Editor.ParsedDocument = Markdig.Markdown.Parse(this.Editor.Text.ToString(), this.Editor.ParserPipeline);
                    this.Editor.DocumentNeedsParsing = false;

                    if (!this.Editor.EverRenderedPreview)
                    {
                        _ = this.Editor.FindLogicalAncestorOfType<Editor>().CompilationErrorChecker.UpdatePreview();
                        this.Editor.EverRenderedPreview = true;
                    }
                }

                MarkdownDocument document = this.Editor.ParsedDocument;

                List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges = new List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)>();

                if (this.Editor.SyntaxHighlighting)
                {
                    foreach (Block block in document)
                    {
                        AddColorRanges(colorRanges, block, firstCharacter, lastCharacter);
                    }
                }

                for (int i = firstLine; i <= lastLine; i++)
                {
                    DrawLine(context, i, firstColumn, lastColumn, lineHeight, colorRanges);
                }
            }
        }

        private void AddColorRanges(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, Block block, int firstCharacter, int lastCharacter)
        {
            ImmutableList<Color> startingColors = ImmutableList<Color>.Empty;

            if (block.Span.Start <= lastCharacter && firstCharacter <= block.Span.End || block is LinkReferenceDefinitionGroup)
            {
                AddBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors, false, false, false, 0);
            }
        }

        private void AddBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, Block block, int firstCharacter, int lastCharacter, ImmutableList<Color> startingColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            if (block is LeafBlock leaf)
            {
                if (block is HeadingBlock heading)
                {
                    AddHeadingBlock(colorRanges, heading, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (block is ParagraphBlock paragraph)
                {
                    AddParagraphBlock(colorRanges, paragraph, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (block is CodeBlock code)
                {
                    if (block is Markdig.Extensions.Mathematics.MathBlock math)
                    {
                        AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors.Add(SyntaxHighlightingColors.MathColor), currentBold, currentItalic, currentUnderline, currentScriptPos);
                    }
                    else if (leaf is FencedCodeBlock fenced)
                    {
                        AddFencedCodeBlock(colorRanges, fenced, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                    }
                    else
                    {
                        AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors.Add(SyntaxHighlightingColors.CodeColor), currentBold, currentItalic, currentUnderline, currentScriptPos);
                    }
                }
                else if (leaf is HtmlBlock html)
                {
                    AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors.Add(SyntaxHighlightingColors.HTMLColor), currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (leaf is ThematicBreakBlock thematicBreak)
                {
                    AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors.Add(SyntaxHighlightingColors.ThematicBreakColor), currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (leaf is LinkReferenceDefinition link)
                {
                    // Nothing to do (these are only found within LinkReferenceDefinitionGroup containers)
                }
                else
                {
                    AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
            else if (block is ContainerBlock container)
            {
                if (block is ListBlock list)
                {
                    AddListBlock(colorRanges, list, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (block is ListItemBlock listItem)
                {
                    AddListItemBlock(colorRanges, listItem, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (block is QuoteBlock quote)
                {
                    AddQuoteBlock(colorRanges, quote, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (block is LinkReferenceDefinitionGroup linkGroup)
                {
                    AddLinkReferenceDefinitionGroup(colorRanges, linkGroup, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (block is Table table)
                {
                    AddTableBlock(colorRanges, table, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else
                {
                    AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
            else if (block is BlankLineBlock)
            {
                // Nothing to render here
            }
            else
            {
                AddPlainBlock(colorRanges, block, firstCharacter, lastCharacter, startingColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
            }
        }

        private void DrawLine(DrawingContext context, int line, int firstColumn, int lastColumn, double lineHeight, List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges)
        {
            TextLine tLine = this.Editor.Text.Lines[line];

            int length = tLine.End - tLine.Start;

            if (length >= firstColumn)
            {
                int start = tLine.Start + firstColumn;
                int end = Math.Min(tLine.End, tLine.Start + lastColumn + 1);

                TextSpan span = new TextSpan(start, end - start);

                if (Editor.SyntaxHighlighting && colorRanges.Count > 0)
                {
                    var cons = Consolidate(colorRanges, start, end);

                    foreach ((int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos) range in cons)
                    {
                        Typeface tf = range.bold ? (range.italic ? this.Editor.BoldItalicTypeface : this.Editor.BoldTypeface) : (range.italic ? this.Editor.ItalicTypeface : this.Editor.Typeface);

                        Avalonia.Media.FormattedText formattedText = new Avalonia.Media.FormattedText() { Text = this.Editor.Text.ToString(new TextSpan(range.start, range.end - range.start)), Typeface = tf, FontSize = this.Editor.FontSize, TextWrapping = TextWrapping.NoWrap };

                        if (range.scriptPos > 0)
                        {
                            using (context.PushPreTransform(Matrix.CreateScale(1, 0.85) * Matrix.CreateTranslation((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y)))
                            {
                                if (range.italic)
                                {
                                    using (context.PushPreTransform(Matrix.CreateScale(1.0219917181930989299330381030104, 1)))
                                    {
                                        context.DrawText(range.color, new Point(0, 0), formattedText);
                                    }
                                }
                                else
                                {
                                    context.DrawText(range.color, new Point(0, 0), formattedText);
                                }

                                if (range.underline)
                                {
                                    context.DrawLine(new Pen(range.color) { Thickness = 1 + (this.Editor.FontSize - 14) / 11 }, new Point(0, lineHeight), new Point(this.Editor.CharacterWidth * (range.end - range.start), lineHeight));
                                }
                            }
                        }
                        else if (range.scriptPos < 0)
                        {
                            using (context.PushPreTransform(Matrix.CreateScale(1, 0.85) * Matrix.CreateTranslation((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y + lineHeight * 0.15)))
                            {
                                if (range.italic)
                                {
                                    using (context.PushPreTransform(Matrix.CreateScale(1.0219917181930989299330381030104, 1)))
                                    {
                                        context.DrawText(range.color, new Point(0, 0), formattedText);
                                    }
                                }
                                else
                                {
                                    context.DrawText(range.color, new Point(0, 0), formattedText);
                                }

                                if (range.underline)
                                {
                                    context.DrawLine(new Pen(range.color) { Thickness = 1 + (this.Editor.FontSize - 14) / 11 }, new Point(0, lineHeight), new Point(this.Editor.CharacterWidth * (range.end - range.start), lineHeight));
                                }
                            }
                        }
                        else
                        {
                            if (range.italic)
                            {
                                using (context.PushPreTransform(Matrix.CreateScale(1.0219917181930989299330381030104, 1) * Matrix.CreateTranslation((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y)))
                                {
                                    context.DrawText(range.color, new Point(0, 0), formattedText);
                                }
                            }
                            else
                            {
                                context.DrawText(range.color, new Point((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y), formattedText);
                            }

                            if (range.underline)
                            {
                                context.DrawLine(new Pen(range.color) { Thickness = 1 + (this.Editor.FontSize - 14) / 11 }, new Point((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y + lineHeight), new Point((range.start - start) * this.Editor.CharacterWidth - this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth + this.Editor.CharacterWidth * (range.end - range.start), line * lineHeight - this.Offset.Y + lineHeight));
                            }
                        }
                    }
                }
                else
                {
                    string text = this.Editor.Text.ToString(span);
                    Avalonia.Media.FormattedText formattedText = new Avalonia.Media.FormattedText() { Text = text, Typeface = this.Editor.Typeface, FontSize = this.Editor.FontSize, TextWrapping = TextWrapping.NoWrap };
                    context.DrawText(Brushes.Black, new Point(-this.Offset.X % this.Editor.CharacterWidth + 25 + this.Editor.LineNumbersWidth, line * lineHeight - this.Offset.Y), formattedText);
                }
            }
        }

        private IEnumerable<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> Consolidate(IEnumerable<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> ranges, int start, int end)
        {
            int currPos = start;

            List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> tbr = new List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)>();

            foreach ((int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos) range in ranges)
            {
                if (range.start < end && start < range.end)
                {
                    if (currPos < 0)
                    {
                        currPos = range.start;
                    }

                    int rangeStart = Math.Max(range.start, currPos);
                    int rangeEnd = Math.Min(range.end, end);
                    rangeStart = Math.Min(rangeStart, rangeEnd);

                    List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> toBeReadded = new List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)>();

                    if (currPos < rangeStart)
                    {
                        tbr.Add((currPos, rangeStart, new SolidColorBrush(SyntaxHighlightingColors.ErrorColor), false, false, false, 0));
                    }
                    else if (currPos > range.start)
                    {
                        currPos = Math.Max(start, range.start);
                        rangeStart = currPos;

                        for (int i = tbr.Count - 1; i >= 0; i--)
                        {
                            if (tbr[i].start > currPos)
                            {
                                if (tbr[i].end > rangeEnd && Math.Max(tbr[i].start, rangeEnd) < tbr[i].end)
                                {
                                    toBeReadded.Add((Math.Max(tbr[i].start, rangeEnd), tbr[i].end, tbr[i].color, tbr[i].bold, tbr[i].italic, tbr[i].underline, tbr[i].scriptPos));
                                }

                                tbr.RemoveAt(i);
                            }
                            else
                            {
                                if (tbr[i].end > rangeEnd)
                                {
                                    toBeReadded.Add((rangeEnd, tbr[i].end, tbr[i].color, tbr[i].bold, tbr[i].italic, tbr[i].underline, tbr[i].scriptPos));
                                }

                                if (tbr[i].end > currPos)
                                {
                                    if (tbr[i].start < currPos)
                                    {
                                        tbr[i] = (tbr[i].start, currPos, tbr[i].color, tbr[i].bold, tbr[i].italic, tbr[i].underline, tbr[i].scriptPos);
                                    }
                                    else
                                    {
                                        tbr.RemoveAt(i);
                                    }
                                }

                                //break;
                            }
                        }
                    }

                    if (rangeStart < rangeEnd)
                    {
                        tbr.Add((rangeStart, rangeEnd, range.color, range.bold, range.italic, range.underline, range.scriptPos));
                        currPos = rangeEnd;
                    }

                    for (int i = toBeReadded.Count - 1; i >= 0; i--)
                    {
                        tbr.Add(toBeReadded[i]);
                        currPos = toBeReadded[i].end;
                    }
                }
            }

            currPos = start;
            int index = 0;

            while (currPos < end && index < tbr.Count)
            {
                if (tbr[index].start > currPos)
                {
                    tbr.Add((currPos, tbr[index].start, new SolidColorBrush(SyntaxHighlightingColors.ErrorColor), false, false, false, 0));
                }
                else
                {
                    currPos = tbr[index].end;
                    index++;
                }
            }

            if (currPos < end)
            {
                tbr.Add((currPos, end, new SolidColorBrush(SyntaxHighlightingColors.ErrorColor), false, false, false, 0));
            }

            return tbr;
        }

        private Brush GetBrush(ImmutableList<Color> colors)
        {
            HashSet<Color> actualColors = new HashSet<Color>(colors);

            if (actualColors.Count == 0)
            {
                return new SolidColorBrush(Colors.Black);
            }
            else if (actualColors.Count == 1)
            {
                return new SolidColorBrush(colors[0]);
            }
            else
            {
                GradientBrush gradient = new LinearGradientBrush() { SpreadMethod = GradientSpreadMethod.Repeat, StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = new RelativePoint(colors.Count * 6, colors.Count * 6 * 1, RelativeUnit.Absolute) };

                int i = 0;

                foreach (Color color in actualColors)
                {
                    gradient.GradientStops.Add(new GradientStop(color, (double)i / actualColors.Count));
                    gradient.GradientStops.Add(new GradientStop(color, (double)(i + 1) / actualColors.Count));
                    i++;
                }

                return gradient;
            }
        }


        private void AddHeadingBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, HeadingBlock heading, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            ImmutableList<Color> myColors = currentColors.Add(SyntaxHighlightingColors.HeadingColor);

            currentBold |= true;

            colorRanges.Add((heading.Span.Start, heading.Span.End + 1, GetBrush(myColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

            foreach (Inline inline in heading.Inline)
            {
                if (inline.Span.Start <= lastCharacter && firstCharacter <= inline.Span.End)
                {
                    AddInline(colorRanges, inline, firstCharacter, lastCharacter, myColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
        }

        private void AddParagraphBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, ParagraphBlock paragraph, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            ImmutableList<Color> myColors = currentColors;

            colorRanges.Add((paragraph.Span.Start, paragraph.Span.End + 1, GetBrush(myColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

            foreach (Inline inline in paragraph.Inline)
            {
                if (inline.Span.Start <= lastCharacter && firstCharacter <= inline.Span.End)
                {
                    AddInline(colorRanges, inline, firstCharacter, lastCharacter, myColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
        }

        private void AddPlainBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, Block block, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            colorRanges.Add((block.Span.Start, block.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));
        }

        private void AddInline(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, Inline inline, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            if (inline is LeafInline)
            {
                if (inline is AutolinkInline autoLink)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LinkColor)), currentBold, currentItalic, currentUnderline | true, currentScriptPos));
                }
                else if (inline is CodeInline code)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.CodeColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
                }
                else if (inline is HtmlEntityInline htmlEntity)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.HTMLEntityColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
                }
                else if (inline is HtmlInline html)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.HTMLColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
                }
                else if (inline is LineBreakInline lineBreak)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LineBreakColor)), currentBold | true, currentItalic, currentUnderline, currentScriptPos));
                }
                else if (inline is LiteralInline literal)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));
                }
                else if (inline is Markdig.Extensions.Mathematics.MathInline math)
                {
                    colorRanges.Add((math.Span.Start, math.Span.Start + math.Content.Length + math.DelimiterCount * 2, GetBrush(currentColors.Add(SyntaxHighlightingColors.MathColor)), currentBold, currentItalic | true, currentUnderline, currentScriptPos));
                }
                else if (inline is Markdig.Extensions.SmartyPants.SmartyPant smartyPant)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.QuoteColor)), currentBold, currentItalic | true, currentUnderline, currentScriptPos));
                }
                else if (inline is Markdig.Extensions.TaskLists.TaskList taskList)
                {
                    if (taskList.Checked)
                    {
                        colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.TaskCompletedColor)), currentBold | true, currentItalic, currentUnderline, currentScriptPos));
                    }
                    else
                    {
                        colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.TaskIncompleteColor)), currentBold | true, currentItalic, currentUnderline, currentScriptPos));
                    }
                }
                else
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));
                }
            }
            else if (inline is ContainerInline)
            {
                if (inline is DelimiterInline)
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors), currentBold | true, currentItalic, currentUnderline, currentScriptPos));
                }
                else if (inline is EmphasisInline emphasis)
                {
                    AddEmphasisInline(colorRanges, emphasis, firstCharacter, lastCharacter, currentColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
                else if (inline is LinkInline link)
                {
                    if (link.IsImage)
                    {
                        colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

                        if (link.LabelSpan != null)
                        {
                            colorRanges.Add((link.LabelSpan.Start, link.LabelSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.ImageColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
                        }

                        if (link.UrlSpan != null)
                        {
                            colorRanges.Add((link.UrlSpan.Start, link.UrlSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.ImageColor)), currentBold, currentItalic, currentUnderline | true, currentScriptPos));
                        }
                    }
                    else
                    {
                        colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

                        if (link.LabelSpan != null)
                        {
                            colorRanges.Add((link.LabelSpan.Start, link.LabelSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LinkColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
                        }

                        if (link.UrlSpan != null)
                        {
                            colorRanges.Add((link.UrlSpan.Start, link.UrlSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LinkColor)), currentBold, currentItalic, currentUnderline | true, currentScriptPos));
                        }
                    }
                }
                else
                {
                    colorRanges.Add((inline.Span.Start, inline.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));
                }
            }
        }

        private void AddEmphasisInline(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, EmphasisInline emphasis, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            switch (emphasis.DelimiterChar)
            {
                case '*':
                case '_':
                    if (emphasis.DelimiterCount == 2)
                    {
                        currentBold |= true;
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                    }
                    else if (emphasis.DelimiterCount == 3)
                    {
                        currentBold |= true;
                        currentItalic |= true;
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                    }
                    else
                    {
                        currentItalic |= true;
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                    }
                    break;
                case '"':
                    if (emphasis.DelimiterCount == 2)
                    {
                        currentItalic |= true;
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                    }
                    break;
                case '~':
                    if (emphasis.DelimiterCount == 1)
                    {
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                        currentScriptPos = -1;
                    }
                    else
                    {
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                        currentUnderline |= true;
                    }
                    break;
                case '^':
                    if (emphasis.DelimiterCount == 1)
                    {
                        currentColors = currentColors.Add(SyntaxHighlightingColors.EmphasisColor);
                        currentScriptPos = 1;
                    }
                    break;
                case '+':
                    currentColors = currentColors.Add(SyntaxHighlightingColors.InsertedColor);
                    break;
                case '=':
                    currentColors = currentColors.Add(SyntaxHighlightingColors.RemovedColor);
                    break;
            }

            colorRanges.Add((emphasis.Span.Start, emphasis.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

            foreach (Inline innerInline in emphasis)
            {
                if (innerInline.Span.Start <= lastCharacter && firstCharacter <= innerInline.Span.End)
                {
                    AddInline(colorRanges, innerInline, firstCharacter, lastCharacter, currentColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
        }

        private void AddLinkReferenceDefinitionGroup(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, LinkReferenceDefinitionGroup linkGroup, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            foreach (Block block in linkGroup)
            {
                if (block is LinkReferenceDefinition link)
                {
                    if (link.Span.End >= link.Span.Start)
                    {
                        if (link.Span.Start <= lastCharacter && firstCharacter <= link.Span.End)
                        {
                            colorRanges.Add((link.Span.Start, link.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

                            if (link.LabelSpan.End >= link.LabelSpan.Start)
                            {
                                colorRanges.Add((link.LabelSpan.Start, link.LabelSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LinkColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
                            }

                            if (link.TitleSpan.End >= link.TitleSpan.Start)
                            {
                                colorRanges.Add((link.TitleSpan.Start, link.TitleSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LinkColor)), currentBold | true, currentItalic, currentUnderline, currentScriptPos));
                            }

                            if (link.UrlSpan.End >= link.UrlSpan.Start)
                            {
                                colorRanges.Add((link.UrlSpan.Start, link.UrlSpan.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.LinkColor)), currentBold, currentItalic, currentUnderline | true, currentScriptPos));
                            }
                        }
                    }
                }
            }
        }

        private void AddListBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, ListBlock list, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            foreach (Block block in list)
            {
                if (block.Span.Start <= lastCharacter && firstCharacter <= block.Span.End || block is LinkReferenceDefinitionGroup)
                {
                    AddBlock(colorRanges, block, firstCharacter, lastCharacter, currentColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
        }

        private void AddListItemBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, ListItemBlock listItem, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            colorRanges.Add((listItem.Span.Start, listItem.Span.Start + listItem.ColumnWidth + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.BulletColor)), currentBold | true, currentItalic, currentUnderline, currentScriptPos));

            foreach (Block block in listItem)
            {
                if (block.Span.Start <= lastCharacter && firstCharacter <= block.Span.End || block is LinkReferenceDefinitionGroup)
                {
                    AddBlock(colorRanges, block, firstCharacter, lastCharacter, currentColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
        }

        private void AddQuoteBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, QuoteBlock quote, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            currentColors = currentColors.Add(SyntaxHighlightingColors.QuoteColor);

            colorRanges.Add((quote.Span.Start, quote.Span.End + 1, GetBrush(currentColors), currentBold, currentItalic, currentUnderline, currentScriptPos));

            foreach (Block block in quote)
            {
                if (block.Span.Start <= lastCharacter && firstCharacter <= block.Span.End || block is LinkReferenceDefinitionGroup)
                {
                    AddBlock(colorRanges, block, firstCharacter, lastCharacter, currentColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                }
            }
        }

        private void AddTableBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, Table table, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            foreach (TableRow row in table)
            {
                foreach (TableCell cell in row)
                {
                    foreach (Block block in cell)
                    {
                        if (block.Span.Start <= lastCharacter && firstCharacter <= block.Span.End || block is LinkReferenceDefinitionGroup)
                        {
                            AddBlock(colorRanges, block, firstCharacter, lastCharacter, currentColors, currentBold, currentItalic, currentUnderline, currentScriptPos);
                        }
                    }
                }
            }
        }

        private void AddFencedCodeBlock(List<(int start, int end, Brush color, bool bold, bool italic, bool underline, int scriptPos)> colorRanges, FencedCodeBlock codeBlock, int firstCharacter, int lastCharacter, ImmutableList<Color> currentColors, bool currentBold, bool currentItalic, bool currentUnderline, int currentScriptPos)
        {
            string info = codeBlock.Info;

            if (string.IsNullOrEmpty(info) || codeBlock.Lines.Count == 0)
            {
                AddPlainBlock(colorRanges, codeBlock, firstCharacter, lastCharacter, currentColors.Add(SyntaxHighlightingColors.CodeColor), currentBold, currentItalic, currentUnderline, currentScriptPos);
                return;
            }

            StringBuilder code = new StringBuilder();

            List<int> lineStarts = new List<int>();
            int codeEnd = -1;

            foreach (StringLine line in codeBlock.Lines)
            {
                if (line.Slice.End >= line.Slice.Start && line.Slice.End > 0)
                {
                    code.Append(line.ToString());
                    code.Append('\n');
                    lineStarts.Add(line.Position);
                    codeEnd = line.Position + line.Slice.Length;
                }
            }

            List<List<VectSharp.Markdown.FormattedString>> lines = VectSharp.Markdown.SyntaxHighlighter.GetSyntaxHighlightedLines(code.ToString(0, Math.Max(0, code.Length - 1)), info);

            if (lines == null || lines.Count == 0 || code.Length == 0)
            {
                AddPlainBlock(colorRanges, codeBlock, firstCharacter, lastCharacter, currentColors.Add(SyntaxHighlightingColors.CodeColor), currentBold, currentItalic, currentUnderline, currentScriptPos);
                return;
            }

            colorRanges.Add((codeBlock.Span.Start, lineStarts[0], GetBrush(currentColors.Add(SyntaxHighlightingColors.CodeColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));
            colorRanges.Add((codeEnd, codeBlock.Span.End + 1, GetBrush(currentColors.Add(SyntaxHighlightingColors.CodeColor)), currentBold, currentItalic, currentUnderline, currentScriptPos));

            for (int i = 0; i < lines.Count; i++)
            {
                int currPos = lineStarts[i];
                foreach (VectSharp.Markdown.FormattedString element in lines[i])
                {
                    colorRanges.Add((currPos, currPos + element.Text.Length, GetBrush(currentColors.Add(FromVectSharp(element.Colour))), currentBold | element.IsBold, currentItalic | element.IsItalic, currentUnderline, currentScriptPos));
                    currPos += element.Text.Length;
                }
            }
        }

        private static Color FromVectSharp(VectSharp.Colour col)
        {
            return Color.FromArgb((byte)(col.A * 255), (byte)(col.R * 255), (byte)(col.G * 255), (byte)(col.B * 255));
        }
    }
}
