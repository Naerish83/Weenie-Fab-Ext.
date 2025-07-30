using System;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace WeenieFab
{
    /// <summary>
    /// Interaction logic for EnhancedTools.xaml
    /// </summary>
    public partial class EnhancedTools : UserControl
    {
        private LocalToolService _service;

        public EnhancedTools()
        {
            InitializeComponent();
        }

        /// <summary>
        ///  Call this after construction to supply the LocalToolService.  The
        ///  MainWindow should create a single instance of LocalToolService and
        ///  share it with this control.  Loading of drop‑downs happens here.
        /// </summary>
        public void Initialize(LocalToolService service)
        {
            _service = service;
            try
            {
                cbIcons.ItemsSource = _service.ListIcons();
                cbMotionTables.ItemsSource = _service.ListMotionTables();
                cbSpellTables.ItemsSource = _service.ListSpellTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading lists: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResolveVisual_Click(object sender, RoutedEventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Service not initialized.");
                return;
            }
            var input = tbSetupDID.Text?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Please enter a Setup DID.");
                return;
            }
            try
            {
                using JsonDocument doc = _service.ResolveSetup(input);
                // For demonstration, simply display the raw JSON.  In a real
                // application you would parse and present fields of interest.
                MessageBox.Show(doc.RootElement.ToString(), "Setup Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to resolve setup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWeenie_Click(object sender, RoutedEventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Service not initialized.");
                return;
            }
            if (!int.TryParse(tbCopyWcid.Text, out int wcid))
            {
                MessageBox.Show("Please enter a valid numeric WCID.");
                return;
            }
            try
            {
                var weenie = _service.LoadWeenie(wcid);
                // Here you would prefill fields in the main application using
                // the loaded weenie.  For demonstration we only show the name.
                MessageBox.Show($"Loaded weenie: {weenie.ClassName}", "Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load weenie: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}