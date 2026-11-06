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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGen {
    /// <summary>
    /// Thin wrapper around System.Text.Json.
    ///
    /// This replaces System.Web.Script.Serialization.JavaScriptSerializer, which does not
    /// exist outside .NET Framework.  The options and the object converter are chosen to
    /// reproduce the behaviour the project files were written against:
    ///
    ///   - property names are emitted exactly as declared (no naming policy),
    ///   - properties are never omitted, including nulls and default values,
    ///   - unknown members in the input are ignored,
    ///   - "object"-typed values are written using their runtime type and read back as
    ///     ordinary CLR primitives, lists and dictionaries rather than JsonElement.
    /// </summary>
    public static class Json {
        public static readonly JsonSerializerOptions Options = CreateOptions();

        private static JsonSerializerOptions CreateOptions() {
            JsonSerializerOptions options = new JsonSerializerOptions {
                WriteIndented = false,
                // JavaScriptSerializer serialized public fields as well as properties, and
                // several serialized types (e.g. SerDbrValue) are field-only.  Leave this
                // on or those members silently disappear from saved projects.
                IncludeFields = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
            };
            options.Converters.Add(new LenientObjectConverter());
            return options;
        }

        public static string Serialize<T>(T value) {
            return JsonSerializer.Serialize(value, Options);
        }

        public static string Serialize(object value, Type type) {
            return JsonSerializer.Serialize(value, type, Options);
        }

        public static T Deserialize<T>(string json) {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>
        /// Serializes/deserializes values declared as "object".
        ///
        /// JavaScriptSerializer wrote these using the runtime type, and handed back plain
        /// primitives and containers on the way in.  System.Text.Json would otherwise
        /// produce JsonElement values, which the project-file loading code does not expect.
        /// </summary>
        private sealed class LenientObjectConverter : JsonConverter<object> {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert,
                    JsonSerializerOptions options) {
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader)) {
                    return FromElement(doc.RootElement, options);
                }
            }

            public override void Write(Utf8JsonWriter writer, object value,
                    JsonSerializerOptions options) {
                if (value == null) {
                    writer.WriteNullValue();
                    return;
                }
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }

            private static object FromElement(JsonElement elem, JsonSerializerOptions options) {
                switch (elem.ValueKind) {
                    case JsonValueKind.Object:
                        Dictionary<string, object> dict =
                            new Dictionary<string, object>(StringComparer.Ordinal);
                        foreach (JsonProperty prop in elem.EnumerateObject()) {
                            dict[prop.Name] = FromElement(prop.Value, options);
                        }
                        return dict;
                    case JsonValueKind.Array:
                        List<object> list = new List<object>();
                        foreach (JsonElement item in elem.EnumerateArray()) {
                            list.Add(FromElement(item, options));
                        }
                        return list;
                    case JsonValueKind.String:
                        return elem.GetString();
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.Number:
                        // Match JavaScriptSerializer: integral values become int (or long
                        // when they don't fit), everything else becomes a double.
                        if (elem.TryGetInt32(out int intVal)) {
                            return intVal;
                        }
                        if (elem.TryGetInt64(out long longVal)) {
                            return longVal;
                        }
                        return elem.GetDouble();
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                    default:
                        return null;
                }
            }
        }
    }
}
