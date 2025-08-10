using System;
using System.Text;
using System.Windows;

namespace WeenieFab
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(e.Exception.GetType().FullName);
                sb.AppendLine(e.Exception.Message);
                sb.AppendLine();
                sb.AppendLine(e.Exception.StackTrace);

                MessageBox.Show(sb.ToString(), "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Keep app alive so you can still see the UI
                e.Handled = true;
            };
        }
    }
}
