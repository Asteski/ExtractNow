using System;
using System.Windows;
using System.IO;
using ExtractNow.Services;

namespace ExtractNow
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var settings = new SettingsService();

            // Removed proactive association metadata registration to avoid app appearing in Open With after cleanup.

            bool hasFileArg = e.Args.Length > 0 && File.Exists(e.Args[0]);
            bool hideOnAssoc = hasFileArg && !settings.ShowWindowOnAssociationLaunch;

            // If a size threshold is set and the archive exceeds it, force-show the window
            if (hasFileArg && settings.ShowWindowThresholdMB > 0)
            {
                try
                {
                    var fi = new FileInfo(e.Args[0]);
                    long thresholdBytes = (long)settings.ShowWindowThresholdMB * 1024L * 1024L;
                    if (fi.Exists && fi.Length >= thresholdBytes)
                    {
                        hideOnAssoc = false;
                    }
                }
                catch { }
            }

            var main = new MainWindow();
            MainWindow = main;

            if (!hideOnAssoc)
            {
                main.Show();
            }

            if (hasFileArg)
            {
                var task = main.StartExtraction(e.Args[0]);
                if (hideOnAssoc)
                {
                    task.ContinueWith(_ => Dispatcher.Invoke(() =>
                    {
                        try { main.Close(); } catch { }
                    }));
                }
            }
            else if (hideOnAssoc)
            {
                // No file arg; nothing to do, show the window anyway
                main.Show();
            }
        }
    }
}
