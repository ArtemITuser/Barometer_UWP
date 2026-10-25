using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Barometer_UWP.Models;

namespace Barometer_UWP.Helpers
{
    /// <summary>
    /// v1.4.0.34: dependency-free JSON reader/writer for the app's data model.
    /// Replaces Newtonsoft.Json, whose netstandard1.3 compile-time closure demanded the
    /// .NET Standard 1.3 facade package graph and broke the UAP XamlCompiler type universe
    /// (WMC1006/WMC9999 'System.Runtime'). This file references ONLY assemblies that ship
    /// with the Windows SDK / netcore50 itself. Supported subset: JSON objects/arrays of
    /// string|number|bool|null, ISO-8601 dates; schema = PressureRecord / BackupFile /
    /// OAuth token response / GitHub release info. Malformed input throws FormatException.
    /// </summary>
    public static class Json
    {
        #region Writing

        private static void WriteRecords(StringBuilder sb, IEnumerable<PressureRecord> records)
        {
            sb.Append('[');
            bool first = true;
            foreach (var r in records ?? Enumerable.Empty<PressureRecord>())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"Timestamp\":\"");
                sb.Append(Esc(r.Timestamp.ToString("o", CultureInfo.InvariantCulture)));
                sb.Append("\",\"PressureHpa\":");
                sb.Append(r.PressureHpa.ToString("R", CultureInfo.InvariantCulture));
                sb.Append(",\"Source\":\"");
                sb.Append(Esc(r.Source ?? "sensor"));
                sb.Append("\"}");
            }
            sb.Append(']');
        }

        public static string SerializeRecords(IEnumerable<PressureRecord> records)
        {
            var sb = new StringBuilder();
            WriteRecords(sb, records);
            return sb.ToString();
        }

        public static string SerializeBackup(BackupMetadata meta, IEnumerable<PressureRecord> records)
        {
            var sb = new StringBuilder();
            sb.Append("{\"metadata\":{\"Filename\":\"");
            sb.Append(Esc(meta?.Filename ?? ""));
            sb.Append("\",\"CreatedAt\":\"");
            sb.Append(Esc((meta?.CreatedAt ?? DateTime.UtcNow).ToString("o", CultureInfo.InvariantCulture)));
            sb.Append("\",\"Storage\":\"");
            sb.Append(Esc(meta?.Storage ?? "Local"));
            sb.Append("\"},\"records\":");
            WriteRecords(sb, records);
            sb.Append('}');
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Reading (minimal recursive-descent parser)

        private sealed class Value
        {
            public string Str;               // string scalar (unescaped)
            public double Num;               // number scalar
            public bool Bool;
            public bool IsBool, IsNull;
            public List<Value> Arr;          // array
            public Dictionary<string, Value> Obj; // object
            public bool IsObj => Obj != null;
            public bool IsArr => Arr != null;

            public Value Get(string key)
            {
                Value v;
                return Obj != null && Obj.TryGetValue(key, out v) ? v : null;
            }
            public string AsString()
            {
                if (this == null) return null;
                if (Str != null) return Str;
                if (IsBool) return Bool ? "true" : "false";
                return Num.ToString(CultureInfo.InvariantCulture);
            }
            public double? AsNumber()
            {
                if (this == null || Str != null || IsBool || IsNull) return null;
                return Num;
            }
            public long AsLong(long def = 0)
            {
                if (this == null) return def;
                if (Str != null)
                {
                    long p;
                    return long.TryParse(Str, NumberStyles.Integer, CultureInfo.InvariantCulture, out p) ? p : def;
                }
                return IsNull || IsBool ? def : (long)Num;
            }
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            public Parser(string s) { _s = s; }

            private void SkipWs() { while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++; }
            private char Peek() { SkipWs(); return _i < _s.Length ? _s[_i] : '\0'; }

            public Value ParseValue()
            {
                char c = Peek();
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return new Value { Str = ParseString() };
                    case 't': Expect("true"); return new Value { Bool = true, IsBool = true };
                    case 'f': Expect("false"); return new Value { Bool = false, IsBool = true };
                    case 'n': Expect("null"); return new Value { IsNull = true };
                    default: return ParseNumber();
                }
            }

            private void Expect(string lit)
            {
                if (_i + lit.Length > _s.Length || string.CompareOrdinal(_s, _i, lit, 0, lit.Length) != 0)
                    throw new FormatException("Invalid JSON literal at " + _i);
                _i += lit.Length;
            }

            private Value ParseObject()
            {
                var obj = new Dictionary<string, Value>(StringComparer.Ordinal);
                _i++; // {
                if (Peek() == '}') { _i++; return new Value { Obj = obj }; }
                while (true)
                {
                    if (Peek() != '"') throw new FormatException("Expected key at " + _i);
                    string key = ParseString();
                    if (Peek() != ':') throw new FormatException("Expected ':' at " + _i);
                    _i++;
                    obj[key] = ParseValue();
                    char n = Peek();
                    if (n == ',') { _i++; continue; }
                    if (n == '}') { _i++; break; }
                    throw new FormatException("Expected ',' or '}' at " + _i);
                }
                return new Value { Obj = obj };
            }

            private Value ParseArray()
            {
                var arr = new List<Value>();
                _i++; // [
                if (Peek() == ']') { _i++; return new Value { Arr = arr }; }
                while (true)
                {
                    arr.Add(ParseValue());
                    char n = Peek();
                    if (n == ',') { _i++; continue; }
                    if (n == ']') { _i++; break; }
                    throw new FormatException("Expected ',' or ']' at " + _i);
                }
                return new Value { Arr = arr };
            }

            private string ParseString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new FormatException("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) throw new FormatException("Bad escape");
                        char e = _s[_i++];
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
                            case 'u':
                                if (_i + 4 > _s.Length) throw new FormatException("Bad \\u escape");
                                sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                _i += 4;
                                break;
                            default: throw new FormatException("Bad escape \\" + e);
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private Value ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-.eE0123456789".IndexOf(_s[_i]) >= 0) _i++;
                string raw = _s.Substring(start, _i - start);
                double d;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    throw new FormatException("Invalid number '" + raw + "'");
                return new Value { Num = d };
            }
        }

        private static Value ParseRoot(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Empty JSON");
            var p = new Parser(json);
            var v = p.ParseValue();
            p.SkipWs();
            return v;
        }

        #endregion

        #region Typed API used by services

        public static List<PressureRecord> ParseRecords(string json)
        {
            var root = ParseRoot(json);
            if (root.Arr == null) throw new FormatException("Expected a JSON array of records");
            var list = new List<PressureRecord>(root.Arr.Count);
            foreach (var item in root.Arr)
            {
                if (item.Obj == null) continue;
                var rec = new PressureRecord
                {
                    Timestamp = ParseDate(item.Get("Timestamp")),
                    PressureHpa = item.Get("PressureHpa").AsNumber() ?? double.NaN,
                    Source = item.Get("Source")?.AsString() ?? "sensor"
                };
                if (!double.IsNaN(rec.PressureHpa)) list.Add(rec);
            }
            return list;
        }

        public static BackupFile ParseBackup(string json)
        {
            var root = ParseRoot(json);
            if (root.Obj == null) throw new FormatException("Expected a backup JSON object");
            var doc = new BackupFile();
            var meta = root.Get("metadata");
            if (meta != null && meta.Obj != null)
            {
                doc.Metadata = new BackupMetadata
                {
                    Filename = meta.Get("Filename")?.AsString(),
                    CreatedAt = ParseDate(meta.Get("CreatedAt")),
                    Storage = meta.Get("Storage")?.AsString() ?? "Local"
                };
            }
            var recs = root.Get("records");
            if (recs != null && recs.Arr != null)
            {
                doc.Records = new List<PressureRecord>();
                foreach (var item in recs.Arr)
                {
                    if (item.Obj == null) continue;
                    var hpa = item.Get("PressureHpa").AsNumber();
                    if (hpa == null) continue;
                    doc.Records.Add(new PressureRecord
                    {
                        Timestamp = ParseDate(item.Get("Timestamp")),
                        PressureHpa = hpa.Value,
                        Source = item.Get("Source")?.AsString() ?? "sensor"
                    });
                }
            }
            return doc;
        }

        /// <summary>OAuth token response (access_token/refresh_token/expires_in).</summary>
        public static void ParseTokenResponse(string json, out string accessToken, out string refreshToken, out long expiresIn)
        {
            accessToken = null; refreshToken = null; expiresIn = 0;
            var root = ParseRoot(json);
            if (root.Obj == null) return;
            accessToken = root.Get("access_token")?.AsString();
            refreshToken = root.Get("refresh_token")?.AsString();
            expiresIn = root.Get("expires_in").AsLong(0);
        }

        /// <summary>Broker form-encoded-ish response probe: returns value of "code" if present.</summary>
        public static string TryGetCode(string json)
        {
            try
            {
                var root = ParseRoot(json);
                return root.Get("code")?.AsString();
            }
            catch { return null; }
        }

        /// <summary>GitHub release fields (tag_name/name/html_url/published_at).</summary>
        public static void ParseRelease(string json, out string tagName, out string name, out string htmlUrl, out DateTime publishedAt)
        {
            tagName = null; name = null; htmlUrl = null; publishedAt = default(DateTime);
            var root = ParseRoot(json);
            if (root.Obj == null) return;
            tagName = root.Get("tag_name")?.AsString();
            name = root.Get("name")?.AsString();
            htmlUrl = root.Get("html_url")?.AsString();
            publishedAt = ParseDate(root.Get("published_at"));
        }

        /// <summary>OneDrive children page: value[] items + @odata.nextLink.</summary>
        public static List<CloudItem> ParseOneDrivePage(string json, out string nextLink)
        {
            nextLink = null;
            var result = new List<CloudItem>();
            var root = ParseRoot(json);
            if (root.Obj == null) return result;
            nextLink = root.Get("@odata.nextLink")?.AsString();
            var value = root.Get("value");
            if (value == null || value.Arr == null) return result;
            foreach (var item in value.Arr)
            {
                if (item.Obj == null) continue;
                result.Add(new CloudItem
                {
                    Name = item.Get("name")?.AsString(),
                    SizeBytes = item.Get("size").AsLong(0),
                    LastModified = ParseDate(item.Get("lastModifiedDateTime"))
                });
            }
            return result;
        }

        private static DateTime ParseDate(Value v)
        {
            string s = v?.AsString();
            DateTime dt;
            if (!string.IsNullOrEmpty(s) &&
                DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out dt))
                return dt;
            return default(DateTime);
        }

        public sealed class CloudItem
        {
            public string Name;
            public long SizeBytes;
            public DateTime LastModified;
        }

        #endregion
    }
}
