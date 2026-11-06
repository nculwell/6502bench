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
using System.Text;
using Xunit;

using CommonUtil;
using CUV = CommonUtil.Version;      // disambiguate from System.Version

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for the shared utility layer.
    /// </summary>
    public class CommonUtilTests {
        // ---- CRC32 ----

        [Fact]
        public void Crc32_MatchesStandardCheckValue() {
            // 0xCBF43926 is the canonical CRC-32/ISO-HDLC check value for "123456789",
            // so this pins the polynomial and initial/final inversion.
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            Assert.Equal(0xcbf43926u, CRC32.OnWholeBuffer(0, data));
        }

        [Fact]
        public void Crc32_EmptyInputIsZero() {
            Assert.Equal(0u, CRC32.OnWholeBuffer(0, new byte[0]));
        }

        [Fact]
        public void Crc32_IncrementalMatchesOneShot() {
            byte[] data = Encoding.ASCII.GetBytes("123456789");

            uint crc = CRC32.OnBuffer(0, data, 0, 4);
            crc = CRC32.OnBuffer(crc, data, 4, data.Length - 4);

            Assert.Equal(CRC32.OnWholeBuffer(0, data), crc);
        }

        [Fact]
        public void Crc32_DifferentInputsDiffer() {
            byte[] a = Encoding.ASCII.GetBytes("abc");
            byte[] b = Encoding.ASCII.GetBytes("abd");
            Assert.NotEqual(CRC32.OnWholeBuffer(0, a), CRC32.OnWholeBuffer(0, b));
        }

        // ---- RangeSet ----

        [Fact]
        public void RangeSet_TracksMembershipAndCount() {
            RangeSet set = new RangeSet();
            set.Add(5);
            set.Add(6);
            set.Add(7);
            set.Add(10);

            Assert.Equal(4, set.Count);
            Assert.True(set.Contains(5));
            Assert.True(set.Contains(7));
            Assert.False(set.Contains(8));
            Assert.False(set.Contains(4));
        }

        [Fact]
        public void RangeSet_CollapsesContiguousAdds() {
            RangeSet set = new RangeSet();
            set.AddRange(1, 5);

            Assert.Equal(5, set.Count);
            Assert.Equal(1, set.DebugRangeCount);       // a single contiguous run
        }

        [Fact]
        public void RangeSet_RemoveSplitsRange() {
            RangeSet set = new RangeSet();
            set.AddRange(1, 5);
            set.Remove(3);

            Assert.Equal(4, set.Count);
            Assert.False(set.Contains(3));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(4));
        }

        [Fact]
        public void RangeSet_EnumeratesInAscendingOrder() {
            RangeSet set = new RangeSet();
            set.Add(10);
            set.Add(5);
            set.Add(7);

            List<int> values = new List<int>();
            foreach (int val in set) {
                values.Add(val);
            }

            Assert.Equal(new[] { 5, 7, 10 }, values);
        }

        [Fact]
        public void RangeSet_ClearEmptiesSet() {
            RangeSet set = new RangeSet();
            set.AddRange(1, 3);
            set.Clear();

            Assert.Equal(0, set.Count);
            Assert.False(set.Contains(1));
        }

        // ---- Version ----

        [Fact]
        public void Version_ParsesDottedTriples() {
            CUV v = CUV.Parse("1.10.3");

            Assert.Equal(1, v.Major);
            Assert.Equal(10, v.Minor);
            Assert.Equal(3, v.Patch);
            Assert.True(v.IsValid);
        }

        [Fact]
        public void Version_ParseFailureYieldsNoVersion() {
            CUV v = CUV.Parse("not-a-version");
            Assert.False(v.IsValid);
        }

        [Fact]
        public void Version_OrdersNumerically() {
            Assert.True(new CUV(1, 2, 3) > new CUV(1, 2, 2));
            Assert.True(new CUV(1, 10, 0) > new CUV(1, 9, 9));    // not string ordering
            Assert.True(new CUV(2, 0, 0) > new CUV(1, 99, 99));
            Assert.True(new CUV(1, 2, 3) == new CUV(1, 2, 3));
            Assert.False(new CUV(1, 2, 3) != new CUV(1, 2, 3));
        }

        [Fact]
        public void Version_EqualityIsConsistentWithHashCode() {
            CUV a = new CUV(1, 2, 3);
            CUV b = new CUV(1, 2, 3);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a.Equals(b));
            Assert.False(a.Equals(new CUV(1, 2, 4)));
        }

        // ---- TextUtil ----

        [Theory]
        [InlineData('A', true)]
        [InlineData(' ', true)]
        [InlineData('~', true)]
        [InlineData('\t', false)]
        [InlineData('\n', false)]
        public void TextUtil_IsPrintableAscii(char ch, bool expected) {
            Assert.Equal(expected, TextUtil.IsPrintableAscii(ch));
        }

        [Theory]
        [InlineData(0x41, true)]        // printable low ASCII
        [InlineData(0x20, true)]        // space
        [InlineData(0xc1, true)]        // high ASCII (0xa0-0xfe)
        [InlineData(0x09, false)]       // tab
        [InlineData(0x7f, false)]       // DEL
        [InlineData(0x9f, false)]       // gap between the two ranges
        public void TextUtil_IsHiLoAscii(int val, bool expected) {
            Assert.Equal(expected, TextUtil.IsHiLoAscii(val));
        }
    }
}
