using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TextureSwapper.Views
{
    public partial class ImagePreviewWindow : Wpf.Ui.Controls.FluentWindow
    {
        private Point _panStart;
        private Point _scrollStartOffset;
        private bool _isPanning;

        public ImagePreviewWindow(ImageSource imageSource)
        {
            InitializeComponent();
            PreviewImage.Source = imageSource;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;
            Zoom(scaleFactor);
            e.Handled = true;
        }

        private void Zoom(double scaleFactor)
        {
            double newScaleX = ImageScale.ScaleX * scaleFactor;
            double newScaleY = ImageScale.ScaleY * scaleFactor;

            if (newScaleX is >= 0.5 and <= 10.0)
            {
                ImageScale.ScaleX = newScaleX;
                ImageScale.ScaleY = newScaleY;
            }
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            Zoom(1.2);
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            Zoom(0.8);
        }

        private void OnResetZoomClick(object sender, RoutedEventArgs e)
        {
            ImageScale.ScaleX = 1.0;
            ImageScale.ScaleY = 1.0;
            PreviewScrollViewer.ScrollToHome();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _panStart = e.GetPosition(PreviewScrollViewer);
            _scrollStartOffset = new Point(PreviewScrollViewer.HorizontalOffset, PreviewScrollViewer.VerticalOffset);
            _isPanning = true;
            _ = PreviewScrollViewer.CaptureMouse();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                PreviewScrollViewer.ReleaseMouseCapture();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && PreviewScrollViewer.IsMouseCaptured)
            {
                Point currentPos = e.GetPosition(PreviewScrollViewer);
                double deltaX = currentPos.X - _panStart.X;
                double deltaY = currentPos.Y - _panStart.Y;

                PreviewScrollViewer.ScrollToHorizontalOffset(_scrollStartOffset.X - deltaX);
                PreviewScrollViewer.ScrollToVerticalOffset(_scrollStartOffset.Y - deltaY);
            }
        }
    }
}
