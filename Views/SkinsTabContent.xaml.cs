using System.Windows;
using System.Windows.Controls;
using TextureSwapper.ViewModels;

namespace TextureSwapper.Views
{
    public partial class SkinsTabContent : UserControl
    {
        public SkinsTabContent()
        {
            InitializeComponent();
        }

        private void OnListSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged && DataContext is MainViewModel vm)
            {
                double width = e.NewSize.Width;
                int cols = (int)((width - 25) / 230);
                if (cols < 1)
                {
                    cols = 1;
                }
                if (cols > 4)
                {
                    cols = 4;
                }
                vm.UpdateColumns(cols);
            }
        }
    }
}
