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

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for the instruction set definitions in Asm65.
    /// </summary>
    public class Asm65Tests {
        // ---- opcode decoding ----

        [Theory]
        // opcode, expected mnemonic, expected addressing mode
        [InlineData(0xea, OpName.NOP, OpDef.AddressMode.Implied)]
        [InlineData(0xa9, OpName.LDA, OpDef.AddressMode.Imm)]
        [InlineData(0x4c, OpName.JMP, OpDef.AddressMode.Abs)]
        [InlineData(0x20, OpName.JSR, OpDef.AddressMode.Abs)]
        [InlineData(0x60, OpName.RTS, OpDef.AddressMode.StackRTS)]
        [InlineData(0x00, OpName.BRK, OpDef.AddressMode.Implied)]
        [InlineData(0xa5, OpName.LDA, OpDef.AddressMode.DP)]
        [InlineData(0xbd, OpName.LDA, OpDef.AddressMode.AbsIndexX)]
        [InlineData(0xb1, OpName.LDA, OpDef.AddressMode.DPIndIndexY)]
        public void Cpu6502_DecodesKnownOpcodes(int opcode, string mnemonic,
                OpDef.AddressMode addrMode) {
            CpuDef cpu = CpuDef.GetBestMatch(CpuDef.CpuType.Cpu6502, false, false);
            OpDef op = cpu.GetOpDef(opcode);

            Assert.Equal(mnemonic, op.Mnemonic);
            Assert.Equal(addrMode, op.AddrMode);
            Assert.Equal(opcode, op.Opcode);
        }

        [Fact]
        public void Cpu6502_AddressModeSizesMatchInstructionLengths() {
            CpuDef cpu = CpuDef.GetBestMatch(CpuDef.CpuType.Cpu6502, false, false);

            // Spot-check the encoding widths that drive instruction length in the analyzer.
            Assert.Equal(OpDef.AddressMode.Implied, cpu.GetOpDef(0xea).AddrMode);    // 1 byte
            Assert.Equal(OpDef.AddressMode.Imm, cpu.GetOpDef(0xa9).AddrMode);        // 2 bytes
            Assert.Equal(OpDef.AddressMode.Abs, cpu.GetOpDef(0x4c).AddrMode);        // 3 bytes
        }

        [Fact]
        public void Cpu6502_UndocumentedOpcodesExcludedWhenRequested() {
            CpuDef withUndoc = CpuDef.GetBestMatch(CpuDef.CpuType.Cpu6502, true, false);
            CpuDef withoutUndoc = CpuDef.GetBestMatch(CpuDef.CpuType.Cpu6502, false, false);

            // 0x02 is a well-known undocumented 6502 opcode (JAM/KIL family).
            Assert.False(withUndoc.GetOpDef(0x02).IsUndocumented == false);
            Assert.True(withoutUndoc.GetOpDef(0x02).IsUndocumented == false);
        }

        [Fact]
        public void Cpu65816_TwoByteBrkChangesBrkLength() {
            CpuDef oneByte = CpuDef.GetBestMatch(CpuDef.CpuType.Cpu65816, false, false);
            CpuDef twoByte = CpuDef.GetBestMatch(CpuDef.CpuType.Cpu65816, false, true);

            // Default is the classic single-byte BRK; the two-byte form is opt-in.
            Assert.Equal(OpDef.AddressMode.Implied, oneByte.GetOpDef(0x00).AddrMode);
            Assert.Equal(OpDef.AddressMode.StackInt, twoByte.GetOpDef(0x00).AddrMode);
            Assert.NotSame(oneByte.GetOpDef(0x00), twoByte.GetOpDef(0x00));
        }

        // ---- CPU name mapping ----

        [Theory]
        [InlineData(CpuDef.CpuType.Cpu6502)]
        [InlineData(CpuDef.CpuType.Cpu6510)]
        [InlineData(CpuDef.CpuType.Cpu65C02)]
        [InlineData(CpuDef.CpuType.CpuW65C02)]
        [InlineData(CpuDef.CpuType.Cpu65816)]
        public void CpuTypeName_RoundTripsThroughString(CpuDef.CpuType cpuType) {
            string name = CpuDef.GetCpuNameFromType(cpuType);
            Assert.False(string.IsNullOrEmpty(name));
            Assert.Equal(cpuType, CpuDef.GetCpuTypeFromName(name));
        }

        [Fact]
        public void CpuTypeFromName_UnknownNameReturnsCpuUnknown() {
            Assert.Equal(CpuDef.CpuType.CpuUnknown, CpuDef.GetCpuTypeFromName("NotACpu"));
        }

        // ---- number parsing ----

        [Theory]
        [InlineData("$1234", 0x1234, 16)]
        [InlineData("$0", 0, 16)]
        [InlineData("0x1f", 31, 16)]
        [InlineData("%1010", 10, 2)]
        [InlineData("%0", 0, 2)]
        [InlineData("42", 42, 10)]
        [InlineData("0", 0, 10)]
        public void TryParseInt_RecognisesNumericPrefixes(string text, int expected, int baseOut) {
            Assert.True(Number.TryParseInt(text, out int value, out int radix));
            Assert.Equal(expected, value);
            Assert.Equal(baseOut, radix);
        }

        [Theory]
        [InlineData("")]
        [InlineData("zzz")]
        [InlineData("$")]
        [InlineData("%2")]          // 2 is not a binary digit
        public void TryParseInt_RejectsInvalidInput(string text) {
            Assert.False(Number.TryParseInt(text, out _, out _));
        }

        [Theory]
        [InlineData("$ff", 255)]
        [InlineData("ff", 255)]
        [InlineData("0xFF", 255)]
        [InlineData("FFFF", 65535)]
        public void TryParseIntHex_AcceptsCommonForms(string text, int expected) {
            Assert.True(Number.TryParseIntHex(text, out int value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("$10000", 0x10000)]     // beyond 16 bits, still an int
        [InlineData("0", 0)]
        [InlineData("%1111", 15)]
        public void TryParseLong_HandlesWideValues(string text, long expected) {
            Assert.True(Number.TryParseLong(text, out long value, out _));
            Assert.Equal(expected, value);
        }
    }
}
