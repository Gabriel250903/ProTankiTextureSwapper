using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;

namespace TextureSwapper.Views.Controls
{
    public partial class HalloweenAtmosphereCanvas : UserControl
    {
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

        private struct Ghost
        {
            public double X;
            public double Y;
            public double SpeedX;
            public double DriftFreq;
            public double DriftAmp;
            public double Phase;
            public double Scale;
            public double Alpha;
            public double TargetAlpha;
        }

        private struct SpookyEyes
        {
            public double X;
            public double Y;
            public double StateTimer;
            public bool IsVisible;
            public double BlinkProgress;
            public double Alpha;
        }

        private const int MaxEmbers = 40;
        private const int MaxBats = 4;
        private const int MaxGhosts = 3;
        private const int MaxEyes = 3;

        private readonly Ember[] _embers = new Ember[MaxEmbers];
        private readonly Bat[] _bats = new Bat[MaxBats];
        private readonly Ghost[] _ghosts = new Ghost[MaxGhosts];
        private readonly SpookyEyes[] _eyes = new SpookyEyes[MaxEyes];
        private readonly Random _rnd = new();

        private readonly Stopwatch _stopwatch = new();
        private double _lastSeconds;
        private double _lightningAlpha;
        private double _nextLightningTime = 8.0;
        private bool _isRunning;

        private static readonly Brush OrangeEmberBrush;
        private static readonly Brush PurpleEmberBrush;
        private static readonly Brush BatFillBrush;
        private static readonly Pen BatOutlinePen;
        private static readonly Brush FogBrush;
        private static readonly Brush GraveyardGroundBrush;
        private static readonly Pen SilhouettePen;
        private static readonly Brush MoonCoreBrush;
        private static readonly Brush MoonGlowBrush;
        private static readonly Brush GhostBodyBrush;
        private static readonly Brush GhostEyeBrush;
        private static readonly Brush SpookyEyeGlowBrush;
        private static readonly Brush SpookyEyePupilBrush;
        private static readonly Pen WebPen;
        private static readonly Brush SpiderBodyBrush;
        private static readonly Pen SpiderLegPen;
        private static readonly Brush ApparitionBrush;

        private static readonly Brush[] PrecachedOrangeEmbers = new Brush[16];
        private static readonly Brush[] PrecachedPurpleEmbers = new Brush[16];

        static HalloweenAtmosphereCanvas()
        {
            OrangeEmberBrush = new SolidColorBrush(Color.FromArgb(200, 0xFF, 0x6B, 0x00));
            OrangeEmberBrush.Freeze();

            PurpleEmberBrush = new SolidColorBrush(Color.FromArgb(190, 0x9B, 0x51, 0xE0));
            PurpleEmberBrush.Freeze();

            for (int i = 0; i < 16; i++)
            {
                byte a = (byte)((i * 16) + 15);
                SolidColorBrush ob = new(Color.FromArgb(a, 0xFF, 0x75, 0x18));
                ob.Freeze();
                PrecachedOrangeEmbers[i] = ob;

                SolidColorBrush pb = new(Color.FromArgb(a, 0x9B, 0x51, 0xE0));
                pb.Freeze();
                PrecachedPurpleEmbers[i] = pb;
            }

            BatFillBrush = new SolidColorBrush(Color.FromArgb(190, 0x12, 0x06, 0x1F));
            BatFillBrush.Freeze();

            BatOutlinePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 0xFF, 0x6B, 0x00)), 0.6);
            BatOutlinePen.Freeze();

            FogBrush = new LinearGradientBrush(
                Color.FromArgb(0, 0x09, 0x04, 0x0E),
                Color.FromArgb(170, 0x1A, 0x07, 0x2C),
                new Point(0.5, 0),
                new Point(0.5, 1)
            );
            FogBrush.Freeze();

            GraveyardGroundBrush = new SolidColorBrush(Color.FromArgb(235, 0x08, 0x03, 0x0F));
            GraveyardGroundBrush.Freeze();

            SilhouettePen = new Pen(
                new SolidColorBrush(Color.FromArgb(210, 0x07, 0x02, 0x0E)),
                1.5
            );
            SilhouettePen.Freeze();

            RadialGradientBrush moonRgb = new()
            {
                GradientOrigin = new Point(0.4, 0.4),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
            };
            moonRgb.GradientStops.Add(new GradientStop(Color.FromArgb(240, 0xFF, 0xEE, 0xAA), 0.0));
            moonRgb.GradientStops.Add(new GradientStop(Color.FromArgb(200, 0xFF, 0x88, 0x00), 0.7));
            moonRgb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0x44, 0x00), 1.0));
            moonRgb.Freeze();
            MoonCoreBrush = moonRgb;

            RadialGradientBrush moonGlow = new()
            {
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
            };
            moonGlow.GradientStops.Add(new GradientStop(Color.FromArgb(70, 0xFF, 0x77, 0x00), 0.0));
            moonGlow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x8A, 0x2B, 0xE2), 1.0));
            moonGlow.Freeze();
            MoonGlowBrush = moonGlow;

            GhostBodyBrush = new SolidColorBrush(Color.FromArgb(110, 0xD4, 0xBF, 0xF9));
            GhostBodyBrush.Freeze();

            GhostEyeBrush = new SolidColorBrush(Color.FromArgb(180, 0x09, 0x04, 0x12));
            GhostEyeBrush.Freeze();

            SpookyEyeGlowBrush = new SolidColorBrush(Color.FromArgb(220, 0xFF, 0x44, 0x00));
            SpookyEyeGlowBrush.Freeze();

            SpookyEyePupilBrush = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xEE, 0x33));
            SpookyEyePupilBrush.Freeze();

            WebPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0xBF, 0xA8, 0xE0)), 0.8);
            WebPen.Freeze();

            SpiderBodyBrush = new SolidColorBrush(Color.FromArgb(220, 0x1A, 0x09, 0x2A));
            SpiderBodyBrush.Freeze();

            SpiderLegPen = new Pen(
                new SolidColorBrush(Color.FromArgb(190, 0x0D, 0x04, 0x16)),
                0.75
            );
            SpiderLegPen.Freeze();

            ApparitionBrush = new SolidColorBrush(Color.FromArgb(85, 0xC4, 0xAD, 0xF5));
            ApparitionBrush.Freeze();
        }

        public HalloweenAtmosphereCanvas()
        {
            InitializeComponent();

            IsVisibleChanged += (_, _) => CheckEngineState();
            Loaded += (_, _) => CheckEngineState();
            Unloaded += (_, _) => StopEngine();
        }

        private void CheckEngineState()
        {
            if (Visibility == Visibility.Visible && IsLoaded)
            {
                StartEngine();
            }
            else
            {
                StopEngine();
            }
        }

        private void StartEngine()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _stopwatch.Restart();
            _lastSeconds = 0;

            InitEmbers();
            InitBats();
            InitGhosts();
            InitEyes();

            CompositionTarget.Rendering += OnRenderFrame;
        }

        private void StopEngine()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            CompositionTarget.Rendering -= OnRenderFrame;
            _stopwatch.Stop();

            try
            {
                if (VisualHost?.Visual != null)
                {
                    using DrawingContext dc = VisualHost.Visual.RenderOpen();
                }
            }
            catch { }
        }

        private void InitEmbers()
        {
            double w = Math.Max(ActualWidth, 1200);
            double h = Math.Max(ActualHeight, 800);

            for (int i = 0; i < MaxEmbers; i++)
            {
                ResetEmber(ref _embers[i], w, h, initial: true);
            }
        }

        private void ResetEmber(ref Ember e, double w, double h, bool initial)
        {
            e.X = _rnd.NextDouble() * w;
            e.Y = initial ? (_rnd.NextDouble() * h) : (h + _rnd.Next(10, 40));
            e.SpeedY = (_rnd.NextDouble() * 38.0) + 22.0;
            e.DriftFreq = (_rnd.NextDouble() * 1.8) + 0.6;
            e.DriftAmp = (_rnd.NextDouble() * 25.0) + 10.0;
            e.Phase = _rnd.NextDouble() * Math.PI * 2;
            e.Radius = (_rnd.NextDouble() * 2.8) + 1.2;
            e.MaxAlpha = (byte)_rnd.Next(130, 240);
            e.IsPurple = _rnd.Next(0, 4) == 0;
        }

        private void InitBats()
        {
            double w = Math.Max(ActualWidth, 1200);
            double h = Math.Max(ActualHeight, 800);

            for (int i = 0; i < MaxBats; i++)
            {
                ResetBat(ref _bats[i], w, h, initial: true);
            }
        }

        private void ResetBat(ref Bat b, double w, double h, bool initial)
        {
            b.X = initial ? (_rnd.NextDouble() * w) : (w + _rnd.Next(40, 200));
            b.Y = _rnd.Next(30, (int)Math.Max(50, h * 0.55));
            b.SpeedX = (_rnd.NextDouble() * 70.0) + 55.0;
            b.WaveFreq = (_rnd.NextDouble() * 1.5) + 0.8;
            b.WaveAmp = (_rnd.NextDouble() * 30.0) + 15.0;
            b.Scale = (_rnd.NextDouble() * 0.35) + 0.35;
            b.FlapSpeed = (_rnd.NextDouble() * 14.0) + 12.0;
            b.FlapPhase = _rnd.NextDouble() * Math.PI * 2;
        }

        private void InitGhosts()
        {
            double w = Math.Max(ActualWidth, 1200);
            double h = Math.Max(ActualHeight, 800);

            for (int i = 0; i < MaxGhosts; i++)
            {
                ResetGhost(ref _ghosts[i], w, h, initial: true);
            }
        }

        private void ResetGhost(ref Ghost g, double w, double h, bool initial)
        {
            g.X = initial ? (_rnd.NextDouble() * w) : (-100 - _rnd.Next(50, 200));
            g.Y = _rnd.Next(100, (int)Math.Max(150, h * 0.7));
            g.SpeedX = (_rnd.NextDouble() * 22.0) + 15.0;
            g.DriftFreq = (_rnd.NextDouble() * 0.8) + 0.4;
            g.DriftAmp = (_rnd.NextDouble() * 35.0) + 15.0;
            g.Phase = _rnd.NextDouble() * Math.PI * 2;
            g.Scale = (_rnd.NextDouble() * 0.35) + 0.65;
            g.Alpha = 0.0;
            g.TargetAlpha = (_rnd.NextDouble() * 0.55) + 0.35;
        }

        private void InitEyes()
        {
            for (int i = 0; i < MaxEyes; i++)
            {
                _eyes[i] = new SpookyEyes
                {
                    X = _rnd.Next(60, 400),
                    Y = _rnd.Next(80, 500),
                    StateTimer = (_rnd.NextDouble() * 4.0) + 1.0,
                    IsVisible = false,
                    Alpha = 0.0,
                    BlinkProgress = 0.0,
                };
            }
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (!_isRunning || VisualHost?.Visual == null)
            {
                return;
            }

            try
            {
                double totalSeconds = _stopwatch.Elapsed.TotalSeconds;
                double dt = totalSeconds - _lastSeconds;
                _lastSeconds = totalSeconds;

                if (dt is <= 0 or > 0.1)
                {
                    dt = 0.016;
                }

                double w = ActualWidth;
                double h = ActualHeight;

                if (w <= 10 || h <= 10)
                {
                    return;
                }

                if (totalSeconds >= _nextLightningTime)
                {
                    _lightningAlpha = 0.45;
                    _nextLightningTime = totalSeconds + _rnd.Next(9, 20);
                }
                if (_lightningAlpha > 0)
                {
                    _lightningAlpha = Math.Max(0, _lightningAlpha - (dt * 1.6));
                }

                using DrawingContext dc = VisualHost.Visual.RenderOpen();

                if (_lightningAlpha > 0.01)
                {
                    byte alpha = (byte)(_lightningAlpha * 255);
                    SolidColorBrush flashBrush = new(Color.FromArgb(alpha, 0x8A, 0x2B, 0xE2));
                    dc.DrawRectangle(flashBrush, null, new Rect(0, 0, w, h));

                    if (_lightningAlpha > 0.2)
                    {
                        DrawSpectralApparition(dc, w * 0.5, h * 0.45);
                    }
                }

                double moonX = Math.Max(150, w - 160);
                double moonY = 85;
                dc.DrawEllipse(MoonGlowBrush, null, new Point(moonX, moonY), 95, 95);
                dc.DrawEllipse(MoonCoreBrush, null, new Point(moonX, moonY), 42, 42);

                DrawGraveyardBackground(dc, w, h);

                double fogHeight = Math.Min(h * 0.38, 240);
                double fogWave = Math.Sin(totalSeconds * 0.8) * 14.0;
                dc.DrawRectangle(
                    FogBrush,
                    null,
                    new Rect(0, h - fogHeight + fogWave, w, fogHeight - fogWave)
                );

                DrawSpiderWeb(dc, w, totalSeconds);

                for (int i = 0; i < MaxGhosts; i++)
                {
                    ref Ghost ghost = ref _ghosts[i];
                    ghost.X += ghost.SpeedX * dt;

                    ghost.Alpha =
                        ghost.X < w * 0.8
                            ? Math.Min(ghost.TargetAlpha, ghost.Alpha + (dt * 0.4))
                            : Math.Max(0, ghost.Alpha - (dt * 0.5));

                    if (ghost.X > w + 120 || (ghost.X > w * 0.8 && ghost.Alpha <= 0.01))
                    {
                        ResetGhost(ref ghost, w, h, initial: false);
                        continue;
                    }

                    double waveY =
                        ghost.Y
                        + (
                            Math.Sin((totalSeconds * ghost.DriftFreq) + ghost.Phase)
                            * ghost.DriftAmp
                        );
                    DrawGhost(dc, ghost.X, waveY, ghost.Scale, ghost.Alpha);
                }

                UpdateAndDrawEyes(dc, dt, w, h);

                for (int i = 0; i < MaxEmbers; i++)
                {
                    ref Ember emb = ref _embers[i];
                    emb.Y -= emb.SpeedY * dt;

                    if (emb.Y < -15)
                    {
                        ResetEmber(ref emb, w, h, initial: false);
                        continue;
                    }

                    double driftOffset =
                        Math.Sin((totalSeconds * emb.DriftFreq) + emb.Phase) * emb.DriftAmp;
                    double renderX = emb.X + driftOffset;

                    double lifeRatio = Math.Clamp(emb.Y / h, 0.0, 1.0);
                    int bucket = Math.Clamp((int)(lifeRatio * 15.0), 0, 15);
                    Brush b = emb.IsPurple
                        ? PrecachedPurpleEmbers[bucket]
                        : PrecachedOrangeEmbers[bucket];

                    dc.DrawEllipse(b, null, new Point(renderX, emb.Y), emb.Radius, emb.Radius);
                }

                for (int i = 0; i < MaxBats; i++)
                {
                    ref Bat bat = ref _bats[i];
                    bat.X -= bat.SpeedX * dt;

                    if (bat.X < -120)
                    {
                        ResetBat(ref bat, w, h, initial: false);
                        continue;
                    }

                    double renderY = bat.Y + (Math.Sin(totalSeconds * bat.WaveFreq) * bat.WaveAmp);
                    double flap = Math.Sin((totalSeconds * bat.FlapSpeed) + bat.FlapPhase);

                    DrawBat(dc, bat.X, renderY, bat.Scale, flap);
                }
            }
            catch
            {
                Log.Warning(
                    "Exception occurred during HalloweenAtmosphereCanvas rendering. Rendering loop will continue, but some frames may be skipped."
                );
            }
        }

        private static void DrawGraveyardBackground(DrawingContext dc, double w, double h)
        {
            double groundY = h - 65;
            dc.DrawRectangle(GraveyardGroundBrush, null, new Rect(0, groundY, w, 70));

            DrawTombstone(dc, w * 0.12, groundY, 26, 38, isCross: false, tilt: -0.1);
            DrawTombstone(dc, w * 0.16, groundY, 18, 44, isCross: true, tilt: 0.12);
            DrawTombstone(dc, w * 0.38, groundY, 28, 42, isCross: false, tilt: 0.08);
            DrawTombstone(dc, w * 0.62, groundY, 22, 48, isCross: true, tilt: -0.06);
            DrawTombstone(dc, w * 0.84, groundY, 26, 40, isCross: false, tilt: 0.14);
            DrawTombstone(dc, w * 0.88, groundY, 20, 46, isCross: true, tilt: -0.15);
            DrawDeadTree(dc, 75, groundY, 1.1);
            DrawDeadTree(dc, w - 85, groundY, 0.95);
        }

        private static void DrawTombstone(
            DrawingContext dc,
            double x,
            double groundY,
            double width,
            double height,
            bool isCross,
            double tilt
        )
        {
            Transform? prev = null;
            if (Math.Abs(tilt) > 0.01)
            {
                dc.PushTransform(new RotateTransform(tilt * 180.0 / Math.PI, x, groundY));
                prev = Transform.Identity;
            }

            if (isCross)
            {
                double crossW = width * 0.35;
                double armH = width * 0.3;
                double armW = width * 1.1;

                dc.DrawRectangle(
                    GraveyardGroundBrush,
                    SilhouettePen,
                    new Rect(x - (crossW / 2), groundY - height, crossW, height)
                );
                dc.DrawRectangle(
                    GraveyardGroundBrush,
                    SilhouettePen,
                    new Rect(x - (armW / 2), groundY - height + (height * 0.28), armW, armH)
                );
            }
            else
            {
                dc.DrawRoundedRectangle(
                    GraveyardGroundBrush,
                    SilhouettePen,
                    new Rect(x - (width / 2), groundY - height, width, height),
                    width * 0.4,
                    width * 0.4
                );
            }

            if (prev != null)
            {
                dc.Pop();
            }
        }

        private static void DrawDeadTree(
            DrawingContext dc,
            double rootX,
            double rootY,
            double scale
        )
        {
            StreamGeometry geom = new();
            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(
                    new Point(rootX - (18 * scale), rootY),
                    isFilled: true,
                    isClosed: true
                );
                ctx.QuadraticBezierTo(
                    new Point(rootX - (10 * scale), rootY - (70 * scale)),
                    new Point(rootX - (35 * scale), rootY - (140 * scale)),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(rootX - (15 * scale), rootY - (110 * scale)),
                    new Point(rootX, rootY - (95 * scale)),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(rootX + (25 * scale), rootY - (145 * scale)),
                    new Point(rootX + (45 * scale), rootY - (175 * scale)),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(rootX + (25 * scale), rootY - (120 * scale)),
                    new Point(rootX + (12 * scale), rootY - (75 * scale)),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(rootX + (18 * scale), rootY - (30 * scale)),
                    new Point(rootX + (22 * scale), rootY),
                    isStroked: true,
                    isSmoothJoin: true
                );
            }
            geom.Freeze();

            dc.DrawGeometry(GraveyardGroundBrush, SilhouettePen, geom);
        }

        private static void DrawSpiderWeb(DrawingContext dc, double w, double totalSeconds)
        {
            double cornerX = w;
            double cornerY = 0;

            for (int r = 40; r <= 140; r += 32)
            {
                StreamGeometry arcGeom = new();
                using (StreamGeometryContext ctx = arcGeom.Open())
                {
                    ctx.BeginFigure(
                        new Point(cornerX - r, cornerY),
                        isFilled: false,
                        isClosed: false
                    );
                    ctx.QuadraticBezierTo(
                        new Point(cornerX - (r * 0.7), cornerY + (r * 0.7)),
                        new Point(cornerX, cornerY + r),
                        isStroked: true,
                        isSmoothJoin: true
                    );
                }
                arcGeom.Freeze();
                dc.DrawGeometry(null, WebPen, arcGeom);
            }

            dc.DrawLine(
                WebPen,
                new Point(cornerX, cornerY),
                new Point(cornerX - 150, cornerY + 30)
            );
            dc.DrawLine(
                WebPen,
                new Point(cornerX, cornerY),
                new Point(cornerX - 110, cornerY + 110)
            );
            dc.DrawLine(
                WebPen,
                new Point(cornerX, cornerY),
                new Point(cornerX - 30, cornerY + 150)
            );

            double threadOriginX = cornerX - 70;
            double threadLength = 75.0 + (Math.Sin(totalSeconds * 2.0) * 14.0);
            double spiderY = threadLength;

            dc.DrawLine(WebPen, new Point(threadOriginX, 0), new Point(threadOriginX, spiderY));

            dc.DrawEllipse(SpiderBodyBrush, null, new Point(threadOriginX, spiderY), 5.5, 4.5);
            dc.DrawEllipse(
                SpiderBodyBrush,
                null,
                new Point(threadOriginX, spiderY + 4.5),
                3.5,
                3.0
            );

            dc.DrawLine(
                SpiderLegPen,
                new Point(threadOriginX - 3, spiderY),
                new Point(threadOriginX - 9, spiderY - 3)
            );
            dc.DrawLine(
                SpiderLegPen,
                new Point(threadOriginX - 3, spiderY + 2),
                new Point(threadOriginX - 10, spiderY + 3)
            );
            dc.DrawLine(
                SpiderLegPen,
                new Point(threadOriginX + 3, spiderY),
                new Point(threadOriginX + 9, spiderY - 3)
            );
            dc.DrawLine(
                SpiderLegPen,
                new Point(threadOriginX + 3, spiderY + 2),
                new Point(threadOriginX + 10, spiderY + 3)
            );
        }

        private static void DrawGhost(
            DrawingContext dc,
            double x,
            double y,
            double scale,
            double alpha
        )
        {
            if (alpha <= 0.01)
            {
                return;
            }

            byte a = (byte)(alpha * 255);
            SolidColorBrush gBrush = new(Color.FromArgb((byte)(a * 0.45), 0xD5, 0xC4, 0xFA));
            SolidColorBrush eyeBrush = new(Color.FromArgb((byte)(a * 0.75), 0x09, 0x04, 0x14));

            StreamGeometry geom = new();
            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(new Point(x, y - (35 * scale)), isFilled: true, isClosed: true);
                ctx.QuadraticBezierTo(
                    new Point(x + (25 * scale), y - (35 * scale)),
                    new Point(x + (25 * scale), y),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x + (15 * scale), y + (35 * scale)),
                    new Point(x + (8 * scale), y + (25 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x, y + (38 * scale)),
                    new Point(x - (8 * scale), y + (25 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x - (15 * scale), y + (35 * scale)),
                    new Point(x - (25 * scale), y),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x - (25 * scale), y - (35 * scale)),
                    new Point(x, y - (35 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
            }
            geom.Freeze();

            dc.DrawGeometry(gBrush, null, geom);

            dc.DrawEllipse(
                eyeBrush,
                null,
                new Point(x - (7 * scale), y - (12 * scale)),
                3.2 * scale,
                4.2 * scale
            );
            dc.DrawEllipse(
                eyeBrush,
                null,
                new Point(x + (7 * scale), y - (12 * scale)),
                3.2 * scale,
                4.2 * scale
            );
            dc.DrawEllipse(eyeBrush, null, new Point(x, y - (2 * scale)), 2.8 * scale, 4.0 * scale);
        }

        private void UpdateAndDrawEyes(DrawingContext dc, double dt, double w, double h)
        {
            for (int i = 0; i < MaxEyes; i++)
            {
                ref SpookyEyes eye = ref _eyes[i];
                eye.StateTimer -= dt;

                if (eye.StateTimer <= 0)
                {
                    if (eye.IsVisible)
                    {
                        eye.IsVisible = false;
                        eye.StateTimer = (_rnd.NextDouble() * 5.0) + 3.0;
                    }
                    else
                    {
                        eye.IsVisible = true;
                        eye.StateTimer = (_rnd.NextDouble() * 3.5) + 2.0;
                        eye.X = _rnd.Next(60, (int)Math.Max(80, w - 120));
                        eye.Y = _rnd.Next(60, (int)Math.Max(80, h - 140));
                    }
                }

                eye.Alpha = eye.IsVisible
                    ? Math.Min(1.0, eye.Alpha + (dt * 2.5))
                    : Math.Max(0.0, eye.Alpha - (dt * 2.5));

                if (eye.Alpha > 0.05)
                {
                    double blinkHeight = 3.5 * Math.Clamp(1.0 - eye.BlinkProgress, 0.1, 1.0);
                    byte a = (byte)(eye.Alpha * 230);
                    SolidColorBrush glowB = new(Color.FromArgb(a, 0xFF, 0x55, 0x00));
                    SolidColorBrush pupilB = new(Color.FromArgb(a, 0xFF, 0xEE, 0x33));

                    dc.DrawEllipse(glowB, null, new Point(eye.X, eye.Y), 5.5, blinkHeight);
                    dc.DrawEllipse(pupilB, null, new Point(eye.X, eye.Y), 2.0, blinkHeight * 0.7);
                    dc.DrawEllipse(glowB, null, new Point(eye.X + 13, eye.Y), 5.5, blinkHeight);
                    dc.DrawEllipse(
                        pupilB,
                        null,
                        new Point(eye.X + 13, eye.Y),
                        2.0,
                        blinkHeight * 0.7
                    );
                }
            }
        }

        private static void DrawSpectralApparition(DrawingContext dc, double x, double y)
        {
            double scale = 1.3;
            StreamGeometry geom = new();
            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(new Point(x, y - (70 * scale)), isFilled: true, isClosed: true);
                ctx.QuadraticBezierTo(
                    new Point(x + (30 * scale), y - (65 * scale)),
                    new Point(x + (80 * scale), y - (35 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x + (50 * scale), y - (15 * scale)),
                    new Point(x + (25 * scale), y + (40 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x, y + (75 * scale)),
                    new Point(x - (25 * scale), y + (40 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x - (50 * scale), y - (15 * scale)),
                    new Point(x - (80 * scale), y - (35 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x - (30 * scale), y - (65 * scale)),
                    new Point(x, y - (70 * scale)),
                    isStroked: false,
                    isSmoothJoin: true
                );
            }
            geom.Freeze();

            dc.DrawGeometry(ApparitionBrush, null, geom);
        }

        private static void DrawBat(
            DrawingContext dc,
            double x,
            double y,
            double scale,
            double flapFactor
        )
        {
            double wingY = 12.0 * flapFactor;

            StreamGeometry geom = new();
            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(new Point(x, y), isFilled: true, isClosed: true);
                ctx.QuadraticBezierTo(
                    new Point(x + (15 * scale), y - wingY),
                    new Point(x + (30 * scale), y + (4 * scale)),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x + (45 * scale), y - wingY),
                    new Point(x + (60 * scale), y),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x + (45 * scale), y + (10 * scale)),
                    new Point(x + (30 * scale), y + (18 * scale)),
                    isStroked: true,
                    isSmoothJoin: true
                );
                ctx.QuadraticBezierTo(
                    new Point(x + (15 * scale), y + (10 * scale)),
                    new Point(x, y),
                    isStroked: true,
                    isSmoothJoin: true
                );
            }
            geom.Freeze();

            dc.DrawGeometry(BatFillBrush, BatOutlinePen, geom);
        }
    }
}
