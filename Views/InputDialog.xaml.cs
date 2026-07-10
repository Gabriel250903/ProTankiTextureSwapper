using System.Windows;
using System.Windows.Input;

namespace TextureSwapper.Views
{
    public partial class InputDialog : Wpf.Ui.Controls.FluentWindow
    {
        public string InputText { get; private set; } = string.Empty;

        public InputDialog(string defaultText = "")
        {
            InitializeComponent();
            InputTextBox.Text = defaultText;
            _ = InputTextBox.Focus();
            if (!string.IsNullOrEmpty(defaultText))
            {
                InputTextBox.SelectAll();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmInput();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmInput();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void ConfirmInput()
        {
            string text = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                _ = MessageBox.Show("Please enter a valid name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            InputText = text;
            DialogResult = true;
            Close();
        }
    }
}
