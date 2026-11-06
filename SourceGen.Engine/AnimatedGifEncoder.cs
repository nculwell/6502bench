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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;

namespace SourceGen {
    /// <summary>
    /// Assembles a set of frames into an animated GIF.
    ///
    /// History: this used to live in CommonWPF and leaned on the WPF GifBitmapEncoder to
    /// quantize each frame, then reparsed the resulting GIF to recover the indexed pixels.
    /// Neither WPF nor a GIF encoder is available to us now, so this quantizes the frames
    /// itself and writes GIF89a directly (including the LZW-compressed image data).
    ///
    /// Frames that already use 256 or fewer distinct colours — which covers wireframe
    /// renders (black/white) and the indexed bitmap visualizations — are reproduced exactly.
    /// More colourful input is reduced to a 6x6x6 colour cube plus a grey ramp.
    /// </summary>
    public class AnimatedGifEncoder {
        private const int MAX_COLORS = 256;

        /// <summary>
        /// One quantized frame: indexed pixels plus the palette they refer to.
        /// </summary>
        private class Frame {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[] Indices { get; set; }      // one byte per pixel
            public byte[] Palette { get; set; }      // RGB triples, length = colors * 3
            public int ColorCount { get; set; }
            public int DelayMsec { get; set; }
        }

        private List<Frame> mFrames = new List<Frame>();

        public AnimatedGifEncoder() { }

        /// <summary>
        /// Adds a frame to the animation.
        /// </summary>
        /// <param name="frame">Source bitmap.</param>
        /// <param name="delayMsec">Delay before showing the next frame.</param>
        public void AddFrame(Bitmap frame, int delayMsec) {
            if (frame == null) {
                Debug.WriteLine("AddFrame: ignoring null frame");
                return;
            }
            Frame q = Quantize(frame);
            q.DelayMsec = delayMsec;
            mFrames.Add(q);
        }

        /// <summary>
        /// Converts the list of frames into an animated GIF, and writes it to the stream.
        /// </summary>
        /// <param name="stream">Output stream.</param>
        /// <param name="maxWidth">Largest frame width, or -1.</param>
        /// <param name="maxHeight">Largest frame height, or -1.</param>
        public void Save(Stream stream, out int maxWidth, out int maxHeight) {
            maxWidth = maxHeight = -1;

            if (mFrames.Count == 0) {
                Debug.Assert(false);
                return;
            }

            foreach (Frame f in mFrames) {
                if (f.Width > maxWidth) {
                    maxWidth = f.Width;
                }
                if (f.Height > maxHeight) {
                    maxHeight = f.Height;
                }
            }
            if (maxWidth <= 0 || maxHeight <= 0) {
                Debug.WriteLine("Unable to determine correct width/height");
                return;
            }

            // Header: GIF89a, logical screen descriptor, no global colour table.
            WriteAscii(stream, "GIF89a");
            WriteU16(stream, (ushort)maxWidth);
            WriteU16(stream, (ushort)maxHeight);
            stream.WriteByte(0x70);     // no GCT; max colour resolution
            stream.WriteByte(0);        // background colour index
            stream.WriteByte(0);        // no aspect ratio adjustment

            // Loop forever.
            WriteAscii(stream, "NETSCAPE2.0");
            stream.WriteByte(0x03);
            stream.WriteByte(0x01);
            WriteU16(stream, 0);        // loop count; 0 = infinite
            stream.WriteByte(0x00);

            foreach (Frame f in mFrames) {
                WriteFrame(stream, f);
            }

            stream.WriteByte(0x3b);     // trailer
        }

        private static void WriteFrame(Stream stream, Frame f) {
            // Graphic control extension (delay + disposal method).
            stream.WriteByte(0x21);
            stream.WriteByte(0xf9);
            stream.WriteByte(0x04);
            stream.WriteByte(0x04);     // disposal = restore to background, no transparency
            WriteU16(stream, (ushort)(f.DelayMsec / 10));
            stream.WriteByte(0);        // transparent colour index (unused)
            stream.WriteByte(0x00);

            // Image descriptor.
            stream.WriteByte(0x2c);
            WriteU16(stream, 0);        // left
            WriteU16(stream, 0);        // top
            WriteU16(stream, (ushort)f.Width);
            WriteU16(stream, (ushort)f.Height);

            // Local colour table.  Size must be a power of two, at least 2 entries.
            int bits = 1;
            while ((1 << bits) < f.ColorCount) {
                bits++;
            }
            if (bits > 8) {
                bits = 8;
            }
            int tableSize = 1 << bits;
            stream.WriteByte((byte)(0x80 | (bits - 1)));    // local table, size
            for (int i = 0; i < tableSize; i++) {
                int src = i * 3;
                if (i < f.ColorCount) {
                    stream.WriteByte(f.Palette[src + 0]);
                    stream.WriteByte(f.Palette[src + 1]);
                    stream.WriteByte(f.Palette[src + 2]);
                } else {
                    stream.WriteByte(0);
                    stream.WriteByte(0);
                    stream.WriteByte(0);
                }
            }

            WriteLzwImageData(stream, f.Indices, Math.Max(2, bits));
        }

        /// <summary>
        /// LZW-compresses the pixel indices and writes them as GIF sub-blocks.
        /// </summary>
        private static void WriteLzwImageData(Stream stream, byte[] indices, int minCodeSize) {
            stream.WriteByte((byte)minCodeSize);

            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            int nextCode = endCode + 1;
            int codeSize = minCodeSize + 1;

            Dictionary<int, int> table = new Dictionary<int, int>();
            List<byte> block = new List<byte>(255);
            int cur = 0;            // bit accumulator
            int curBits = 0;

            // Emit clear code to start.
            EmitCode(clearCode);
            int prefix = indices.Length > 0 ? indices[0] : 0;

            for (int i = 1; i < indices.Length; i++) {
                int suffix = indices[i];
                int key = (prefix << 8) | suffix;
                if (table.TryGetValue(key, out int code)) {
                    prefix = code;
                } else {
                    EmitCode(prefix);
                    if (nextCode < 4096) {
                        table[key] = nextCode++;
                        if (nextCode > (1 << codeSize) && codeSize < 12) {
                            codeSize++;
                        }
                    } else {
                        // Table full: reset.
                        EmitCode(clearCode);
                        table.Clear();
                        nextCode = endCode + 1;
                        codeSize = minCodeSize + 1;
                    }
                    prefix = suffix;
                }
            }

            EmitCode(prefix);
            EmitCode(endCode);

            // Flush remaining bits.
            if (curBits > 0) {
                block.Add((byte)(cur & 0xff));
                if (curBits > 8) {
                    block.Add((byte)((cur >> 8) & 0xff));
                }
                FlushBlock();
            }

            stream.WriteByte(0x00);     // block terminator

            void EmitCode(int code) {
                cur |= code << curBits;
                curBits += codeSize;
                while (curBits >= 8) {
                    block.Add((byte)(cur & 0xff));
                    cur >>= 8;
                    curBits -= 8;
                    if (block.Count == 255) {
                        FlushBlock();
                    }
                }
            }

            void FlushBlock() {
                if (block.Count == 0) {
                    return;
                }
                stream.WriteByte((byte)block.Count);
                for (int i = 0; i < block.Count; i++) {
                    stream.WriteByte(block[i]);
                }
                block.Clear();
            }
        }

        private static void WriteU16(Stream stream, ushort val) {
            stream.WriteByte((byte)(val & 0xff));
            stream.WriteByte((byte)((val >> 8) & 0xff));
        }

        private static void WriteAscii(Stream stream, string str) {
            for (int i = 0; i < str.Length; i++) {
                stream.WriteByte((byte)str[i]);
            }
        }

        /// <summary>
        /// Converts a bitmap to indexed form.
        /// </summary>
        private static Frame Quantize(Bitmap bitmap) {
            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;

            byte[] bgra = ReadBgra(bitmap, width, height);

            // Collect distinct colours.  Frames with 256 or fewer colours (wireframes,
            // indexed visualizations) round-trip exactly.
            Dictionary<int, int> colorToIndex = new Dictionary<int, int>();
            List<byte> palette = new List<byte>(MAX_COLORS * 3);
            bool exact = true;
            for (int i = 0; i < width * height; i++) {
                int argb = (bgra[i * 4 + 3] << 24) | (bgra[i * 4 + 2] << 16) |
                    (bgra[i * 4 + 1] << 8) | bgra[i * 4 + 0];
                if (!colorToIndex.ContainsKey(argb)) {
                    if (colorToIndex.Count >= MAX_COLORS) {
                        exact = false;
                        break;
                    }
                    colorToIndex[argb] = colorToIndex.Count;
                    palette.Add((byte)(argb >> 16));    // R
                    palette.Add((byte)(argb >> 8));     // G
                    palette.Add((byte)argb);            // B
                }
            }

            byte[] indices = new byte[width * height];
            if (exact) {
                for (int i = 0; i < width * height; i++) {
                    int argb = (bgra[i * 4 + 3] << 24) | (bgra[i * 4 + 2] << 16) |
                        (bgra[i * 4 + 1] << 8) | bgra[i * 4 + 0];
                    indices[i] = (byte)colorToIndex[argb];
                }
                // Palettes must have at least two entries.
                while (palette.Count < 6) {
                    palette.Add(0);
                }
                return new Frame {
                    Width = width,
                    Height = height,
                    Indices = indices,
                    Palette = palette.ToArray(),
                    ColorCount = Math.Max(2, colorToIndex.Count)
                };
            }

            // Too many colours: reduce to a 6x6x6 cube plus a 16-step grey ramp.
            const int CUBE = 6;
            const int GREY = 16;
            int colorCount = CUBE * CUBE * CUBE + GREY;
            byte[] cube = new byte[colorCount * 3];
            int ci = 0;
            for (int r = 0; r < CUBE; r++) {
                for (int g = 0; g < CUBE; g++) {
                    for (int b = 0; b < CUBE; b++) {
                        cube[ci++] = (byte)(r * 255 / (CUBE - 1));
                        cube[ci++] = (byte)(g * 255 / (CUBE - 1));
                        cube[ci++] = (byte)(b * 255 / (CUBE - 1));
                    }
                }
            }
            for (int i = 0; i < GREY; i++) {
                byte v = (byte)(i * 255 / (GREY - 1));
                cube[ci++] = v;
                cube[ci++] = v;
                cube[ci++] = v;
            }

            for (int i = 0; i < width * height; i++) {
                int b = bgra[i * 4 + 0];
                int g = bgra[i * 4 + 1];
                int r = bgra[i * 4 + 2];
                int ri = r * (CUBE - 1) / 255;
                int gi = g * (CUBE - 1) / 255;
                int bi = b * (CUBE - 1) / 255;
                indices[i] = (byte)((ri * CUBE + gi) * CUBE + bi);
            }

            return new Frame {
                Width = width,
                Height = height,
                Indices = indices,
                Palette = cube,
                ColorCount = colorCount
            };
        }

        /// <summary>
        /// Extracts pixels from an Avalonia bitmap as 32-bit BGRA.
        /// </summary>
        private static byte[] ReadBgra(Bitmap bitmap, int width, int height) {
            int stride = width * 4;
            byte[] buffer = new byte[stride * height];
            IntPtr handle = Marshal.AllocHGlobal(buffer.Length);
            try {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle, buffer.Length,
                    stride);
                Marshal.Copy(handle, buffer, 0, buffer.Length);
            } finally {
                Marshal.FreeHGlobal(handle);
            }
            return buffer;
        }
    }
}
