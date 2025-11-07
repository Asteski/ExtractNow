using System;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows;

namespace ExtractNow.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var product = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "ExtractNow";
                var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                              ?? asm.GetName().Version?.ToString(3)
                              ?? "1.0.0";
                var company = "Asteski"; // override owner per request

                AppTitle.Text = product;
                AppVersion.Text = $"Version: {version}";
                AppOwner.Text = $"Owner: {company}";
            }
            catch { }
        }

        private void AboutWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            }
            catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                var url = e.Uri?.AbsoluteUri ?? "https://github.com/Asteski/ExtractNow";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
            e.Handled = true;
        }
    }
}
