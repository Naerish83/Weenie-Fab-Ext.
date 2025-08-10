using System;
using MySql.Data.MySqlClient;

namespace WeenieFab
{
    public static class DbConfig
    {
        // Tweak as needed
        public static string ConnectionString { get; set; } =
            "server=127.0.0.1;port=3306;uid=user;pwd=YOUR_PASSWORD;database=ace_world;SslMode=None;Default Command Timeout=5;Connection Timeout=4;"; //YOU MUST CHANGE uid + pwd to match your setup

        // When true, the app runs in offline mode and skips DB calls
        public static bool DisableDatabase { get; private set; }

        public static void GoOffline() => DisableDatabase = true;

        // Light probe — never throws; flips offline if unreachable
        public static void Probe()
        {
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                conn.Open();
                DisableDatabase = false;
            }
            catch
            {
                DisableDatabase = true;
            }
        }
    }
}

