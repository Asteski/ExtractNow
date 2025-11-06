using ExtractNow.Services;
using System;
using System.Windows;

namespace ExtractNow.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settings;
        private string? _selectedSevenZipPath; // null -> default app folder

        public SettingsWindow(SettingsService settings)
        {
            InitializeComponent();
            _settings = settings;
            ShowOnAssocCheck.IsChecked = _settings.ShowWindowOnAssociationLaunch;
            ShowTrayIconCheck.IsChecked = _settings.ShowTrayIconDuringExtraction;
            ThresholdMbBox.Text = _settings.ShowWindowThresholdMB > 0 ? _settings.ShowWindowThresholdMB.ToString() : string.Empty;

            // Initialize 7-Zip path UI
            _selectedSevenZipPath = string.IsNullOrWhiteSpace(_settings.SevenZipPath) ? null : _settings.SevenZipPath;
            SevenZipPathBox.Text = GetDisplaySevenZipPath();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save window visibility preference only and close the settings window
            _settings.ShowWindowOnAssociationLaunch = ShowOnAssocCheck.IsChecked == true;
            _settings.ShowTrayIconDuringExtraction = ShowTrayIconCheck.IsChecked == true;
            if (int.TryParse(ThresholdMbBox.Text.Trim(), out var mb) && mb > 0)
            {
                _settings.ShowWindowThresholdMB = mb;
            }
            else
            {
                _settings.ShowWindowThresholdMB = 0;
            }

            // Validate selected 7-Zip path if custom, else accept default
            if (_selectedSevenZipPath != null && !IsValidSevenZipFolder(_selectedSevenZipPath))
            {
                System.Windows.MessageBox.Show(this,
                    "The selected 7-Zip folder does not contain 7z.exe (or 7zG.exe) and 7z.dll. Please select a valid folder.",
                    "Invalid 7-Zip path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // keep the window open and force user to pick again
            }

            _settings.SevenZipPath = _selectedSevenZipPath; // null => default
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CleanupAssocButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var removed = FileAssociations.CleanupAssociationRegistryEntries();
                System.Windows.MessageBox.Show(this,
                    removed > 0 ? $"Removed {removed} association-related entries from your user registry." : "No association entries found to remove.",
                    "Cleanup complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to clean up entries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetDisplaySevenZipPath()
        {
            // Always display the effective folder path without any suffix
            var effective = _selectedSevenZipPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "7zip");
            return effective;
        }

        private static bool IsValidSevenZipFolder(string folder)
        {
            try
            {
                var sevenZip = System.IO.Path.Combine(folder, "7z.exe");
                var sevenZipGui = System.IO.Path.Combine(folder, "7zG.exe");
                var sevenZipDll = System.IO.Path.Combine(folder, "7z.dll");
                bool exeOk = System.IO.File.Exists(sevenZip) || System.IO.File.Exists(sevenZipGui);
                bool dllOk = System.IO.File.Exists(sevenZipDll);
                return exeOk && dllOk;
            }
            catch { return false; }
        }

        private void SelectSevenZip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select the folder that contains 7z.exe and 7z.dll",
                    ShowNewFolderButton = false
                };
                var result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    if (!IsValidSevenZipFolder(dlg.SelectedPath))
                    {
                        System.Windows.MessageBox.Show(this,
                            "The selected folder doesn't contain required 7-Zip files (7z.exe/7zG.exe and 7z.dll).",
                            "Invalid folder",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    _selectedSevenZipPath = dlg.SelectedPath;
                    SevenZipPathBox.Text = GetDisplaySevenZipPath();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreSevenZip_Click(object sender, RoutedEventArgs e)
        {
            _selectedSevenZipPath = null; // default
            SevenZipPathBox.Text = GetDisplaySevenZipPath();
        }

        
    }
}
