using ExtractNow.Services;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Input;

namespace ExtractNow
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settings;
        private readonly Extractor _extractor;
        private CancellationTokenSource? _cts;
    private System.Windows.Forms.NotifyIcon? _tray;
    private int _lastPercent;
    private bool _openedOutputFolderThisRun;
    private bool _extractionCompleted;
    private static string? _lastOpenedOutputFolder;
    private string? _currentOutputDir;

        // Routed commands for keyboard shortcuts
        public static readonly RoutedUICommand SettingsCommand = new("Settings", nameof(SettingsCommand), typeof(MainWindow));
        public static readonly RoutedUICommand ExtractCommand = new("Extract", nameof(ExtractCommand), typeof(MainWindow));
        public static readonly RoutedUICommand OpenFolderCommand = new("OpenFolder", nameof(OpenFolderCommand), typeof(MainWindow));
        public static readonly RoutedUICommand CancelCommand = new("Cancel", nameof(CancelCommand), typeof(MainWindow));
        public static readonly RoutedUICommand ExitCommand = new("Exit", nameof(ExitCommand), typeof(MainWindow));

        public MainWindow()
        {
            InitializeComponent();
            _settings = new SettingsService();
            _extractor = new Extractor(_settings);

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            try { SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged; } catch { }
            // Initialize theme-dependent colors immediately
            try { UpdateDropHintTheme(); } catch { }

            // Command bindings
            CommandBindings.Add(new CommandBinding(SettingsCommand, (s, e) => SettingsButton_Click(s, e))); // no special CanExecute
            CommandBindings.Add(new CommandBinding(ExtractCommand, (s, e) => BrowseButton_Click(s, e), (s, e) => e.CanExecute = _cts == null));
            CommandBindings.Add(new CommandBinding(OpenFolderCommand, (s, e) => OpenOutputButton_Click(s, e), (s, e) => e.CanExecute = OpenOutputButton != null && OpenOutputButton.IsEnabled));
            CommandBindings.Add(new CommandBinding(CancelCommand, (s, e) => CancelButton_Click(s, e), (s, e) => e.CanExecute = _cts != null));
            CommandBindings.Add(new CommandBinding(ExitCommand, (s, e) => ExitButton_Click(s, e))); // always executable

            // Fallback hotkey handling: ensure shortcuts work even when a child (e.g., TextBox) eats the gesture
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Loaded += RestoreWindowSize;
            Closing += SaveWindowSize;
        }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.OemComma: // Ctrl + , → Settings
                            SettingsButton_Click(this, new RoutedEventArgs());
                            e.Handled = true;
                            break;
                        case Key.O: // Ctrl + O → Extract
                            if (_cts == null)
                            {
                                BrowseButton_Click(this, new RoutedEventArgs());
                                e.Handled = true;
                            }
                            break;
                        case Key.E: // Ctrl + E → Open extracted folder
                            // Use FindName to avoid generated name resolution issues during static analysis
                            if (FindName("OpenOutputButton") is System.Windows.Controls.Button b && b.IsEnabled)
                            {
                                OpenOutputButton_Click(this, new RoutedEventArgs());
                                e.Handled = true;
                            }
                            break;
                        case Key.C: // Ctrl + C → Cancel
                            if (_cts != null)
                            {
                                CancelButton_Click(this, new RoutedEventArgs());
                                e.Handled = true;
                            }
                            break;
                        case Key.W: // Ctrl + W → Exit
                            ExitButton_Click(this, new RoutedEventArgs());
                            e.Handled = true;
                            break;
                    }
                }
            }
            catch { }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Handle command-line archive path
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var candidate = args[1];
                if (File.Exists(candidate))
                {
                    // Guard against concurrent extraction (in case Loaded fires multiple times or another trigger is active)
                    if (_cts != null) return;
                    await StartExtraction(candidate);
                }
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            try { SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged; } catch { }
        }

        private void RestoreWindowSize(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_settings.RestoreDefaultWindowSizeOnRestart)
                {
                    WindowState = WindowState.Normal;
                    Width = 720;
                    Height = 580;
                    try
                    {
                        // Center on current work area (respects taskbar and DPI)
                        var wa = SystemParameters.WorkArea;
                        Left = wa.Left + (wa.Width - Width) / 2;
                        Top = wa.Top + (wa.Height - Height) / 2;
                    }
                    catch { }
                }
                else if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
                {
                    Width = _settings.WindowWidth;
                    Height = _settings.WindowHeight;
                }
                if (!_settings.RestoreDefaultWindowSizeOnRestart && _settings.WindowMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }
            catch { }
        }

        private void SaveWindowSize(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Only save window geometry if user has disabled "Restore default on restart"
                // This prevents polluting settings.json with window size when user wants defaults
                if (!_settings.RestoreDefaultWindowSizeOnRestart)
                {
                    _settings.WindowMaximized = WindowState == WindowState.Maximized;
                    if (WindowState == WindowState.Normal)
                    {
                        _settings.WindowWidth = (int)Math.Round(Width);
                        _settings.WindowHeight = (int)Math.Round(Height);
                    }
                }
            }
            catch { }
        }

        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.VisualStyle)
            {
                try { Dispatcher.Invoke(UpdateDropHintTheme); } catch { }
            }
        }

        private static bool IsOsDarkMode()
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                if (key != null)
                {
                    var v = key.GetValue("AppsUseLightTheme");
                    if (v is int i)
                    {
                        return i == 0; // 0 = dark mode for apps
                    }
                }
            }
            catch { }
            return false; // default to light
        }

        private void UpdateDropHintTheme()
        {
            if (DropHintText == null) return;
            bool dark = IsOsDarkMode();
            // Use high-contrast friendly defaults; light gray on dark, dark gray on light
            var color = dark ? System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)
                             : System.Windows.Media.Color.FromArgb(0x77, 0x00, 0x00, 0x00);
            DropHintText.Foreground = new SolidColorBrush(color);
        }

        public async Task StartExtraction(string archivePath)
        {
            // Guard: If an extraction is already in progress, abort immediately to prevent double extraction
            if (_cts != null)
            {
                AppendLog($"Extraction already in progress. Ignoring request for: {archivePath}");
                return;
            }

            _openedOutputFolderThisRun = false;
            _extractionCompleted = false;
            LogBox.Clear();
            StatusText.Text = "Starting…";
            Progress.IsIndeterminate = true;
            Progress.Value = 0;
            CancelButton.IsEnabled = true;
            _lastPercent = 0;

            if (!File.Exists(archivePath))
            {
                AppendLog($"Archive not found: {archivePath}");
                StatusText.Text = "Archive not found";
                return;
            }

            // 7-Zip is expected to be bundled under app's 7zip folder; Extractor will validate presence

            _cts = new CancellationTokenSource();
            var progress = new Progress<int>(p =>
            {
                Progress.IsIndeterminate = false;
                Progress.Value = Math.Clamp(p, 0, 100);
                StatusText.Text = $"Extracting… {p}%";
                _lastPercent = Math.Clamp(p, 0, 100);
                UpdateTrayIcon(_lastPercent);
            });
            var log = new Progress<string>(line => AppendLog(line));

            string? outDir = null;
            OpenOutputButton.IsEnabled = false; // reset per run
            try
            {
                outDir = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath));
                _currentOutputDir = outDir;
                Directory.CreateDirectory(outDir);
                AppendLog($"Output folder: {outDir}");
                EnsureTrayIcon();
                // Ensure some visible fill even if extraction is very fast
                UpdateTrayIcon(Math.Max(1, _lastPercent));
                var result = await _extractor.ExtractAsync(archivePath, outDir, progress, log, _cts.Token);
                if (result.Success)
                {
                    Progress.Value = 100;
                    StatusText.Text = "Done";
                    AppendLog("Extraction completed successfully.");
                    _extractionCompleted = true;
                    OpenOutputButton.IsEnabled = true; // enable manual open
                    OpenOutputFolderIfEnabled(outDir);

                    if (_settings.CloseAppAfterExtraction)
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await Task.Delay(200); } catch { }
                            try { Dispatcher.Invoke(() => Close()); } catch { }
                        });
                    }
                }
                else
                {
                    StatusText.Text = "Failed";
                    AppendLog("Extraction failed: " + result.ErrorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Canceled";
                AppendLog("Extraction canceled by user.");
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error";
                AppendLog("Unexpected error: " + ex.Message);
            }
            finally
            {
                CancelButton.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
                DisposeTrayIcon();
            }
        }

        private void AppendLog(string text)
        {
            LogBox.AppendText(text + Environment.NewLine);
            LogBox.ScrollToEnd();
        }

        private void OpenOutputFolderIfEnabled(string? outDir)
        {
            try
            {
                if (!_settings.OpenOutputFolderOnComplete) return;
                if (!_extractionCompleted) return;
                if (_openedOutputFolderThisRun) return;
                if (string.IsNullOrWhiteSpace(outDir) || !Directory.Exists(outDir)) return;
                if (string.Equals(_lastOpenedOutputFolder, outDir, StringComparison.OrdinalIgnoreCase)) return;

                _openedOutputFolderThisRun = true;
                _lastOpenedOutputFolder = outDir;
                AppendLog($"Opening output folder: {outDir}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{outDir}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendLog("Failed to open output folder: " + ex.Message);
            }
        }

        private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_currentOutputDir) && Directory.Exists(_currentOutputDir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{_currentOutputDir}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                AppendLog("Failed to open folder: " + ex.Message);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wnd = new Views.AboutWindow();
                wnd.Owner = this;
                wnd.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                wnd.ShowDialog();
            }
            catch { }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select an archive",
                Filter = "Archives|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz;*.zst;*.iso|All files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                await StartExtraction(dlg.FileName);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new Views.SettingsWindow(_settings);
            wnd.Owner = this;
            wnd.ShowDialog();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog before canceling extraction
            if (_cts != null)
            {
                var result = System.Windows.MessageBox.Show(
                    this,
                    "Do you want to cancel the extraction in progress?",
                    "Cancel Extraction",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.Yes)
                {
                    _cts.Cancel();
                }
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch { }
        }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            // Prevent bubbling to parent handlers and avoid duplicate extractions
            e.Handled = true;
            if (_cts != null)
            {
                // Extraction already in progress; ignore new drops
                return;
            }
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    await StartExtraction(files[0]);
                }
            }
        }

        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }
    }

    // Tray icon helpers
    partial class MainWindow
    {
        [DllImport("user32.dll", SetLastError = false)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private void EnsureTrayIcon()
        {
            try
            {
                if (!_settings.ShowTrayIconDuringExtraction) return;
                if (_tray != null) return;

                _tray = new NotifyIcon
                {
                    Text = "ExtractNow",
                    Visible = true
                };
                _tray.Click += (s, e) =>
                {
                    try
                    {
                        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                        Show();
                        Activate();
                        Topmost = true; Topmost = false; // bring to front
                    }
                    catch { }
                };
                UpdateTrayIcon(1);
            }
            catch { }
        }

        private void DisposeTrayIcon()
        {
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Icon?.Dispose();
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { }
        }

        private void UpdateTrayIcon(int percent)
        {
            try
            {
                if (_tray == null || !_settings.ShowTrayIconDuringExtraction) return;
                // Render at classic tray size 16x16
                using var bmp = new System.Drawing.Bitmap(16, 16);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                    // Draw a single dark grey square outline spanning most of the icon
                    var square = new System.Drawing.Rectangle(1, 1, 14, 14); // 1px margin
                    using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 90, 90, 90), 1);
                    g.DrawRectangle(borderPen, square);

                    // Green progress fill inside the square (bottom -> top)
                    var inner = new System.Drawing.Rectangle(square.X + 1, square.Y + 1, square.Width - 2, square.Height - 2);
                    int fillHeight = percent <= 0 ? 0 : (int)Math.Round(inner.Height * Math.Clamp(percent, 0, 100) / 100.0);
                    if (fillHeight > 0)
                    {
                        using var fillBrush = new System.Drawing.SolidBrush(System.Drawing.Color.LimeGreen);
                        var y = inner.Y + (inner.Height - fillHeight);
                        g.FillRectangle(fillBrush, new System.Drawing.Rectangle(inner.X, y, inner.Width, fillHeight));
                    }
                }
                var hIcon = bmp.GetHicon();
                var srcIcon = System.Drawing.Icon.FromHandle(hIcon);
                // Clone to manage lifetime and avoid GDI leaks/black backgrounds, then destroy handle
                var icon = (System.Drawing.Icon)srcIcon.Clone();
                DestroyIcon(hIcon);
                srcIcon.Dispose();
                // Dispose previous icon
                _tray.Icon?.Dispose();
                _tray.Icon = icon;
            }
            catch { }
        }
    }
}
