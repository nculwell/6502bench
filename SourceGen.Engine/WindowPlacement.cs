/*
 * Copyright 2019 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Globalization;

namespace SourceGen {
    /// <summary>
    /// Saved window geometry.
    ///
    /// The WPF implementation (CommonWPF.WindowPlacement) stored the Win32 WINDOWPLACEMENT
    /// structure, marshalled through WindowInteropHelper and serialized as base64 XML.  That
    /// is meaningless off Windows, so this records the geometry explicitly instead.  The
    /// value continues to be persisted through AppSettings (MAIN_WINDOW_PLACEMENT); only the
    /// encoding changes.
    ///
    /// Serialized form is a comma-separated list: X,Y,Width,Height,Maximized.
    /// </summary>
    public struct WindowPlacement {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public bool Maximized;

        public WindowPlacement(int x, int y, int width, int height, bool maximized) {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Maximized = maximized;
        }

        /// <summary>
        /// Default placement: size unset (-1) so the window can pick its own, not maximized.
        /// </summary>
        public static WindowPlacement Default {
            get { return new WindowPlacement(0, 0, -1, -1, false); }
        }

        /// <summary>
        /// True if a usable width and height were recorded.
        /// </summary>
        public bool HasSize { get { return Width > 0 && Height > 0; } }

        /// <summary>
        /// Encodes the placement for storage in the settings file.
        /// </summary>
        public string Serialize() {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}",
                X, Y, Width, Height, Maximized ? 1 : 0);
        }

        /// <summary>
        /// Decodes a placement previously produced by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="text">Encoded placement.</param>
        /// <param name="placement">Decoded value, or Default on failure.</param>
        /// <returns>True if the text was well-formed.</returns>
        public static bool TryParse(string text, out WindowPlacement placement) {
            placement = Default;
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            string[] parts = text.Split(',');
            if (parts.Length != 5) {
                return false;
            }

            int[] vals = new int[5];
            for (int i = 0; i < 5; i++) {
                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out vals[i])) {
                    return false;
                }
            }

            placement = new WindowPlacement(vals[0], vals[1], vals[2], vals[3], vals[4] != 0);
            return true;
        }
    }
}
