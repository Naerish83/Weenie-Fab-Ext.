using System;
using System.Globalization;
using System.Windows;

namespace WeenieFab
{
    // This shim lets legacy code keep using tbDiDValue.Text.
    // It proxies get/set to the new DidValuePicker (x:Name="didPicker").
    public partial class MainWindow : Window
    {
        private TbDidValueShim _tbDidValueShim;

        // Expose a property named exactly like the old TextBox field.
        public TbDidValueShim tbDiDValue => _tbDidValueShim ??= new TbDidValueShim(this);
    }

    public sealed class TbDidValueShim
    {
        private readonly MainWindow _win;

        public TbDidValueShim(MainWindow win) => _win = win;

        // Mirror TextBox.Text API used by legacy handlers.
        public string Text
        {
            get
            {
                var dp = _win.FindName("didPicker") as DidValuePicker;
                if (dp == null) return string.Empty;
                return dp.Value.ToString(CultureInfo.InvariantCulture);
            }
            set
            {
                var dp = _win.FindName("didPicker") as DidValuePicker;
                if (dp == null) return;

                if (string.IsNullOrWhiteSpace(value))
                {
                    dp.Value = 0;
                    return;
                }

                value = value.Trim();

                // Accept decimal or 0xHEX
                int parsed;
                if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
                        dp.Value = parsed;
                }
                else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    dp.Value = parsed;
                }
                // else: ignore invalid text (keeps previous value)
            }
        }
    }
}
