using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MySql.Data.MySqlClient;

namespace WeenieFab
{
    public sealed class DatCatalogService
    {
        private readonly string _connString;
        private readonly System.Collections.IDictionary? _allFilesDict;

        public DatCatalogService(string connString)
        {
            _connString = connString ?? throw new ArgumentNullException(nameof(connString));

            // Reflective DAT presence (optional)
            var datManagerType = Type.GetType("ACE.DatLoader.DatManager, ACE.DatLoader", throwOnError: false);
            if (datManagerType != null)
            {
                var piPortal = datManagerType.GetProperty("PortalDat", BindingFlags.Public | BindingFlags.Static);
                var portalDat = piPortal?.GetValue(null);
                if (portalDat != null)
                {
                    var piAllFiles = portalDat.GetType().GetProperty("AllFiles", BindingFlags.Public | BindingFlags.Instance);
                    _allFilesDict = piAllFiles?.GetValue(portalDat) as System.Collections.IDictionary;
                }
            }
        }

        public sealed class DidOption
        {
            public int Did { get; set; }
            public string Hex => $"0x{Did:X8}";
            public string Label => $"{Did} ({Hex})";
            public bool ExistsInDat { get; set; }
        }

        public List<DidOption> GetDidOptions(int didType, int limit = 100000)
        {
            // Offline mode → return empty; picker still accepts manual input
            if (DbConfig.DisableDatabase) return new List<DidOption>();

            var list = new List<DidOption>();

            try
            {
                using var conn = new MySqlConnection(_connString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT DISTINCT value
                    FROM weenie_properties_d_i_d
                    WHERE type = @type
                    ORDER BY value
                    LIMIT @limit;";
                cmd.Parameters.AddWithValue("@type", didType);
                cmd.Parameters.AddWithValue("@limit", limit);

                using var rd = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
                while (rd.Read())
                {
                    var did = rd.GetInt32(0);
                    list.Add(new DidOption
                    {
                        Did = did,
                        ExistsInDat = _allFilesDict?.Contains((uint)did) ?? false
                    });
                }
            }
            catch
            {
                // Flip to offline for rest of session
                DbConfig.GoOffline();
                return new List<DidOption>();
            }

            return list;
        }

        public List<DidOption> SearchDidOptions(int didType, string query, int take = 200)
        {
            var all = GetDidOptions(didType);
            if (string.IsNullOrWhiteSpace(query))
                return all.Take(take).ToList();

            query = query.Trim();
            bool qIsHex = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            bool qIsInt = int.TryParse(query, out _);

            IEnumerable<DidOption> filtered = all;

            if (qIsHex)
                filtered = filtered.Where(o => o.Hex.Contains(query, StringComparison.OrdinalIgnoreCase));
            else if (qIsInt)
                filtered = filtered.Where(o => o.Did.ToString().Contains(query, StringComparison.OrdinalIgnoreCase));
            else
                filtered = filtered.Where(o => o.Label.Contains(query, StringComparison.OrdinalIgnoreCase));

            return filtered.Take(take).ToList();
        }

        public static bool TryParseDid(string text, out int did)
        {
            did = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out did);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out did);
        }
    }
}
