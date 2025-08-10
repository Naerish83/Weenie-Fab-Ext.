using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WeenieFab
{
    public partial class DidValuePicker : UserControl
    {
        public DidValuePicker() { InitializeComponent(); }

        private DatCatalogService _svc;

        public static readonly DependencyProperty DidTypeProperty =
            DependencyProperty.Register(nameof(DidType), typeof(int), typeof(DidValuePicker),
                new PropertyMetadata(0, (d, _) => ((DidValuePicker)d).Refresh("")));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(DidValuePicker),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (d, e) => ((DidValuePicker)d).SetIndicator((int)e.NewValue)));

        public int DidType
        {
            get => (int)GetValue(DidTypeProperty);
            set => SetValue(DidTypeProperty, value);
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this)) return;

            try
            {
                var owner = System.Windows.Window.GetWindow(this);
                DatPathInitializer.EnsureDatReady(owner);
            }
            catch (Exception ex)
            {
                var owner = System.Windows.Window.GetWindow(this);
                if (owner != null) MessageBox.Show(owner, $"DAT not ready: {ex.Message}", "WeenieFab",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                else MessageBox.Show($"DAT not ready: {ex.Message}", "WeenieFab",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _svc ??= new DatCatalogService(DbConfig.ConnectionString);

            combo.SelectionChanged += (s, _) =>
            {
                if (combo.SelectedValue is int did) Value = did;
                else if (DatCatalogService.TryParseDid(combo.Text, out var parsed)) Value = parsed;
                SetIndicator(Value);
            };

            combo.LostFocus += (s, _) =>
            {
                if (DatCatalogService.TryParseDid(combo.Text, out var did)) Value = did;
                SetIndicator(Value);
            };

            Refresh("");
        }

        private void OnQueryChanged(object sender, System.Windows.Input.KeyEventArgs e)
            => Refresh(combo.Text ?? "");

        private void Refresh(string query)
        {
            if (_svc == null) return;

            List<DatCatalogService.DidOption> options;
            try
            {
                options = string.IsNullOrWhiteSpace(query)
                    ? _svc.GetDidOptions(DidType)
                    : _svc.SearchDidOptions(DidType, query);
            }
            catch (Exception ex)
            {
                var owner = System.Windows.Window.GetWindow(this);
                if (owner != null) MessageBox.Show(owner, $"Error loading DIDs for type {DidType}: {ex.Message}",
                        "WeenieFab", MessageBoxButton.OK, MessageBoxImage.Error);
                else MessageBox.Show($"Error loading DIDs for type {DidType}: {ex.Message}",
                        "WeenieFab", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            combo.ItemsSource = options;

            var match = options.FirstOrDefault(o => o.Did == Value);
            if (match != null) combo.SelectedItem = match;

            SetIndicator(Value);
        }

        private void SetIndicator(int did)
        {
            try
            {
                var opts = combo.ItemsSource as IEnumerable<DatCatalogService.DidOption>;
                var hit = opts?.FirstOrDefault(o => o.Did == did);
                bool ok = hit?.ExistsInDat ?? false;

                status.Fill = ok ? Brushes.LightGreen : Brushes.OrangeRed;
                status.ToolTip = ok ? "Found in DAT" : "Not found in DAT (id will still save)";
            }
            catch
            {
                status.Fill = Brushes.LightGray;
                status.ToolTip = "Unknown DAT status";
            }
        }
    }
}
