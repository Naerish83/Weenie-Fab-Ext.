using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace WeenieFab
{
    public partial class MainWindow
    {
        /// <summary>
        /// Handles Window.Closing from XAML (x:Name="MainWindow" Closing="Window_Closing").
        /// Confirms close if there are unsaved changes.
        /// </summary>
        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                if (Globals.FileChanged)
                {
                    var res = MessageBox.Show(
                        "You have unsaved changes. Do you want to exit without saving?",
                        "WeenieFab",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (res == MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            catch
            {
                // if anything goes sideways, allow close
            }
        }

        /// <summary>
        /// TextBox PreviewTextInput handler wired in XAML as IntValidationTextBox.
        /// Rejects non-integer characters (allows optional leading minus).
        /// </summary>
        private void IntValidationTextBox(object? sender, TextCompositionEventArgs e)
        {
            // allow control chars handled by WPF, block anything not digit or leading '-'
            string text = e.Text ?? string.Empty;
            bool valid = IsIntegerInput(text);
            e.Handled = !valid;
        }

        private static bool IsIntegerInput(string s)
        {
            // Accept digits; allow single leading '-' (handled by the TextBox composition)
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsDigit(c)) continue;
                if (i == 0 && c == '-') continue;
                return false;
            }
            return s.Length > 0;
        }
    }
}
