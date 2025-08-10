#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace WeenieFab
{
    public static class DatPathInitializer
    {
        private static bool _initialized;
        private static Type? _datManagerType; // Marked as nullable

        // Returns true if ACE.DatLoader is available and initialized
        public static bool EnsureDatReady(Window? owner)
        {
            if (_initialized) return _datManagerType != null;

            // Try to find ACE.DatLoader.DatManager by reflection (optional)
            _datManagerType = Type.GetType("ACE.DatLoader.DatManager, ACE.DatLoader", throwOnError: false);
            if (_datManagerType == null)
            {
                _initialized = true; // run without DAT
                return false;
            }

            var ofd = new OpenFileDialog
            {
                Title = "Select Portal.dat",
                Filter = "Portal.dat|Portal.dat|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            bool? res;
            // If we have an owner, use that overload; otherwise use parameterless to avoid null crash
            if (owner != null) res = ofd.ShowDialog(owner);
            else res = ofd.ShowDialog();

            if (res != true)
            {
                _initialized = true;   // allow continuing without DAT selection
                return false;
            }

            var portalPath = ofd.FileName;
            var cellPath = Path.Combine(Path.GetDirectoryName(portalPath) ?? "", "Cell.dat");
            if (!File.Exists(cellPath)) cellPath = null;

            // Call DatManager.Initialize(portalPath, cellPath) via reflection
            var mi = _datManagerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            mi?.Invoke(null, new object?[] { portalPath, cellPath });

            _initialized = true;
            return true;
        }

        public static bool DatAvailable =>
            Type.GetType("ACE.DatLoader.DatManager, ACE.DatLoader", throwOnError: false) != null;
    }
}
