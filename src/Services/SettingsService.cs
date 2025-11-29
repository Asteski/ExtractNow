using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExtractNow.Services
{
    public sealed class SettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _portablePath;

        public class Settings
        {
            public bool ShowWindowOnAssociationLaunch { get; set; } = false; // default: don't show window
            public int ShowWindowThresholdMB { get; set; } = 0; // 0 means disabled
            public bool ShowTrayIconDuringExtraction { get; set; } = false; // default: don't show tray icon unless enabled
            public string? SevenZipPath { get; set; } = null; // null/empty -> use app default folder
            public bool OpenOutputFolderOnComplete { get; set; } = false; // default: off
            public bool CloseAppAfterExtraction { get; set; } = false; // default: off
            public bool ReuseExplorerWindows { get; set; } = false; // default: off (open in new window)
            public bool EnableSizeThreshold { get; set; } = false; // default: off
            public int MaxArchiveSizeMB { get; set; } = 1000; // default: 1000 MB
            public string OversizedArchiveAction { get; set; } = "Explorer"; // "Explorer" or "7-Zip"
            // Window persistence (0 means unset / use defaults)
            // These properties are ALWAYS hidden from JSON but can still be read if manually added
            [JsonIgnore]
            public int WindowWidth { get; set; } = 0;
            [JsonIgnore]
            public int WindowHeight { get; set; } = 0;
            [JsonIgnore]
            public bool WindowMaximized { get; set; } = false;
            // When true, ignore persisted geometry and restore default window size on next launch
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public bool RestoreDefaultWindowSizeOnRestart { get; set; } = false;
            public bool ShowNotificationOnComplete { get; set; } = true; // default: on
            public bool AlwaysOnTop { get; set; } = true; // default: on
        }

    private Settings _settings;

        public SettingsService()
        {
            // Portable location: alongside the executable (fully portable)
            // Use Environment.ProcessPath to ensure we get the exe location even in single-file publish
            var baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            _portablePath = Path.Combine(baseDir, SettingsFileName);
            _settings = Load();
        }

        public bool ShowWindowOnAssociationLaunch
        {
            get => _settings.ShowWindowOnAssociationLaunch;
            set { _settings.ShowWindowOnAssociationLaunch = value; Save(); }
        }

        public int ShowWindowThresholdMB
        {
            get => _settings.ShowWindowThresholdMB;
            set { _settings.ShowWindowThresholdMB = Math.Max(0, value); Save(); }
        }

        public bool ShowTrayIconDuringExtraction
        {
            get => _settings.ShowTrayIconDuringExtraction;
            set { _settings.ShowTrayIconDuringExtraction = value; Save(); }
        }

        public string? SevenZipPath
        {
            get => _settings.SevenZipPath;
            set { _settings.SevenZipPath = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
        }

        public bool OpenOutputFolderOnComplete
        {
            get => _settings.OpenOutputFolderOnComplete;
            set { _settings.OpenOutputFolderOnComplete = value; Save(); }
        }

        public bool CloseAppAfterExtraction
        {
            get => _settings.CloseAppAfterExtraction;
            set { _settings.CloseAppAfterExtraction = value; Save(); }
        }

        public bool ReuseExplorerWindows
        {
            get => _settings.ReuseExplorerWindows;
            set { _settings.ReuseExplorerWindows = value; Save(); }
        }

        public bool EnableSizeThreshold
        {
            get => _settings.EnableSizeThreshold;
            set { _settings.EnableSizeThreshold = value; Save(); }
        }

        public int MaxArchiveSizeMB
        {
            get => _settings.MaxArchiveSizeMB;
            set { _settings.MaxArchiveSizeMB = Math.Max(1, value); Save(); }
        }

        public string OversizedArchiveAction
        {
            get => _settings.OversizedArchiveAction;
            set { _settings.OversizedArchiveAction = value; Save(); }
        }

        public int WindowWidth
        {
            get => _settings.WindowWidth;
            set { _settings.WindowWidth = Math.Max(400, value); Save(); }
        }

        public int WindowHeight
        {
            get => _settings.WindowHeight;
            set { _settings.WindowHeight = Math.Max(300, value); Save(); }
        }

        public bool WindowMaximized
        {
            get => _settings.WindowMaximized;
            set { _settings.WindowMaximized = value; Save(); }
        }

        public bool RestoreDefaultWindowSizeOnRestart
        {
            get => _settings.RestoreDefaultWindowSizeOnRestart;
            set { _settings.RestoreDefaultWindowSizeOnRestart = value; Save(); }
        }

        public bool ShowNotificationOnComplete
        {
            get => _settings.ShowNotificationOnComplete;
            set { _settings.ShowNotificationOnComplete = value; Save(); }
        }

        public bool AlwaysOnTop
        {
            get => _settings.AlwaysOnTop;
            set { _settings.AlwaysOnTop = value; Save(); }
        }

        private Settings Load()
        {
            try
            {
                // Load only from portable file next to exe
                if (File.Exists(_portablePath))
                {
                    var json = File.ReadAllText(_portablePath);
                    var s = JsonSerializer.Deserialize<Settings>(json);
                    if (s != null) return s;
                }
            }
            catch { }
            return new Settings();
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                // Always write next to the executable (fully portable). Swallow IO errors silently.
                File.WriteAllText(_portablePath, json);
            }
            catch { }
        }

        public void ResetWindowGeometry()
        {
            try
            {
                _settings.WindowWidth = 0;
                _settings.WindowHeight = 0;
                _settings.WindowMaximized = false;
                Save();
            }
            catch { }
        }
    }
}
