using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WeenieFab.Adapters; // ModelViewerAdapter (optional)

namespace WeenieFab.Controls
{
    public partial class PreviewPanel : UserControl
    {
        private static readonly HttpClient Http = new() { BaseAddress = new Uri("http://127.0.0.1:7788/") };
        private const string OverrideRoot = @"C:\ACAssets\Overrides"; // match AssetBridge appsettings.json

        public PreviewPanel()
        {
            InitializeComponent();

            PreviewBtn.Click += PreviewBtn_Click;
            DidBox.KeyDown += DidBox_KeyDown;
            OpenViewerBtn.Click += OpenViewerBtn_Click;

            // drag/drop anywhere in the control
            AllowDrop = true;
            DragOver += OnDragOver;
            Drop += OnDrop;
        }

        // --- DID parsing (dec or 0xHEX) ---
        private static bool TryParseDid(string s, out uint did)
        {
            did = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (uint.TryParse(s, out did)) return true; // decimal
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
            return uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out did);
        }

        private void PreviewBtn_Click(object sender, RoutedEventArgs e) => _ = RefreshFromServerAsync();

        private void DidBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = RefreshFromServerAsync();
                e.Handled = true;
            }
        }

        private async Task RefreshFromServerAsync()
        {
            if (!TryParseDid(DidBox.Text, out var did)) return;
            var url = $"preview/icon?did={did}";
            try
            {
                using var res = await Http.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    var bytes = await res.Content.ReadAsByteArrayAsync();
                    SetImage(bytes);
                    return;
                }
            }
            catch { /* v0: silent */ }

            // Fallback: show local override if it exists
            var local = Path.Combine(OverrideRoot, $"{did}.icon.png");
            if (File.Exists(local))
            {
                var bytes = await File.ReadAllBytesAsync(local);
                SetImage(bytes);
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0 || !TryParseDid(DidBox.Text, out var did)) return;

            try
            {
                Directory.CreateDirectory(OverrideRoot);
                var dest = Path.Combine(OverrideRoot, $"{did}.icon.png");
                await TranscodeToPngAsync(files[0], dest);

                // show immediately from disk, then try the bridge (round-trip)
                SetImage(await File.ReadAllBytesAsync(dest));
                await Task.Delay(50);
                await RefreshFromServerAsync();
            }
            catch { /* v0: silent */ }
        }

        private static async Task TranscodeToPngAsync(string src, string dest)
        {
            if (string.Equals(Path.GetExtension(src), ".png", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(src, dest, overwrite: true);
                return;
            }
            await Task.Run(() =>
            {
                var bmp = new BitmapImage();
                using var fs = File.OpenRead(src);
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();

                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using var outFs = File.Create(dest);
                enc.Save(outFs);
            });
        }

        private void SetImage(byte[] bytes)
        {
            var bi = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();

            PreviewImg.Source = bi;
            Hint.Visibility = Visibility.Collapsed;
        }

        private void OpenViewerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDid(DidBox.Text, out var did)) return;

            // Prefer adapter if present (for future ACViewer integration)
            if (ModelViewerAdapter.Instance is not null)
            {
                ModelViewerAdapter.Instance.Open(did);
                return;
            }

            // Fallback: open bridge URL in browser now
            var url = $"http://127.0.0.1:7788/preview/icon?did={did}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        // --- Binding: auto-refresh when parent sets CurrentDid ---
        public static readonly DependencyProperty CurrentDidProperty =
            DependencyProperty.Register(
                nameof(CurrentDid),
                typeof(string),
                typeof(PreviewPanel),
                new PropertyMetadata(string.Empty, OnCurrentDidChanged));

        public string CurrentDid
        {
            get => (string)GetValue(CurrentDidProperty);
            set => SetValue(CurrentDidProperty, value);
        }

        private static void OnCurrentDidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (PreviewPanel)d;
            var s = (e.NewValue as string ?? string.Empty).Trim();
            panel.DidBox.Text = s;   // allow hex or decimal in the box
            _ = panel.RefreshFromServerAsync();
        }
    }
}
