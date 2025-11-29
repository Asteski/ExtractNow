using System;
using System.Windows;
using System.IO;
using ExtractNow.Services;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;

namespace ExtractNow
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Global exception handling to catch crashes in Release builds
            DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show($"An unhandled exception occurred: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Listen to notification activation
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                // Obtain the arguments from the notification
                ToastArguments args = ToastArguments.Parse(toastArgs.Argument);
                if (args.TryGetValue("action", out string action) && action == "openFolder")
                {
                    if (args.TryGetValue("folderPath", out string folderPath) && Directory.Exists(folderPath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"\"{folderPath}\"",
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                }
            };

            base.OnStartup(e);
            var settings = new SettingsService();

            // Removed proactive association metadata registration to avoid app appearing in Open With after cleanup.

            bool hasFileArg = e.Args.Length > 0 && File.Exists(e.Args[0]);
            // hideOnAssoc should be TRUE only if: (1) we have a file AND (2) user wants to hide on association launch
            // In other words: hideOnAssoc = false when ShowWindowOnAssociationLaunch = true
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

            // Only show the window if user wants to see it, or if no file is being extracted
            if (!hideOnAssoc)
            {
                main.Show();
            }

            if (hasFileArg)
            {
                var task = main.StartExtraction(e.Args[0]);
                if (hideOnAssoc)
                {
                    // In silent mode: don't show window, just close after extraction
                    task.ContinueWith(_ => Dispatcher.Invoke(() =>
                    {
                        try { main.Close(); } catch { }
                    }));
                }
            }
            else
            {
                // No file argument: always show the window
                main.Show();
            }
        }
    }
}
