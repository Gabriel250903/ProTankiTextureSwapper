using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TextureSwapper.ViewModels.Tabs;

namespace TextureSwapper.Views.Controls
{
    public partial class ChatTabContent : UserControl
    {
        private INotifyCollectionChanged? _observedCollection;
        private readonly DispatcherTimer _snowTimer;
        private readonly List<(
            Ellipse Flake,
            double SpeedY,
            double DriftPhase,
            double BaseX
        )> _flakes = [];
        private readonly Random _rng = new();
        private double _tick;

        public ChatTabContent()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) =>
            {
                ScrollToBottom();
                SyncSnowTimer();
            };
            Unloaded += (_, _) => _snowTimer?.Stop();

            _snowTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(33),
            };
            _snowTimer.Tick += OnSnowTick;

            SnowfallCanvas.IsVisibleChanged += (_, _) => SyncSnowTimer();
        }

        private void SyncSnowTimer()
        {
            if (SnowfallCanvas.IsVisible && IsLoaded)
            {
                EnsureFlakesInitialized();
                _snowTimer.Start();
            }
            else
            {
                _snowTimer.Stop();
            }
        }

        private void EnsureFlakesInitialized()
        {
            if (_flakes.Count > 0)
            {
                return;
            }

            double w = Math.Max(SnowfallCanvas.ActualWidth, 700);
            double h = Math.Max(SnowfallCanvas.ActualHeight, 400);

            for (int i = 0; i < 35; i++)
            {
                double size = 2.0 + (_rng.NextDouble() * 3.5);
                Ellipse flake = new()
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(
                        Color.FromArgb((byte)_rng.Next(90, 200), 235, 248, 255)
                    ),
                };
                double x = _rng.NextDouble() * w;
                double y = _rng.NextDouble() * h;
                Canvas.SetLeft(flake, x);
                Canvas.SetTop(flake, y);
                _ = SnowfallCanvas.Children.Add(flake);
                _flakes.Add(
                    (flake, 0.7 + (_rng.NextDouble() * 1.4), _rng.NextDouble() * Math.PI * 2, x)
                );
            }
        }

        private void OnSnowTick(object? sender, EventArgs e)
        {
            double w = SnowfallCanvas.ActualWidth;
            double h = SnowfallCanvas.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            _tick += 0.04;
            for (int i = 0; i < _flakes.Count; i++)
            {
                (Ellipse flake, double speedY, double phase, double baseX) = _flakes[i];
                double y = Canvas.GetTop(flake) + speedY;
                if (y > h + 6)
                {
                    y = -6;
                    baseX = _rng.NextDouble() * w;
                    _flakes[i] = (flake, speedY, phase, baseX);
                }

                double x = baseX + (Math.Sin(_tick + phase) * 14.0);
                Canvas.SetTop(flake, y);
                Canvas.SetLeft(flake, x);
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_observedCollection != null)
            {
                _observedCollection.CollectionChanged -= OnMessagesCollectionChanged;
            }

            if (e.NewValue is ChatTabViewModel vm)
            {
                _observedCollection = vm.Messages;
                _observedCollection.CollectionChanged += OnMessagesCollectionChanged;
                ScrollToBottom();
            }
        }

        private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                () =>
                {
                    if (MessagesListBox.Items.Count == 0)
                    {
                        return;
                    }

                    ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(MessagesListBox);
                    if (scrollViewer != null)
                    {
                        scrollViewer.ScrollToBottom();
                    }
                    else
                    {
                        MessagesListBox.ScrollIntoView(
                            MessagesListBox.Items[MessagesListBox.Items.Count - 1]
                        );
                    }
                }
            );
        }

        private static T? FindVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                {
                    return typed;
                }

                T? nested = FindVisualChild<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }
            return null;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (
                e.Key == Key.Enter
                && DataContext is ChatTabViewModel vm
                && vm.SendCommand.CanExecute(null)
            )
            {
                vm.SendCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
