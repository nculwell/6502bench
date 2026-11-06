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
using Avalonia.Media;

namespace SourceGen {
    /// <summary>
    /// Colour helpers shared by the engine.
    ///
    /// These used to live in CommonWPF.Helper, which is a WPF assembly.  The colour
    /// values themselves are toolkit-neutral ARGB, so they belong with the engine.
    /// </summary>
    public static class ColorUtil {
        /// <summary>
        /// Fully transparent colour, used to mean "no colour".
        /// </summary>
        public static readonly Color ZeroColor = Color.FromArgb(0, 0, 0, 0);

        /// <summary>
        /// Packs a colour into 32-bit ARGB.
        /// </summary>
        public static int ColorToInt(Color color) {
            return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        /// <summary>
        /// Creates a Color value from 32-bit ARGB.
        /// </summary>
        public static Color ColorFromInt(int colorInt) {
            return Color.FromArgb((byte)(colorInt >> 24), (byte)(colorInt >> 16),
                (byte)(colorInt >> 8), (byte)colorInt);
        }

        /// <summary>
        /// Scales each channel of a colour by the given factor, saturating at 255.
        ///
        /// WPF's Color.Multiply did this; Avalonia has no equivalent.  Used to dim note
        /// backgrounds in dark mode.
        /// </summary>
        public static Color Multiply(Color color, float factor) {
            return Color.FromArgb(
                ScaleChannel(color.A, factor),
                ScaleChannel(color.R, factor),
                ScaleChannel(color.G, factor),
                ScaleChannel(color.B, factor));
        }

        private static byte ScaleChannel(byte value, float factor) {
            int scaled = (int)(value * factor);
            if (scaled < 0) {
                return 0;
            }
            if (scaled > 255) {
                return 255;
            }
            return (byte)scaled;
        }
    }
}
