using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace TextureSwapper.Views.Windows
{
    public partial class InputDialog : FluentWindow
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

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ConfirmInputAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error confirming input dialog.");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                try
                {
                    await ConfirmInputAsync();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error confirming input dialog on Enter.");
                }
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private async Task ConfirmInputAsync()
        {
            string text = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox messageBox = new()
                {
                    Title = "Validation Error",
                    Content = "Please enter a valid name.",
                    CloseButtonText = "OK",
                };
                _ = await messageBox.ShowDialogAsync();
                return;
            }

            InputText = text;
            DialogResult = true;
            Close();
        }
    }
}
