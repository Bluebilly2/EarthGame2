using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>Raised for malformed JSON text, or for a document that lacks what a reader required of it.</summary>
    public sealed class JsonException : Exception
    {
        public JsonException(string message) : base(message) { }
    }

    /// <summary>
    /// A JSON object whose keys keep the order they were added in, so a file the engine writes reads the same way
    /// every time and a diff of two saves is a diff of values, not of key order.
    /// </summary>
    public sealed class JsonObject : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<string> _keys = new List<string>();
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        public int Count => _keys.Count;
        public IReadOnlyList<string> Keys => _keys;

        public object this[string key]
        {
            get => _values.TryGetValue(key, out object v) ? v : throw new JsonException("missing key '" + key + "'");
            set
            {
                if (!_values.ContainsKey(key)) _keys.Add(key);
                _values[key] = value;
            }
        }

        public bool Contains(string key) => _values.ContainsKey(key);

        public bool TryGet(string key, out object value) => _values.TryGetValue(key, out value);

        /// <summary>Adds and returns this, so a document can be built in one expression.</summary>
        public JsonObject With(string key, object value)
        {
            this[key] = value;
            return this;
        }

        public string String(string key)
            => this[key] is string s ? s : throw new JsonException("key '" + key + "' is not a string");

        public double Number(string key)
            => this[key] is double d ? d : this[key] is ulong u ? u : this[key] is long l ? l
                : throw new JsonException("key '" + key + "' is not a number");

        /// <summary>An exact unsigned integer (WG.0c, 2026-09-14). Seeds must never pass through a rounded double.</summary>
        public ulong UInt64(string key)
        {
            object value = this[key];
            if (value is ulong u) return u;
            if (value is long l && l >= 0) return (ulong)l;
            throw new JsonException("key '" + key + "' is not an exact unsigned 64-bit integer");
        }

        /// <summary>An exact signed integer, for saved ticks as well as JSON's small integer fields.</summary>
        public long Int64(string key)
        {
            object value = this[key];
            if (value is long l) return l;
            if (value is ulong u && u <= long.MaxValue) return (long)u;
            throw new JsonException("key '" + key + "' is not an exact signed 64-bit integer");
        }

        public ulong UInt64Or(string key, ulong fallback) => Contains(key) ? UInt64(key) : fallback;
        public long Int64Or(string key, long fallback) => Contains(key) ? Int64(key) : fallback;

        public int Int(string key)
        {
            double d = Number(key);
            if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue)
                throw new JsonException("key '" + key + "' is not a whole number: " + d.ToString(CultureInfo.InvariantCulture));
            return (int)d;
        }

        public bool Bool(string key)
            => this[key] is bool b ? b : throw new JsonException("key '" + key + "' is not a boolean");

        public JsonObject Object(string key)
            => this[key] is JsonObject o ? o : throw new JsonException("key '" + key + "' is not an object");

        public List<object> Array(string key)
            => this[key] is List<object> a ? a : throw new JsonException("key '" + key + "' is not an array");

        /// <summary>A number with a fallback when the key is absent; a present key of the wrong type is still an error.</summary>
        public double NumberOr(string key, double fallback) => Contains(key) ? Number(key) : fallback;

        public string StringOr(string key, string fallback) => Contains(key) ? String(key) : fallback;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (string k in _keys) yield return new KeyValuePair<string, object>(k, _values[k]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A small strict JSON reader and writer, engine-free and dependency-free: the sidecars, <c>world.json</c>
    /// and the run logs are all simple documents, and both Unity and dotnet compile these packages from the same
    /// files, so neither side's JSON library is available to the engine (System.Text.Json is not in .NET Standard
    /// 2.1; Newtonsoft is a Unity package). Values are <see cref="JsonObject"/>, <c>List&lt;object&gt;</c>,
    /// <c>string</c>, <c>double</c>, <c>long</c>, <c>ulong</c>, <c>bool</c> and <c>null</c>. Whole-number tokens
    /// retain their integer type (WG.0c, 2026-09-14); the old all-double reader
    /// rounded large world seeds on continue. Measured quantities still use <see cref="JsonObject.Number"/>.
    /// </summary>
    public static class Json
    {
        private const int MaxDepth = 64;

        public static object Parse(string text)
        {
            if (text == null) throw new JsonException("no text");
            Parser p = new Parser(text);
            p.SkipWhitespace();
            object value = p.ReadValue(0);
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("trailing characters after the document");
            return value;
        }

        /// <summary>Parses a document whose top level must be an object.</summary>
        public static JsonObject ParseObject(string text)
            => Parse(text) is JsonObject o ? o : throw new JsonException("the document is not a JSON object");

        /// <summary>Writes a value; <paramref name="indent"/> lays objects and arrays out one entry per line.</summary>
        public static string Write(object value, bool indent = false)
        {
            StringBuilder sb = new StringBuilder(256);
            WriteValue(sb, value, indent, 0);
            if (indent) sb.Append('\n');
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, bool indent, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case double d:
                    WriteNumber(sb, d);
                    break;
                case float f:
                    WriteNumber(sb, f);
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case uint ui:
                    sb.Append(ui.ToString(CultureInfo.InvariantCulture));
                    break;
                case ulong ul:
                    sb.Append(ul.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonObject o:
                    WriteObject(sb, o, indent, depth);
                    break;
                case IList list:
                    WriteArray(sb, list, indent, depth);
                    break;
                default:
                    throw new JsonException("cannot write a " + value.GetType().Name);
            }
        }

        private static void WriteObject(StringBuilder sb, JsonObject o, bool indent, int depth)
        {
            if (o.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object> pair in o)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, indent, depth + 1);
                WriteString(sb, pair.Key);
                sb.Append(indent ? ": " : ":");
                WriteValue(sb, pair.Value, indent, depth + 1);
            }
            NewLine(sb, indent, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IList list, bool indent, int depth)
        {
            if (list.Count == 0)
            {
                sb.Append("[]");
                return;
            }
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                NewLine(sb, indent, depth + 1);
                WriteValue(sb, list[i], indent, depth + 1);
            }
            NewLine(sb, indent, depth);
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool indent, int depth)
        {
            if (!indent) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                throw new JsonException("JSON has no representation for " + d.ToString(CultureInfo.InvariantCulture));
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
                sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            else
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;

            public Parser(string text)
            {
                _text = text;
            }

            public bool AtEnd => _pos >= _text.Length;

            public JsonException Error(string what) => new JsonException(what + " at offset " + _pos);

            public void SkipWhitespace()
            {
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _pos++;
                    else break;
                }
            }

            private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

            private void Expect(char c)
            {
                if (Peek() != c) throw Error("expected '" + c + "'");
                _pos++;
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Error("nesting deeper than " + MaxDepth);
                SkipWhitespace();
                char c = Peek();
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': ReadWord("true"); return true;
                    case 'f': ReadWord("false"); return false;
                    case 'n': ReadWord("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error(AtEnd ? "unexpected end of text" : "unexpected character '" + c + "'");
                }
            }

            private JsonObject ReadObject(int depth)
            {
                Expect('{');
                JsonObject o = new JsonObject();
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _pos++;
                    return o;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("expected a quoted key");
                    string key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    object value = ReadValue(depth + 1);
                    if (o.Contains(key)) throw Error("duplicate key '" + key + "'");
                    o[key] = value;
                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == '}')
                    {
                        _pos++;
                        return o;
                    }
                    throw Error("expected ',' or '}'");
                }
            }

            private List<object> ReadArray(int depth)
            {
                Expect('[');
                List<object> list = new List<object>();
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _pos++;
                    return list;
                }
                while (true)
                {
                    list.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == ']')
                    {
                        _pos++;
                        return list;
                    }
                    throw Error("expected ',' or ']'");
                }
            }

            private void ReadWord(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0) throw Error("expected '" + word + "'");
                _pos += word.Length;
            }

            private object ReadNumber()
            {
                int start = _pos;
                if (Peek() == '-') _pos++;
                if (!(Peek() >= '0' && Peek() <= '9')) throw Error("expected a digit");
                if (Peek() == '0') _pos++;
                else while (Peek() >= '0' && Peek() <= '9') _pos++;
                if (Peek() == '.')
                {
                    _pos++;
                    if (!(Peek() >= '0' && Peek() <= '9')) throw Error("expected a digit after '.'");
                    while (Peek() >= '0' && Peek() <= '9') _pos++;
                }
                if (Peek() == 'e' || Peek() == 'E')
                {
                    _pos++;
                    if (Peek() == '+' || Peek() == '-') _pos++;
                    if (!(Peek() >= '0' && Peek() <= '9')) throw Error("expected a digit in the exponent");
                    while (Peek() >= '0' && Peek() <= '9') _pos++;
                }
                string slice = _text.Substring(start, _pos - start);
                if (ulong.TryParse(slice, NumberStyles.None, CultureInfo.InvariantCulture, out ulong unsigned)) return unsigned;
                if (long.TryParse(slice, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long signed)) return signed;
                if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error("bad number '" + slice + "'");
                return value;
            }

            private string ReadString()
            {
                Expect('"');
                StringBuilder sb = null;
                int start = _pos;
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _text[_pos];
                    if (c == '"')
                    {
                        string plain = _text.Substring(start, _pos - start);
                        _pos++;
                        return sb == null ? plain : sb.Append(plain).ToString();
                    }
                    if (c < 0x20) throw Error("control character in string");
                    if (c == '\\')
                    {
                        if (sb == null) sb = new StringBuilder();
                        sb.Append(_text, start, _pos - start);
                        _pos++;
                        if (AtEnd) throw Error("unterminated escape");
                        char e = _text[_pos++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u': sb.Append(ReadHex4()); break;
                            default: throw Error("bad escape '\\" + e + "'");
                        }
                        start = _pos;
                        continue;
                    }
                    _pos++;
                }
            }

            private char ReadHex4()
            {
                if (_pos + 4 > _text.Length) throw Error("truncated \\u escape");
                int v = 0;
                for (int i = 0; i < 4; i++)
                {
                    char h = _text[_pos++];
                    int digit = h >= '0' && h <= '9' ? h - '0'
                              : h >= 'a' && h <= 'f' ? h - 'a' + 10
                              : h >= 'A' && h <= 'F' ? h - 'A' + 10
                              : throw Error("bad hex digit '" + h + "'");
                    v = (v << 4) | digit;
                }
                return (char)v;
            }
        }
    }
}
