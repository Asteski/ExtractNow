using System;
using System.IO;
using System.Text.Json;

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
        }

    private Settings _settings;

        public SettingsService()
        {
            // Portable location: alongside the executable (fully portable)
            var baseDir = AppContext.BaseDirectory;
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
    }
}
