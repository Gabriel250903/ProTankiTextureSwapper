using System.Windows;
using System.Windows.Media;

namespace TextureSwapper.Views.Controls
{
    public sealed class HalloweenVisualHost : FrameworkElement
    {
        private readonly VisualCollection _visuals;

        public HalloweenVisualHost()
        {
            _visuals = new VisualCollection(this) { Visual };
            IsHitTestVisible = false;
        }

        public DrawingVisual Visual { get; } = new();

        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index)
        {
            return index != 0
                ? throw new ArgumentOutOfRangeException(nameof(index))
                : _visuals[index];
        }
    }
}
