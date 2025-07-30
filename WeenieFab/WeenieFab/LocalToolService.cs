using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace WeenieFab
{
    /// <summary>
    ///  LocalToolService wraps calls to the compiled ACDatResolverCLI and the local ACE MySQL
    ///  database.  It exposes high‑level methods for resolving SetupDIDs, loading existing
    ///  weenies and listing available icons, motion tables and spell tables.  The intent
    ///  is to provide a single entry point for enhanced UI features such as the
    ///  "Enhanced Tools" tab.
    /// </summary>
    public class LocalToolService
    {
        private readonly string _cliPath;
        private readonly string _connectionString;

        /// <summary>
        ///  Initializes a new instance of the service.  The path to the compiled
        ///  ACDatResolverCLI.exe and the MySQL connection string should be supplied
        ///  externally (e.g. via configuration settings in WeenieFab).
        /// </summary>
        public LocalToolService(string cliPath, string connectionString)
        {
            _cliPath = cliPath;
            _connectionString = connectionString;
        }

        /// <summary>
        ///  Resolves a SetupDID using the ACDatResolverCLI.exe.  Returns the parsed
        ///  JSON result as a System.Text.Json JsonDocument.  Any exceptions thrown
        ///  by the process (file missing, invalid JSON, etc.) will be propagated.
        /// </summary>
        /// <param name="setupDID">Hex string such as "0x020007DA"</param>
        public JsonDocument ResolveSetup(string setupDID)
        {
            if (!File.Exists(_cliPath))
                throw new FileNotFoundException($"Resolver CLI not found at: {_cliPath}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _cliPath,
                    Arguments = $"resolve-setup {setupDID}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"Resolver CLI error: {errors}");
            }
            return JsonDocument.Parse(output);
        }

        /// <summary>
        ///  Represents a simplified weenie for use in the UI.  Only int and string
        ///  properties are shown here for brevity; extend with other property types
        ///  as needed.
        /// </summary>
        public class Weenie
        {
            public int ClassId { get; set; }
            public string ClassName { get; set; }
            public int Type { get; set; }
            public List<(int Type, int Value)> IntProperties { get; } = new();
            public List<(int Type, string Value)> StringProperties { get; } = new();
        }

        /// <summary>
        ///  Loads an existing weenie and its properties from the local MySQL database.
        ///  Throws if the weenie does not exist.  Requires the MySql.Data package.
        /// </summary>
        /// <param name="classId">WCID of the weenie</param>
        public Weenie LoadWeenie(int classId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT class_name, type FROM weenie WHERE class_id=@id";
            cmd.Parameters.AddWithValue("@id", classId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                throw new Exception($"Weenie {classId} not found.");
            var weenie = new Weenie
            {
                ClassId = classId,
                ClassName = reader.GetString(0),
                Type = reader.GetInt32(1)
            };
            reader.Close();
            // Load int properties
            cmd.CommandText = "SELECT type, value FROM weenie_properties_int WHERE class_id=@id ORDER BY type";
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    weenie.IntProperties.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
                }
            }
            // Load string properties
            cmd.CommandText = "SELECT type, value FROM weenie_properties_string WHERE class_id=@id ORDER BY type";
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    weenie.StringProperties.Add((rdr.GetInt32(0), rdr.GetString(1)));
                }
            }
            return weenie;
        }

        /// <summary>
        ///  Lists distinct icon data IDs from the database.  Assumes icons are stored in
        ///  the weenie_properties_did table with an appropriate PropertyDataId value.
        /// </summary>
        public List<uint> ListIcons()
        {
            var results = new List<uint>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT value FROM weenie_properties_d_i_d WHERE type = 1 ORDER BY value";
            // Type 1 should correspond to PropertyDataId.Icon; adjust if needed.
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                results.Add(rdr.GetUInt32(0));
            }
            return results;
        }

        /// <summary>
        ///  Lists distinct motion table data IDs from the database.  Adjust the type filter
        ///  to correspond to the correct PropertyDataId for motion tables.
        /// </summary>
        public List<uint> ListMotionTables()
        {
            var results = new List<uint>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT value FROM weenie_properties_d_i_d WHERE type = 7 ORDER BY value";
            // Type 7 is a placeholder; update according to the correct enum.
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                results.Add(rdr.GetUInt32(0));
            }
            return results;
        }

        /// <summary>
        ///  Lists distinct spell table data IDs from the database.  Adjust the type filter
        ///  based on the appropriate property type for spell tables.
        /// </summary>
        public List<uint> ListSpellTables()
        {
            var results = new List<uint>();
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT value FROM weenie_properties_d_i_d WHERE type = 51 ORDER BY value";
            // Type 51 is a placeholder; update as needed.
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                results.Add(rdr.GetUInt32(0));
            }
            return results;
        }

        /// <summary>
        ///  Checks whether the provided WCID is available (i.e. not already present in
        ///  the weenie table).  Returns true if available.
        /// </summary>
        public bool ValidateClassId(int classId)
        {
            using var conn = new MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM weenie WHERE class_id=@id";
            cmd.Parameters.AddWithValue("@id", classId);
            return cmd.ExecuteScalar() == null;
        }
    }
}
