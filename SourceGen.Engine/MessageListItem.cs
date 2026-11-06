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
namespace SourceGen {
    /// <summary>
    /// One row of the message list, pre-formatted for display.
    ///
    /// This used to be a nested type in the WPF MainWindow, but it is produced by
    /// MessageList and carries no toolkit types, so it belongs with the engine.
    /// </summary>
    public class MessageListItem {
        public string Severity { get; private set; }
        public string Offset { get; private set; }
        public string Type { get; private set; }
        public string Context { get; private set; }
        public string Resolution { get; private set; }

        public int OffsetValue { get; private set; }

        public MessageListItem(string severity, int offsetValue, string offset, string type,
                string context, string resolution) {
            Severity = severity;
            OffsetValue = offsetValue;
            Offset = offset;
            Type = type;
            Context = context;
            Resolution = resolution;
        }
    }
}
