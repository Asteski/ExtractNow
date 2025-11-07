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

            // Defensive: if XAML names change, null checks avoid crashes.
            if (FindName("ShowOnAssocCheck") is System.Windows.Controls.CheckBox showOnAssoc)
                showOnAssoc.IsChecked = _settings.ShowWindowOnAssociationLaunch;
            if (FindName("ShowTrayIconCheck") is System.Windows.Controls.CheckBox trayCheck)
                trayCheck.IsChecked = _settings.ShowTrayIconDuringExtraction;
            if (FindName("ThresholdMbBox") is System.Windows.Controls.TextBox thresholdBox)
                thresholdBox.Text = _settings.ShowWindowThresholdMB > 0 ? _settings.ShowWindowThresholdMB.ToString() : string.Empty;
            if (FindName("OpenFolderOnCompleteCheck") is System.Windows.Controls.CheckBox openFolderCheck)
                openFolderCheck.IsChecked = _settings.OpenOutputFolderOnComplete;
            if (FindName("CloseAppAfterExtractionCheck") is System.Windows.Controls.CheckBox closeAppCheck)
                closeAppCheck.IsChecked = _settings.CloseAppAfterExtraction;
            if (FindName("SevenZipPathBox") is System.Windows.Controls.TextBox sevenZipPathBox)
                sevenZipPathBox.Text = GetDisplaySevenZipPath();

            _selectedSevenZipPath = string.IsNullOrWhiteSpace(_settings.SevenZipPath) ? null : _settings.SevenZipPath;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save window visibility preference only and close the settings window
            // Retrieve controls dynamically (robust to XAML regeneration issues)
            var showOnAssoc = FindName("ShowOnAssocCheck") as System.Windows.Controls.CheckBox;
            var trayCheck = FindName("ShowTrayIconCheck") as System.Windows.Controls.CheckBox;
            var thresholdBox = FindName("ThresholdMbBox") as System.Windows.Controls.TextBox;
            var openFolderCheck = FindName("OpenFolderOnCompleteCheck") as System.Windows.Controls.CheckBox;
            var closeAppCheck = FindName("CloseAppAfterExtractionCheck") as System.Windows.Controls.CheckBox;

            _settings.ShowWindowOnAssociationLaunch = showOnAssoc?.IsChecked == true;
            _settings.ShowTrayIconDuringExtraction = trayCheck?.IsChecked == true;
            if (int.TryParse(thresholdBox?.Text.Trim() ?? string.Empty, out var mb) && mb > 0)
            {
                _settings.ShowWindowThresholdMB = mb;
            }
            else
            {
                _settings.ShowWindowThresholdMB = 0;
            }
            _settings.OpenOutputFolderOnComplete = openFolderCheck?.IsChecked == true;
            _settings.CloseAppAfterExtraction = closeAppCheck?.IsChecked == true;

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
                    if (FindName("SevenZipPathBox") is System.Windows.Controls.TextBox sevenZipPathBox)
                        sevenZipPathBox.Text = GetDisplaySevenZipPath();
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
            if (FindName("SevenZipPathBox") is System.Windows.Controls.TextBox sevenZipPathBox)
                sevenZipPathBox.Text = GetDisplaySevenZipPath();
        }

        
    }
}
