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

using Asm65;
using CommonUtil;

namespace SourceGen.Tests {
    /// <summary>
    /// The codebase already contains self-check routines on several core types, but nothing
    /// ever called them, so a regression could sit unnoticed indefinitely.  These tests wire
    /// them into the suite so they run on every build.
    /// </summary>
    public class CoreSelfTests {
        [Fact]
        public void CpuDef_DefinitionsAreInternallyConsistent() {
            // Validates every built-in CPU definition: opcode table contents, address mode
            // widths, flag effects and the undocumented-opcode flags.
            Assert.True(CpuDef.DebugValidate());
        }

        [Fact]
        public void RangeSet_SelfTest() {
            Assert.True(RangeSet.Test());
        }

        [Fact]
        public void TypedRangeSet_SelfTest() {
            Assert.True(TypedRangeSet.Test());
        }

        [Fact]
        public void AddressMap_SelfTest() {
            // Covers region splitting, merging, address translation and serialization.
            Assert.True(AddressMap.Test());
        }

        [Fact]
        public void Version_SelfTest() {
            Assert.True(CommonUtil.Version.Test());
        }
    }
}
