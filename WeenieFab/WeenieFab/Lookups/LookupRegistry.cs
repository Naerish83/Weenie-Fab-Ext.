
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WeenieFab.Lookups
{
    public sealed class LookupItem
    {
        public int Id { get; init; }
        public string Label { get; init; } = "";
        public override string ToString() => $"{Id} - {Label}";
    }

    public static class LookupRegistry
    {
        private static readonly Dictionary<string, List<LookupItem>> _tables = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;

        public static void Initialize()
        {
            if (_loaded) return;
            // Try embedded resource first
            var asm = Assembly.GetExecutingAssembly();
            var resName = asm.GetManifestResourceNames()
                             .FirstOrDefault(n => n.EndsWith("lookuptables.csv", StringComparison.OrdinalIgnoreCase));
            Stream? stream = null;
            if (resName != null) stream = asm.GetManifestResourceStream(resName);

            // Fallback to file next to exe: /Lookups/lookuptables.csv
            if (stream == null)
            {
                var path1 = Path.Combine(AppContext.BaseDirectory, "Lookups", "lookuptables.csv");
                var path2 = Path.Combine(AppContext.BaseDirectory, "lookuptables.csv");
                var path = File.Exists(path1) ? path1 : (File.Exists(path2) ? path2 : "");
                if (!string.IsNullOrEmpty(path))
                    stream = File.OpenRead(path);
            }
            if (stream == null)
            {
                _loaded = true; // nothing to load, but don't retry forever
                return;
            }

            using var sr = new StreamReader(stream);
            var header = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(header)) { _loaded = true; return; }
            var headers = SplitCsvRow(header);

            var buffers = headers.Select(_ => new List<string>()).ToArray();
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var cells = SplitCsvRow(line);
                for (int i = 0; i < Math.Min(cells.Count, headers.Count); i++)
                {
                    if (!string.IsNullOrWhiteSpace(cells[i]))
                        buffers[i].Add(cells[i]);
                }
            }

            for (int col = 0; col < headers.Count; col++)
            {
                var rawName = headers[col].Trim();
                if (string.IsNullOrWhiteSpace(rawName)) continue;
                if (rawName.StartsWith("Unnamed:", StringComparison.OrdinalIgnoreCase)) continue;

                var key = CanonicalKey(rawName);
                var list = new List<LookupItem>();

                foreach (var cell in buffers[col])
                {
                    if (TryParseCell(cell, out int id, out string label))
                        list.Add(new LookupItem { Id = id, Label = label });
                }

                // Dedup by Id (first wins), sort by Id
                var dedup = list.GroupBy(x => x.Id).Select(g => g.First()).OrderBy(x => x.Id).ToList();
                if (dedup.Count > 0)
                    _tables[key] = dedup;
            }

            _loaded = true;
        }

        public static IReadOnlyList<LookupItem> Get(string key)
        {
            Initialize();
            if (_tables.TryGetValue(key, out var v)) return v;
            // Try canonicalized
            key = CanonicalKey(key);
            if (_tables.TryGetValue(key, out v)) return v;
            return Array.Empty<LookupItem>();
        }

        private static List<string> SplitCsvRow(string row)
        {
            var res = new List<string>();
            if (row == null) return res;
            bool inQ = false;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < row.Length; i++)
            {
                var ch = row[i];
                if (ch == '"') { inQ = !inQ; continue; }
                if (ch == ',' && !inQ) { res.Add(sb.ToString()); sb.Clear(); continue; }
                sb.Append(ch);
            }
            res.Add(sb.ToString());
            return res;
        }

        // Accept "123 - Label", "0x1F - Label", "123,Label", "123 Label"
        private static bool TryParseCell(string s, out int id, out string label)
        {
            id = 0; label = "";
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            string idPart; string labelPart;
            int dash = s.IndexOf('-');
            int comma = s.IndexOf(',');
            if (dash > 0)
            {
                idPart = s.Substring(0, dash).Trim();
                labelPart = s.Substring(dash + 1).Trim();
            }
            else if (comma > 0)
            {
                idPart = s.Substring(0, comma).Trim();
                labelPart = s.Substring(comma + 1).Trim();
            }
            else
            {
                var sp = s.IndexOf(' ');
                if (sp > 0) { idPart = s.Substring(0, sp).Trim(); labelPart = s.Substring(sp + 1).Trim(); }
                else { idPart = s; labelPart = ""; }
            }

            if (idPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(idPart.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id)) return false;
            }
            else
            {
                if (!int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return false;
            }

            label = string.IsNullOrWhiteSpace(labelPart) ? id.ToString() : labelPart;
            return true;
        }

        private static string CanonicalKey(string raw)
        {
            // Strip "Table X - " prefix; collapse spaces to underscores
            var idx = raw.IndexOf(" - ", StringComparison.OrdinalIgnoreCase);
            var tail = idx >= 0 ? raw.Substring(idx + 3) : raw;
            tail = tail.Trim();
            return tail.Replace(' ', '_');
        }
    }
}
