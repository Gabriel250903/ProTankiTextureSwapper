using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Windows
{
    public enum GoldBoxTargetChoice
    {
        NormalOnly,
        ThematicOnly,
        Both,
    }

    public partial class GoldBoxTargetDialog : FluentWindow
    {
        public GoldBoxTargetChoice SelectedChoice { get; private set; } = GoldBoxTargetChoice.Both;

        public GoldBoxTargetDialog()
        {
            InitializeComponent();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedChoice =
                NormalRadioButton.IsChecked == true ? GoldBoxTargetChoice.NormalOnly
                : PumpkinRadioButton.IsChecked == true ? GoldBoxTargetChoice.ThematicOnly
                : GoldBoxTargetChoice.Both;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ApplyButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
