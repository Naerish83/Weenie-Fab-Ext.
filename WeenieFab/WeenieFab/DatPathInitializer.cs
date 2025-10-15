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
        private static Type? _datManagerType;

        public static bool EnsureDatReady(Window? owner)
        {
            if (_initialized)
            {
                return _datManagerType is not null;
            }

            _datManagerType = Type.GetType("ACE.DatLoader.DatManager, ACE.DatLoader", throwOnError: false);
            if (_datManagerType is null)
            {
                _initialized = true;
                return false;
            }

            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Portal.dat",
                Filter = "Portal.dat|Portal.dat|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            var dialogResult = owner is not null ? openFileDialog.ShowDialog(owner) : openFileDialog.ShowDialog();
            if (dialogResult is not true)
            {
                _initialized = true;
                return false;
            }

            var portalPath = openFileDialog.FileName;
            var portalDirectory = Path.GetDirectoryName(portalPath);
            string? cellPath = portalDirectory is null ? null : Path.Combine(portalDirectory, "Cell.dat");
            if (cellPath is not null && !File.Exists(cellPath))
            {
                cellPath = null;
            }

            var initializeMethod = _datManagerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            initializeMethod?.Invoke(null, new object?[] { portalPath, cellPath });

            _initialized = true;
            return true;
        }

        public static bool DatAvailable =>
            Type.GetType("ACE.DatLoader.DatManager, ACE.DatLoader", throwOnError: false) is not null;
    }
}
