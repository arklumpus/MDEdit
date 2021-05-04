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
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MDEdit
{
    internal static class Utils
    {
        public static KeyModifiers ControlCmdModifier { get; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? KeyModifiers.Meta : KeyModifiers.Control;

        internal const string Tab = "    ";

        public static List<TextSpan> Join(this IEnumerable<TextSpan> spans)
        {
            List<TextSpan> tbr = new List<TextSpan>();
            foreach (TextSpan span in spans)
            {
                bool found = false;

                for (int i = 0; i < tbr.Count; i++)
                {
                    if (tbr[i].IntersectsWith(span) || tbr[i].End + 1 == span.Start || tbr[i].Start == span.End + 1)
                    {
                        int start = Math.Min(tbr[i].Start, span.Start);
                        int end = Math.Max(tbr[i].End, span.End);

                        tbr[i] = new TextSpan(start, end - start);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tbr.Add(span);
                }
            }

            return tbr;
        }

        public static List<(TextSpan, List<T>)> Join<T>(this IEnumerable<(TextSpan, T)> spans)
        {
            List<(TextSpan, List<T>)> tbr = new List<(TextSpan, List<T>)>();
            foreach ((TextSpan span, T diag) in spans)
            {
                bool found = false;

                for (int i = 0; i < tbr.Count; i++)
                {
                    if (tbr[i].Item1.IntersectsWith(span))
                    {
                        int start = Math.Min(tbr[i].Item1.Start, span.Start);
                        int end = Math.Max(tbr[i].Item1.End, span.End);

                        tbr[i].Item2.Add(diag);

                        tbr[i] = (new TextSpan(start, end - start), tbr[i].Item2);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tbr.Add((span, new List<T>() { diag }));
                }
            }

            return tbr;
        }

     
        //Adapted from https://stackoverflow.com/questions/2641326/finding-all-positions-of-substring-in-a-larger-string-in-c-sharp
        public static IEnumerable<int> AllIndicesOf(this string text, string pattern, bool caseInsensitive = false)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentNullException(nameof(pattern));
            }
            return Kmp(text, pattern, caseInsensitive);
        }

        private static IEnumerable<int> Kmp(string text, string pattern, bool caseInsensitive)
        {
            int M = pattern.Length;
            int N = text.Length;

            int[] lps = LongestPrefixSuffix(pattern, caseInsensitive);
            int i = 0, j = 0;

            while (i < N)
            {
                if (pattern[j].IsEqual(text[i], caseInsensitive))
                {
                    j++;
                    i++;
                }
                if (j == M)
                {
                    yield return i - j;
                    j = lps[j - 1];
                }

                else if (i < N && !pattern[j].IsEqual(text[i], caseInsensitive))
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        private static int[] LongestPrefixSuffix(string pattern, bool caseInsensitive)
        {
            int[] lps = new int[pattern.Length];
            int length = 0;
            int i = 1;

            while (i < pattern.Length)
            {
                if (pattern[i].IsEqual(pattern[length], caseInsensitive))
                {
                    length++;
                    lps[i] = length;
                    i++;
                }
                else
                {
                    if (length != 0)
                    {
                        length = lps[length - 1];
                    }
                    else
                    {
                        lps[i] = length;
                        i++;
                    }
                }
            }
            return lps;
        }

        private static bool IsEqual(this char char1, char char2, bool caseInsensitive)
        {
            return char1 == char2 || (caseInsensitive && char.ToUpperInvariant(char1) == char.ToUpperInvariant(char2));
        }
    }

    internal static class Extensions
    {
        public static T? FirstOrNull<T>(this IEnumerable<T> list) where T : struct
        {
            foreach (T item in list)
            {
                return item;
            }
            return null;
        }

        public static T? LastOrNull<T>(this IEnumerable<T> list) where T : struct
        {
            if (list.Any())
            {
                return list.Last();
            }
            else
            {
                return null;
            }
        }

        public static bool IsNavigation(this Key key)
        {
            switch (key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Left:
                case Key.Right:
                case Key.PageUp:
                case Key.PageDown:
                case Key.Home:
                case Key.End:
                    return true;

                default:
                    return false;
            }
        }

        public static IEnumerable<LinePositionSpan> ToLinePositionSpans(this TextSpan span, SourceText text)
        {
            LinePositionSpan lineSpan = text.Lines.GetLinePositionSpan(span);

            while (lineSpan.Start.Line != lineSpan.End.Line)
            {
                yield return new LinePositionSpan(lineSpan.Start, new LinePosition(lineSpan.Start.Line, Math.Min(text.Lines[lineSpan.Start.Line].Span.Length + 1, text.Lines[lineSpan.Start.Line].SpanIncludingLineBreak.Length)));
                lineSpan = new LinePositionSpan(new LinePosition(lineSpan.Start.Line + 1, 0), lineSpan.End);
            }

            yield return lineSpan;
        }

        public static TextSpan? ApplyChanges(this TextSpan span, IEnumerable<TextChange> changes)
        {
            int start = span.Start;
            int end = span.End;

            foreach (TextChange change in changes)
            {
                start = ApplyChange(start, change);
                end = ApplyChange(end, change);
            }

            if (start >= 0 && end >= 0 && end >= start)
            {
                return new TextSpan(start, end - start);
            }
            else
            {
                return null;
            }
        }

        public static int ApplyChange(int position, TextChange change)
        {
            if (position <= change.Span.Start)
            {
                return position;
            }
            else if (position >= change.Span.End)
            {
                return position + change.Span.Length - change.NewText.Length;
            }
            else
            {
                return -1;
            }
        }
    }
}
