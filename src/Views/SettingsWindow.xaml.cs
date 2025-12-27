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

            // Initialize selected path from persisted settings BEFORE populating the textbox
            _selectedSevenZipPath = string.IsNullOrWhiteSpace(_settings.SevenZipPath) ? null : _settings.SevenZipPath;

            // Defensive: if XAML names change, null checks avoid crashes.
            if (FindName("ShowOnAssocCheck") is System.Windows.Controls.CheckBox showOnAssoc)
                showOnAssoc.IsChecked = _settings.ShowWindowOnAssociationLaunch;
            if (FindName("ShowTrayIconCheck") is System.Windows.Controls.CheckBox trayCheck)
                trayCheck.IsChecked = _settings.ShowTrayIconDuringExtraction;
            if (FindName("ThresholdMbBox") is System.Windows.Controls.TextBox thresholdBox)
                thresholdBox.Text = _settings.ShowWindowThresholdMB > 0 ? _settings.ShowWindowThresholdMB.ToString() : string.Empty;
            if (FindName("OpenFolderOnCompleteCheck") is System.Windows.Controls.CheckBox openFolderCheck)
            {
                openFolderCheck.IsChecked = _settings.OpenOutputFolderOnComplete;
                openFolderCheck.Checked += OpenFolderOnCompleteCheck_Changed;
                openFolderCheck.Unchecked += OpenFolderOnCompleteCheck_Changed;
            }
            if (FindName("ReuseExplorerWindowsCheck") is System.Windows.Controls.CheckBox reuseWindowCheck)
                reuseWindowCheck.IsChecked = _settings.ReuseExplorerWindows;
            if (FindName("CloseAppAfterExtractionCheck") is System.Windows.Controls.CheckBox closeAppCheck)
                closeAppCheck.IsChecked = _settings.CloseAppAfterExtraction;
            if (FindName("SevenZipPathBox") is System.Windows.Controls.TextBox sevenZipPathBox)
                sevenZipPathBox.Text = GetDisplaySevenZipPath();
            if (FindName("RestoreDefaultWindowSizeCheck") is System.Windows.Controls.CheckBox restoreDefaultCheck)
                restoreDefaultCheck.IsChecked = _settings.RestoreDefaultWindowSizeOnRestart;
            if (FindName("ShowNotificationOnCompleteCheck") is System.Windows.Controls.CheckBox notificationCheck)
                notificationCheck.IsChecked = _settings.ShowNotificationOnComplete;
            
            // Initialize size threshold settings
            if (FindName("EnableSizeThresholdCheck") is System.Windows.Controls.CheckBox enableThresholdCheck)
                enableThresholdCheck.IsChecked = _settings.EnableSizeThreshold;
            if (FindName("MaxArchiveSizeMBBox") is System.Windows.Controls.TextBox maxSizeBox)
                maxSizeBox.Text = _settings.MaxArchiveSizeMB.ToString();
            if (FindName("OversizedActionCombo") is System.Windows.Controls.ComboBox actionCombo)
            {
                // Select the appropriate ComboBoxItem based on saved setting
                foreach (System.Windows.Controls.ComboBoxItem item in actionCombo.Items)
                {
                    if (item.Tag?.ToString() == _settings.OversizedArchiveAction)
                    {
                        actionCombo.SelectedItem = item;
                        break;
                    }
                }
                // Default to first item if nothing matched
                if (actionCombo.SelectedItem == null && actionCombo.Items.Count > 0)
                    actionCombo.SelectedIndex = 0;
            }
            
            // Initialize the enabled state of ReuseExplorerWindows based on OpenFolderOnComplete
            UpdateReuseExplorerWindowsState();
        }

        private void OpenFolderOnCompleteCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateReuseExplorerWindowsState();
        }

        private void UpdateReuseExplorerWindowsState()
        {
            var openFolderCheck = FindName("OpenFolderOnCompleteCheck") as System.Windows.Controls.CheckBox;
            var reuseWindowCheck = FindName("ReuseExplorerWindowsCheck") as System.Windows.Controls.CheckBox;
            
            if (reuseWindowCheck != null)
            {
                reuseWindowCheck.IsEnabled = openFolderCheck?.IsChecked == true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save window visibility preference only and close the settings window
            // Retrieve controls dynamically (robust to XAML regeneration issues)
            var showOnAssoc = FindName("ShowOnAssocCheck") as System.Windows.Controls.CheckBox;
            var trayCheck = FindName("ShowTrayIconCheck") as System.Windows.Controls.CheckBox;
            var thresholdBox = FindName("ThresholdMbBox") as System.Windows.Controls.TextBox;
            var openFolderCheck = FindName("OpenFolderOnCompleteCheck") as System.Windows.Controls.CheckBox;
            var reuseWindowCheck = FindName("ReuseExplorerWindowsCheck") as System.Windows.Controls.CheckBox;
            var closeAppCheck = FindName("CloseAppAfterExtractionCheck") as System.Windows.Controls.CheckBox;
            var restoreDefaultCheck = FindName("RestoreDefaultWindowSizeCheck") as System.Windows.Controls.CheckBox;
            var notificationCheck = FindName("ShowNotificationOnCompleteCheck") as System.Windows.Controls.CheckBox;
            var sevenZipPathBox = FindName("SevenZipPathBox") as System.Windows.Controls.TextBox;
            var enableThresholdCheck = FindName("EnableSizeThresholdCheck") as System.Windows.Controls.CheckBox;
            var maxSizeBox = FindName("MaxArchiveSizeMBBox") as System.Windows.Controls.TextBox;
            var actionCombo = FindName("OversizedActionCombo") as System.Windows.Controls.ComboBox;

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
            _settings.ReuseExplorerWindows = reuseWindowCheck?.IsChecked == true;
            _settings.CloseAppAfterExtraction = closeAppCheck?.IsChecked == true;
            _settings.RestoreDefaultWindowSizeOnRestart = restoreDefaultCheck?.IsChecked == true;
            _settings.ShowNotificationOnComplete = notificationCheck?.IsChecked == true;

            // Save size threshold settings
            _settings.EnableSizeThreshold = enableThresholdCheck?.IsChecked == true;
            if (int.TryParse(maxSizeBox?.Text.Trim() ?? string.Empty, out var maxSizeMB) && maxSizeMB > 0)
            {
                _settings.MaxArchiveSizeMB = maxSizeMB;
            }
            else
            {
                _settings.MaxArchiveSizeMB = 1000; // default
            }
            if (actionCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                _settings.OversizedArchiveAction = selectedItem.Tag?.ToString() ?? "Explorer";
            }

            // Determine 7-Zip path from typed value (or default)
            var typed = sevenZipPathBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(typed))
            {
                _selectedSevenZipPath = null; // use default bundled path
            }
            else
            {
                // Normalize: remove surrounding quotes and expand env vars
                var candidate = typed.Trim('"');
                candidate = Environment.ExpandEnvironmentVariables(candidate);
                // Validate directory existence and required files
                if (!System.IO.Directory.Exists(candidate) || !IsValidSevenZipFolder(candidate))
                {
                    System.Windows.MessageBox.Show(this,
                        "The specified 7-Zip folder is invalid. It must contain 7z.exe (or 7zG.exe) and 7z.dll.",
                        "Invalid 7-Zip path",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // keep the window open
                }
                _selectedSevenZipPath = candidate;
            }

            // Validate selected 7-Zip path if custom, else accept default (extra safety)
            if (_selectedSevenZipPath != null && !IsValidSevenZipFolder(_selectedSevenZipPath))
            {
                System.Windows.MessageBox.Show(this,
                    "The selected 7-Zip folder does not contain 7z.exe (or 7zG.exe) and 7z.dll. Please select a valid folder.",
                    "Invalid 7-Zip path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // keep the window open and force user to pick again
            }

            _settings.SevenZipPath = _selectedSevenZipPath; // null => default
            // Reflect final, effective value immediately in the textbox
            if (sevenZipPathBox != null)
            {
                sevenZipPathBox.Text = GetDisplaySevenZipPath();
            }
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private string GetDisplaySevenZipPath()
        {
            // Display empty string when using default, otherwise show the custom path
            return _selectedSevenZipPath ?? string.Empty;
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
                    Description = "Select 7-Zip folder",
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

        // (reset handler removed; toggle now controls behavior on restart)
    }
}
