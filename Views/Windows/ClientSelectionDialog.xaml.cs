using System.Windows;
using System.Windows.Input;
using TextureSwapper.Core.Models;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Windows
{
    public partial class ClientSelectionDialog : FluentWindow
    {
        public ClientType SelectedClient { get; private set; }
        public bool RememberChoice { get; private set; } = true;

        public ClientSelectionDialog(
            ClientType initialSelection,
            bool isFlashDetected,
            bool isHtml5Detected
        )
        {
            InitializeComponent();

            FlashDetectedBadge.Visibility = isFlashDetected
                ? Visibility.Visible
                : Visibility.Collapsed;
            Html5DetectedBadge.Visibility = isHtml5Detected
                ? Visibility.Visible
                : Visibility.Collapsed;

            SelectedClient = initialSelection;
            if (initialSelection == ClientType.Html5)
            {
                Html5Radio.IsChecked = true;
                FlashRadio.IsChecked = false;
            }
            else
            {
                FlashRadio.IsChecked = true;
                Html5Radio.IsChecked = false;
            }
        }

        private void Html5Card_Click(object sender, RoutedEventArgs e)
        {
            Html5Radio.IsChecked = true;
            FlashRadio.IsChecked = false;
        }

        private void FlashCard_Click(object sender, RoutedEventArgs e)
        {
            FlashRadio.IsChecked = true;
            Html5Radio.IsChecked = false;
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedClient = Html5Radio.IsChecked == true ? ClientType.Html5 : ClientType.Flash;
            RememberChoice = RememberChoiceCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter)
            {
                ContinueButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
