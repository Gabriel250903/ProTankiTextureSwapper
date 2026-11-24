using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;

namespace TextureSwapper.Seasonal
{
    public partial class SeasonalAtmosphereCanvas : UserControl
    {
        private struct Snowflake
        {
            public double X;
            public double Y;
            public double SpeedY;
            public double DriftFreq;
            public double DriftAmp;
            public double Phase;
            public double Radius;
            public byte MaxAlpha;
        }

        private struct Ember
        {
            public double X;
            public double Y;
            public double SpeedY;
            public double DriftFreq;
            public double DriftAmp;
            public double Phase;
            public double Radius;
            public byte MaxAlpha;
            public bool IsPurple;
        }

        private struct Bat
        {
            public double X;
            public double Y;
            public double SpeedX;
            public double WaveFreq;
            public double WaveAmp;
            public double Scale;
            public double FlapSpeed;
            public double FlapPhase;
        }

        private const int MaxSnowflakes = 45;
        private const int MaxEmbers = 35;
        private const int MaxBats = 3;

        private readonly Snowflake[] _snowflakes = new Snowflake[MaxSnowflakes];
        private readonly Ember[] _embers = new Ember[MaxEmbers];
        private readonly Bat[] _bats = new Bat[MaxBats];

        private readonly Random _random = new();
        private readonly Stopwatch _stopwatch = new();
        private double _lastElapsedSeconds;

        private bool _isRendering;
        private bool _isLoaded;

        private static readonly Brush[] SnowflakeBrushes = PrecomputeSnowflakeBrushes();
        private static readonly Brush[] EmberOrangeBrushes = PrecomputeEmberBrushes(
            0xFF,
            0x75,
            0x18
        );
        private static readonly Brush[] EmberPurpleBrushes = PrecomputeEmberBrushes(
            0x9B,
            0x51,
            0xE0
        );
        private static readonly Brush BatBrush;

        static SeasonalAtmosphereCanvas()
        {
            BatBrush = new SolidColorBrush(Color.FromArgb(180, 20, 10, 30));
            BatBrush.Freeze();
        }

        private static Brush[] PrecomputeSnowflakeBrushes()
        {
            Brush[] brushes = new Brush[10];
            for (int i = 0; i < 10; i++)
            {
                byte alpha = (byte)(25 + (i * 23));
                SolidColorBrush b = new(Color.FromArgb(alpha, 255, 255, 255));
                b.Freeze();
                brushes[i] = b;
            }
            return brushes;
        }

        private static Brush[] PrecomputeEmberBrushes(byte r, byte g, byte b)
        {
            Brush[] brushes = new Brush[8];
            for (int i = 0; i < 8; i++)
            {
                byte alpha = (byte)(30 + (i * 28));
                SolidColorBrush brush = new(Color.FromArgb(alpha, r, g, b));
                brush.Freeze();
                brushes[i] = brush;
            }
            return brushes;
        }

        public SeasonalAtmosphereCanvas()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            InitializeParticles();
            EvaluateRenderingState();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            StopRendering();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            EvaluateRenderingState();
        }

        private void EvaluateRenderingState()
        {
            ParticleMode mode = SeasonalThemeConfig.Current.ParticleMode;
            if (
                _isLoaded
                && IsVisible
                && ActualWidth > 0
                && ActualHeight > 0
                && mode != ParticleMode.None
            )
            {
                StartRendering();
            }
            else
            {
                StopRendering();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (sizeInfo.NewSize.Width > 0 && sizeInfo.NewSize.Height > 0)
            {
                InitializeParticles();
                EvaluateRenderingState();
            }
        }

        private void StartRendering()
        {
            if (_isRendering)
            {
                return;
            }

            _isRendering = true;
            _stopwatch.Restart();
            _lastElapsedSeconds = 0;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRendering()
        {
            if (!_isRendering)
            {
                return;
            }

            _isRendering = false;
            CompositionTarget.Rendering -= OnRendering;
            _stopwatch.Stop();

            try
            {
                using DrawingContext dc = VisualHost.Visual.RenderOpen();
            }
            catch { }
        }

        private void InitializeParticles()
        {
            double w = Math.Max(ActualWidth, 800);
            double h = Math.Max(ActualHeight, 600);

            for (int i = 0; i < MaxSnowflakes; i++)
            {
                _snowflakes[i] = new Snowflake
                {
                    X = _random.NextDouble() * w,
                    Y = _random.NextDouble() * h,
                    SpeedY = 20.0 + (_random.NextDouble() * 35.0),
                    DriftFreq = 0.5 + (_random.NextDouble() * 1.5),
                    DriftAmp = 15.0 + (_random.NextDouble() * 25.0),
                    Phase = _random.NextDouble() * Math.PI * 2,
                    Radius = 1.2 + (_random.NextDouble() * 2.5),
                    MaxAlpha = (byte)_random.Next(100, 220),
                };
            }

            for (int i = 0; i < MaxEmbers; i++)
            {
                _embers[i] = new Ember
                {
                    X = _random.NextDouble() * w,
                    Y = _random.NextDouble() * h,
                    SpeedY = 15.0 + (_random.NextDouble() * 30.0),
                    DriftFreq = 0.8 + (_random.NextDouble() * 1.5),
                    DriftAmp = 8.0 + (_random.NextDouble() * 16.0),
                    Phase = _random.NextDouble() * Math.PI * 2,
                    Radius = 1.0 + (_random.NextDouble() * 2.2),
                    MaxAlpha = (byte)_random.Next(120, 240),
                    IsPurple = _random.NextDouble() < 0.3,
                };
            }

            for (int i = 0; i < MaxBats; i++)
            {
                _bats[i] = new Bat
                {
                    X = -50 - (_random.NextDouble() * 200),
                    Y = _random.NextDouble() * (h * 0.6),
                    SpeedX = 40.0 + (_random.NextDouble() * 60.0),
                    WaveFreq = 1.5 + (_random.NextDouble() * 2.0),
                    WaveAmp = 15.0 + (_random.NextDouble() * 25.0),
                    Scale = 0.4 + (_random.NextDouble() * 0.4),
                    FlapSpeed = 6.0 + (_random.NextDouble() * 4.0),
                    FlapPhase = _random.NextDouble() * Math.PI * 2,
                };
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isRendering)
            {
                return;
            }

            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            double dt = elapsedSeconds - _lastElapsedSeconds;
            _lastElapsedSeconds = elapsedSeconds;

            if (dt > 0.1)
            {
                dt = 0.1;
            }

            if (dt <= 0)
            {
                return;
            }

            ParticleMode mode = SeasonalThemeConfig.Current.ParticleMode;
            if (mode == ParticleMode.None)
            {
                StopRendering();
                return;
            }

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            try
            {
                using DrawingContext dc = VisualHost.Visual.RenderOpen();

                if (mode == ParticleMode.Snowfall)
                {
                    RenderSnowfall(dc, elapsedSeconds, dt, w, h);
                }
                else if (mode == ParticleMode.HalloweenSpooky)
                {
                    RenderHalloween(dc, elapsedSeconds, dt, w, h);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "SeasonalAtmosphereCanvas rendering frame exception.");
            }
        }

        private void RenderSnowfall(DrawingContext dc, double time, double dt, double w, double h)
        {
            for (int i = 0; i < MaxSnowflakes; i++)
            {
                ref Snowflake s = ref _snowflakes[i];
                s.Y += s.SpeedY * dt;
                s.X += Math.Sin((time * s.DriftFreq) + s.Phase) * (s.DriftAmp * dt);

                if (s.Y > h + 10)
                {
                    s.Y = -10;
                    s.X = _random.NextDouble() * w;
                }
                if (s.X < -20)
                {
                    s.X = w + 20;
                }
                else if (s.X > w + 20)
                {
                    s.X = -20;
                }

                int brushIdx = Math.Clamp(
                    (int)(s.MaxAlpha / 255.0 * (SnowflakeBrushes.Length - 1)),
                    0,
                    SnowflakeBrushes.Length - 1
                );
                dc.DrawEllipse(
                    SnowflakeBrushes[brushIdx],
                    null,
                    new Point(s.X, s.Y),
                    s.Radius,
                    s.Radius
                );
            }
        }

        private void RenderHalloween(DrawingContext dc, double time, double dt, double w, double h)
        {
            for (int i = 0; i < MaxEmbers; i++)
            {
                ref Ember em = ref _embers[i];
                em.Y -= em.SpeedY * dt;
                em.X += Math.Sin((time * em.DriftFreq) + em.Phase) * (em.DriftAmp * dt);

                if (em.Y < -10)
                {
                    em.Y = h + 10;
                    em.X = _random.NextDouble() * w;
                }
                if (em.X < -10)
                {
                    em.X = w + 10;
                }
                else if (em.X > w + 10)
                {
                    em.X = -10;
                }

                Brush[] set = em.IsPurple ? EmberPurpleBrushes : EmberOrangeBrushes;
                int brushIdx = Math.Clamp(
                    (int)(em.MaxAlpha / 255.0 * (set.Length - 1)),
                    0,
                    set.Length - 1
                );
                dc.DrawEllipse(set[brushIdx], null, new Point(em.X, em.Y), em.Radius, em.Radius);
            }

            for (int i = 0; i < MaxBats; i++)
            {
                ref Bat b = ref _bats[i];
                b.X += b.SpeedX * dt;
                b.Y += Math.Sin(time * b.WaveFreq) * (b.WaveAmp * dt);

                if (b.X > w + 80)
                {
                    b.X = -80;
                    b.Y = _random.NextDouble() * (h * 0.6);
                }

                double wingY = Math.Sin((time * b.FlapSpeed) + b.FlapPhase) * 6.0 * b.Scale;
                StreamGeometry geom = new();
                using (StreamGeometryContext ctx = geom.Open())
                {
                    Point center = new(b.X, b.Y);
                    ctx.BeginFigure(
                        new Point(center.X - (16 * b.Scale), center.Y + wingY),
                        isFilled: true,
                        isClosed: true
                    );
                    ctx.LineTo(
                        new Point(center.X - (4 * b.Scale), center.Y),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                    ctx.LineTo(
                        new Point(center.X, center.Y + (4 * b.Scale)),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                    ctx.LineTo(
                        new Point(center.X + (4 * b.Scale), center.Y),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                    ctx.LineTo(
                        new Point(center.X + (16 * b.Scale), center.Y + wingY),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                    ctx.LineTo(
                        new Point(center.X + (6 * b.Scale), center.Y + (2 * b.Scale)),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                    ctx.LineTo(
                        new Point(center.X, center.Y + (2 * b.Scale)),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                    ctx.LineTo(
                        new Point(center.X - (6 * b.Scale), center.Y + (2 * b.Scale)),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                }
                geom.Freeze();
                dc.DrawGeometry(BatBrush, null, geom);
            }
        }
    }
}
