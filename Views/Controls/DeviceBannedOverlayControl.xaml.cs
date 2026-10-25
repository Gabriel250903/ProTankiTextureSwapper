using System.Windows;
using System.Windows.Controls;
using Serilog;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Controls
{
    public partial class DeviceBannedOverlayControl : UserControl
    {
        private bool _isHwidRevealed;

        public DeviceBannedOverlayControl()
        {
            InitializeComponent();
        }

        private void OnToggleHwidClicked(object sender, RoutedEventArgs e)
        {
            _isHwidRevealed = !_isHwidRevealed;

            if (_isHwidRevealed)
            {
                RevealedHwidBorder.Visibility = Visibility.Visible;
                HwidMaskedText.Visibility = Visibility.Collapsed;
                ToggleHwidButton.SetResourceReference(
                    ContentControl.ContentProperty,
                    "Loc_Ban_Hide"
                );
                ToggleHwidButton.Icon = new SymbolIcon(SymbolRegular.EyeOff24);
            }
            else
            {
                RevealedHwidBorder.Visibility = Visibility.Collapsed;
                HwidMaskedText.Visibility = Visibility.Visible;
                ToggleHwidButton.SetResourceReference(
                    ContentControl.ContentProperty,
                    "Loc_Ban_Reveal"
                );
                ToggleHwidButton.Icon = new SymbolIcon(SymbolRegular.Eye24);
            }
        }

        private void OnCopyHwidClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (
                    DataContext is ViewModels.MainViewModel vm
                    && !string.IsNullOrWhiteSpace(vm.DeviceHwidDisplay)
                )
                {
                    Clipboard.SetText(vm.DeviceHwidDisplay);
                    Log.Information("Hardware ID copied to clipboard from banned overlay.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to copy Hardware ID to clipboard from overlay.");
            }
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            Log.Information("User clicked exit on banned overlay. Shutting down.");
            Application.Current?.MainWindow?.Close();
        }
    }
}
