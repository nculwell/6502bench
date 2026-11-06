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
using System.Collections.Generic;
using Xunit;

using Avalonia.Media;

using SourceGen;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for the project-serialization layer and other engine types touched by the
    /// .NET Framework removal.  These cover the pieces that the generation regression tests
    /// cannot reach: value encoding helpers and the JSON compatibility shims.
    /// </summary>
    public class EngineTypeTests {
        // ---- JSON ----

        [Fact]
        public void Json_ObjectValuesKeepTheirRuntimeType() {
            // JavaScriptSerializer handed back plain CLR primitives for "object" members.
            // Visualization parameters rely on that, so a JsonElement would break them.
            Dictionary<string, object> input = new Dictionary<string, object> {
                ["int"] = 42,
                ["double"] = 1.5,
                ["bool"] = true,
                ["string"] = "hello",
            };

            string json = Json.Serialize(input);
            Dictionary<string, object> back = Json.Deserialize<Dictionary<string, object>>(json);

            Assert.Equal(42, Assert.IsType<int>(back["int"]));
            Assert.Equal(1.5, Assert.IsType<double>(back["double"]));
            Assert.True(Assert.IsType<bool>(back["bool"]));
            Assert.Equal("hello", Assert.IsType<string>(back["string"]));
        }

        [Fact]
        public void Json_NestedContainersRoundTrip() {
            Dictionary<string, object> input = new Dictionary<string, object> {
                ["list"] = new List<object> { 1, "two", 3.5 },
                ["map"] = new Dictionary<string, object> { ["inner"] = 7 },
            };

            string json = Json.Serialize(input);
            Dictionary<string, object> back = Json.Deserialize<Dictionary<string, object>>(json);

            List<object> list = Assert.IsType<List<object>>(back["list"]);
            Assert.Equal(3, list.Count);
            Assert.Equal(1, Assert.IsType<int>(list[0]));
            Assert.Equal("two", Assert.IsType<string>(list[1]));

            Dictionary<string, object> map =
                Assert.IsType<Dictionary<string, object>>(back["map"]);
            Assert.Equal(7, Assert.IsType<int>(map["inner"]));
        }

        [Fact]
        public void Json_SerializesPublicFields() {
            // SerDbrValue is field-only.  Leaving IncludeFields off silently dropped the
            // 65816 data-bank overrides, which changed generated output.
            string json = Json.Serialize(new FieldOnly { Alpha = 1, Beta = "x" });

            Assert.Contains("\"Alpha\":1", json);
            Assert.Contains("\"Beta\":\"x\"", json);
        }

        [Fact]
        public void Json_DeserializesPublicFields() {
            FieldOnly value = Json.Deserialize<FieldOnly>("{\"Alpha\":7,\"Beta\":\"y\"}");

            Assert.Equal(7, value.Alpha);
            Assert.Equal("y", value.Beta);
        }

        [Fact]
        public void Json_NullValuesRoundTrip() {
            Dictionary<string, object> input = new Dictionary<string, object> {
                ["missing"] = null,
            };
            string json = Json.Serialize(input);

            Assert.Contains("null", json);
            Dictionary<string, object> back = Json.Deserialize<Dictionary<string, object>>(json);
            Assert.Null(back["missing"]);
        }

        private class FieldOnly {
            public int Alpha;
            public string Beta;
        }

        // ---- ColorUtil ----

        [Fact]
        public void ColorUtil_IntRoundTripPreservesArgb() {
            Color original = Color.FromArgb(0x80, 0x12, 0x34, 0x56);
            int packed = ColorUtil.ColorToInt(original);

            Assert.Equal(0x80123456u, (uint)packed);
            Assert.Equal(original, ColorUtil.ColorFromInt(packed));
        }

        [Fact]
        public void ColorUtil_ZeroColorIsFullyTransparent() {
            Assert.Equal(0, ColorUtil.ColorToInt(ColorUtil.ZeroColor));
            Assert.Equal(0, ColorUtil.ZeroColor.A);
        }

        [Fact]
        public void ColorUtil_MultiplyScalesChannels() {
            Color half = ColorUtil.Multiply(Color.FromArgb(255, 100, 100, 100), 0.5f);

            // Alpha scales too, which is what WPF's Color.Multiply did.
            Assert.Equal(127, half.A);
            Assert.Equal(50, half.R);
            Assert.Equal(50, half.G);
            Assert.Equal(50, half.B);
        }

        [Fact]
        public void ColorUtil_MultiplySaturatesRatherThanWrapping() {
            Color bright = ColorUtil.Multiply(Color.FromArgb(255, 200, 200, 200), 4.0f);

            Assert.Equal(255, bright.R);
            Assert.Equal(255, bright.G);
            Assert.Equal(255, bright.B);
        }

        // ---- WindowPlacement ----

        [Fact]
        public void WindowPlacement_RoundTripsThroughText() {
            WindowPlacement original = new WindowPlacement(10, 20, 800, 600, true);

            string text = original.Serialize();
            Assert.True(WindowPlacement.TryParse(text, out WindowPlacement parsed));

            Assert.Equal(original.X, parsed.X);
            Assert.Equal(original.Y, parsed.Y);
            Assert.Equal(original.Width, parsed.Width);
            Assert.Equal(original.Height, parsed.Height);
            Assert.Equal(original.Maximized, parsed.Maximized);
            Assert.True(parsed.HasSize);
        }

        [Theory]
        [InlineData("")]
        [InlineData("nonsense")]
        [InlineData("1,2,3")]                   // too few fields
        [InlineData("1,2,3,4,5,6")]             // too many fields
        [InlineData("a,b,c,d,e")]               // not numeric
        public void WindowPlacement_RejectsMalformedText(string text) {
            Assert.False(WindowPlacement.TryParse(text, out WindowPlacement parsed));
            Assert.False(parsed.HasSize);
        }

        [Fact]
        public void WindowPlacement_DefaultHasNoSize() {
            Assert.False(WindowPlacement.Default.HasSize);
        }
    }
}
