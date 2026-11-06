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

using Asm65;
using SourceGen;

namespace SourceGen.Tests {
    /// <summary>
    /// Tests for project-data types: symbols, pseudo-op naming and long-comment rendering.
    /// </summary>
    public class ProjectDataTests {
        // ---- symbols ----

        [Fact]
        public void Symbol_ExposesItsProperties() {
            Symbol sym = new Symbol("MyLabel", 0x1000, Symbol.Source.User,
                Symbol.Type.GlobalAddr, Symbol.LabelAnnotation.None);

            Assert.Equal("MyLabel", sym.Label);
            Assert.Equal(0x1000, sym.Value);
            Assert.Equal(Symbol.Source.User, sym.SymbolSource);
            Assert.Equal(Symbol.Type.GlobalAddr, sym.SymbolType);
            Assert.Equal(Symbol.LabelAnnotation.None, sym.LabelAnno);
        }

        [Fact]
        public void Symbol_EqualityIsValueBased() {
            Symbol a = new Symbol("L1", 0x1000, Symbol.Source.User,
                Symbol.Type.GlobalAddr, Symbol.LabelAnnotation.None);
            Symbol b = new Symbol("L1", 0x1000, Symbol.Source.User,
                Symbol.Type.GlobalAddr, Symbol.LabelAnnotation.None);
            Symbol c = new Symbol("L1", 0x1001, Symbol.Source.User,
                Symbol.Type.GlobalAddr, Symbol.LabelAnnotation.None);

            Assert.True(a == b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            Assert.True(a != c);
            Assert.False(a.Equals(c));
        }

        // ---- pseudo-op names ----

        [Fact]
        public void PseudoOpNames_RoundTripsThroughJson() {
            // The header editor stores the operator names as a JSON string inside the project
            // file, so this exercises the System.Text.Json replacement on a real data type.
            PseudoOp.PseudoOpNames original = new PseudoOp.PseudoOpNames(
                new Dictionary<string, string> {
                    ["EquDirective"] = "MY_EQU",
                    ["DataBankDirective"] = "MY_DBANK",
                });

            Assert.Equal("MY_EQU", original.EquDirective);

            string serialized = original.Serialize();
            Assert.False(string.IsNullOrEmpty(serialized));

            PseudoOp.PseudoOpNames back = PseudoOp.PseudoOpNames.Deserialize(serialized);
            Assert.Equal(original.EquDirective, back.EquDirective);
            Assert.Equal(original.DataBankDirective, back.DataBankDirective);

            // Re-serializing must produce identical text, or the project file would churn.
            Assert.Equal(serialized, back.Serialize());
        }

        [Fact]
        public void PseudoOpNames_MissingValuesBecomeEmptyStrings() {
            // The type carries no built-in defaults; real values come from the per-assembler
            // config.  The dictionary constructor documents that unlisted names are empty.
            PseudoOp.PseudoOpNames names = new PseudoOp.PseudoOpNames(
                new Dictionary<string, string> { ["EquDirective"] = "EQU" });

            Assert.Equal("EQU", names.EquDirective);
            Assert.Equal(string.Empty, names.VarDirective);
            Assert.Equal(string.Empty, names.DataBankDirective);
        }

        // ---- multi-line comments ----

        [Fact]
        public void MultiLineComment_RejectsAbsurdWidth() {
            // Guard rail against a corrupt project file producing runaway formatting.
            MultiLineComment mlc = new MultiLineComment("hello", false, false, 10000);
            List<string> lines = mlc.FormatText(NewFormatter(), string.Empty);

            Assert.Single(lines);
            Assert.Contains("Bad MaxWidth", lines[0]);
        }

        [Fact]
        public void MultiLineComment_FormatsNonEmptyText() {
            MultiLineComment mlc = new MultiLineComment("hello world", false, false, 40);
            List<string> lines = mlc.FormatText(NewFormatter(), string.Empty);

            Assert.NotEmpty(lines);
            // The text must survive formatting.
            Assert.Contains("hello", string.Join("\n", lines));
        }

        [Fact]
        public void MultiLineComment_WrapsLongText() {
            string text = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu";
            MultiLineComment mlc = new MultiLineComment(text, false, false, 30);
            List<string> lines = mlc.FormatText(NewFormatter(), string.Empty);

            Assert.True(lines.Count > 1, "long text should wrap onto multiple lines");
        }

        [Fact]
        public void MultiLineComment_EqualityComparesContent() {
            MultiLineComment a = new MultiLineComment("same text");
            MultiLineComment b = new MultiLineComment("same text");
            MultiLineComment c = new MultiLineComment("different text");

            Assert.True(a == b);
            Assert.True(a != c);
        }

        private static Formatter NewFormatter() {
            return new Formatter(new Formatter.FormatConfig());
        }
    }
}
