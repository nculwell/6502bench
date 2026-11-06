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
using Xunit;

using CommonUtil;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for the file-offset to address translation map.  The analyzer and the code
    /// generator both depend on this being exact; an error here shows up as wrong addresses
    /// throughout a disassembly.
    /// </summary>
    public class AddressMapTests {
        [Fact]
        public void DefaultMap_FloatsWithNoAddressMapping() {
            // A brand-new map assigns no addresses: the whole span is "floating" until an
            // address entry is added.
            AddressMap map = new AddressMap(16);
            for (int i = 0; i < 16; i++) {
                Assert.Equal(AddressMap.NON_ADDR, map.OffsetToAddress(i));
            }
        }

        [Fact]
        public void AddEntry_SetsBaseAddress() {
            AddressMap map = new AddressMap(16);
            map.AddEntry(0, 16, 0x8000);

            Assert.Equal(0x8000, map.OffsetToAddress(0));
            Assert.Equal(0x800f, map.OffsetToAddress(15));
        }

        [Fact]
        public void OffsetToAddress_OutOfRangeIsNotAnAddress() {
            AddressMap map = new AddressMap(16);

            Assert.Equal(AddressMap.NON_ADDR, map.OffsetToAddress(16));
            Assert.Equal(AddressMap.NON_ADDR, map.OffsetToAddress(-1));
            Assert.Equal(AddressMap.NON_ADDR, map.OffsetToAddress(1000));
        }

        [Fact]
        public void AddressToOffset_TranslatesWithinRegion() {
            AddressMap map = new AddressMap(16);
            map.AddEntry(0, 16, 0x8000);

            Assert.Equal(0, map.AddressToOffset(0, 0x8000));
            Assert.Equal(5, map.AddressToOffset(0, 0x8005));
            Assert.Equal(15, map.AddressToOffset(0, 0x800f));
        }

        [Fact]
        public void AddressToOffset_RejectsAddressOutsideRegion() {
            AddressMap map = new AddressMap(16);
            map.AddEntry(0, 16, 0x8000);

            // Unmapped addresses report -1 (this method uses -1 rather than NON_ADDR).
            Assert.Equal(-1, map.AddressToOffset(0, 0x9000));
        }

        [Fact]
        public void IsRangeUnbroken_TrueForSingleRegion() {
            AddressMap map = new AddressMap(16);
            Assert.True(map.IsRangeUnbroken(0, 16));
        }

        [Fact]
        public void Clone_ProducesIndependentEquivalentMap() {
            AddressMap map = new AddressMap(16);
            map.AddEntry(0, 16, 0x8000);

            AddressMap copy = map.Clone();

            Assert.Equal(map.OffsetToAddress(3), copy.OffsetToAddress(3));

            copy.AddEntry(0, 8, 0x4000);
            // The original must be unaffected by edits to the clone.
            Assert.Equal(0x8003, map.OffsetToAddress(3));
        }

        [Fact]
        public void GetEntryList_ReportsSpanLength() {
            AddressMap map = new AddressMap(32);
            map.AddEntry(0, 32, 0x1000);

            int spanLength;
            var entries = map.GetEntryList(out spanLength);

            Assert.Equal(32, spanLength);
            Assert.NotEmpty(entries);
        }
    }
}
