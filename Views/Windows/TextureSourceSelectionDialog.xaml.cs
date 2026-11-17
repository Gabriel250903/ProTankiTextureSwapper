using System.Windows;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Windows
{
    public partial class TextureSourceSelectionDialog : FluentWindow
    {
        public string SelectedMirror { get; private set; } = "Cloudflare";

        public TextureSourceSelectionDialog(string initialPreference = "Cloudflare")
        {
            InitializeComponent();
            if (string.Equals(initialPreference, "GitHub", StringComparison.OrdinalIgnoreCase))
            {
                GitHubRadio.IsChecked = true;
                CloudflareRadio.IsChecked = false;
            }
            else
            {
                CloudflareRadio.IsChecked = true;
                GitHubRadio.IsChecked = false;
            }
        }

        private void CloudflareCard_Click(object sender, RoutedEventArgs e)
        {
            CloudflareRadio.IsChecked = true;
            GitHubRadio.IsChecked = false;
        }

        private void GitHubCard_Click(object sender, RoutedEventArgs e)
        {
            GitHubRadio.IsChecked = true;
            CloudflareRadio.IsChecked = false;
        }

        private void StartDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMirror = GitHubRadio.IsChecked == true ? "GitHub" : "Cloudflare";
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
