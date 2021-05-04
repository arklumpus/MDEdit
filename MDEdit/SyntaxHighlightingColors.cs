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

using Avalonia.Media;

namespace MDEdit
{
    internal static class SyntaxHighlightingColors
    {
        public static readonly Color HeadingColor = Color.FromRgb(166, 55, 0);
        public static readonly Color HTMLEntityColor = Color.FromRgb(75, 152, 220);
        public static readonly Color HTMLColor = Color.FromRgb(0, 158, 115);
        public static readonly Color LineBreakColor = Color.FromRgb(230, 159, 0);
        public static readonly Color LinkColor = Color.FromRgb(0, 114, 178);
        public static readonly Color ImageColor = Color.FromRgb(0, 78, 138);
        public static readonly Color CodeColor = Color.FromRgb(213, 94, 0);
        public static readonly Color MathColor = Color.FromRgb(213, 94, 0);
        public static readonly Color QuoteColor = Color.FromRgb(120, 120, 120);
        public static readonly Color InsertedColor = Color.FromRgb(0, 158, 115);
        public static readonly Color RemovedColor = Color.FromRgb(213, 94, 0);
        public static readonly Color EmphasisColor = Color.FromRgb(0, 78, 138);
        public static readonly Color ThematicBreakColor = Color.FromRgb(180, 180, 180);
        public static readonly Color ErrorColor = Color.FromRgb(0, 0, 0);
        public static readonly Color BulletColor = Color.FromRgb(0, 114, 178);
        public static readonly Color TaskCompletedColor = Color.FromRgb(0, 158, 115);
        public static readonly Color TaskIncompleteColor = Color.FromRgb(213, 94, 0);
    }
}
