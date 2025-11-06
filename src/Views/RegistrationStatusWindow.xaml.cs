using System.Windows;
using ExtractNow.Services;

namespace ExtractNow.Views
{
    public partial class RegistrationStatusWindow : Window
    {
        public RegistrationStatusWindow(string extension, FileAssociations.RegistrationState state, string? error)
        {
            InitializeComponent();
            TitleText.Text = $"Register {extension}";
            switch (state)
            {
                case FileAssociations.RegistrationState.Success:
                    StatusText.Text = "Done";
                    StatusText.Foreground = System.Windows.Media.Brushes.DarkGreen;
                    break;
                case FileAssociations.RegistrationState.AlreadyAssociated:
                    StatusText.Text = "Already associated";
                    StatusText.Foreground = System.Windows.Media.Brushes.SteelBlue;
                    break;
                case FileAssociations.RegistrationState.Error:
                default:
                    StatusText.Text = "Error: " + (string.IsNullOrWhiteSpace(error) ? "Unknown error" : error);
                    StatusText.Foreground = System.Windows.Media.Brushes.DarkRed;
                    break;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
