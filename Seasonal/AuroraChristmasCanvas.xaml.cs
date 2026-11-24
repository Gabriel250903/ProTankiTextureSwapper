using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TextureSwapper.Seasonal
{
    public partial class AuroraChristmasCanvas : UserControl
    {
        private struct SnowParticle
        {
            public double X;
            public double Y;
            public double SpeedY;
            public double DriftFreq;
            public double DriftAmp;
            public double Phase;
            public double Radius;
            public byte Alpha;
        }

        private struct FairyStar
        {
            public double NormX;
            public double NormY;
            public double Phase;
            public double Speed;
            public double Radius;
            public Color BaseColor;
        }

        private const int MaxSnow = 70;
        private const int MaxStars = 22;

        private readonly SnowParticle[] _snow = new SnowParticle[MaxSnow];
        private readonly FairyStar[] _stars = new FairyStar[MaxStars];
        private readonly Random _rng = new();
        private readonly Stopwatch _sw = new();

        private double _lastTime;
        private bool _isRendering;
        private bool _initialized;

        public AuroraChristmasCanvas()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
            Loaded += (_, _) => UpdateRenderingState();
            Unloaded += (_, _) => StopRendering();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateRenderingState();
        }

        private void UpdateRenderingState()
        {
            if (IsVisible && IsLoaded)
            {
                StartRendering();
            }
            else
            {
                StopRendering();
            }
        }

        private void StartRendering()
        {
            if (_isRendering)
            {
                return;
            }

            if (!_initialized)
            {
                InitParticles();
                _initialized = true;
            }

            _sw.Restart();
            _lastTime = 0;
            _isRendering = true;
            CompositionTarget.Rendering += OnRenderFrame;
        }

        private void StopRendering()
        {
            if (!_isRendering)
            {
                return;
            }

            _isRendering = false;
            CompositionTarget.Rendering -= OnRenderFrame;
            _sw.Stop();
            using DrawingContext dc = VisualHost.Visual.RenderOpen();
        }

        private void InitParticles()
        {
            double w = Math.Max(ActualWidth, 1100);
            double h = Math.Max(ActualHeight, 700);

            for (int i = 0; i < MaxSnow; i++)
            {
                double depth = _rng.NextDouble();
                _snow[i] = new SnowParticle
                {
                    X = _rng.NextDouble() * w,
                    Y = _rng.NextDouble() * h,
                    SpeedY = 16 + (depth * 44),
                    DriftFreq = 0.55 + (_rng.NextDouble() * 1.1),
                    DriftAmp = 10 + (depth * 20),
                    Phase = _rng.NextDouble() * Math.PI * 2,
                    Radius = 1.1 + (depth * 2.7),
                    Alpha = (byte)(55 + (depth * 150)),
                };
            }

            Color[] palette =
            [
                Color.FromRgb(253, 224, 71),
                Color.FromRgb(74, 222, 128),
                Color.FromRgb(248, 113, 113),
                Color.FromRgb(103, 232, 249),
            ];

            for (int i = 0; i < MaxStars; i++)
            {
                _stars[i] = new FairyStar
                {
                    NormX = 0.03 + (_rng.NextDouble() * 0.94),
                    NormY = 0.04 + (_rng.NextDouble() * 0.36),
                    Phase = _rng.NextDouble() * Math.PI * 2,
                    Speed = 1.2 + (_rng.NextDouble() * 2.4),
                    Radius = 1.4 + (_rng.NextDouble() * 2.0),
                    BaseColor = palette[i % palette.Length],
                };
            }
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            double t = _sw.Elapsed.TotalSeconds;
            double dt = Math.Min(t - _lastTime, 0.05);
            _lastTime = t;

            using DrawingContext dc = VisualHost.Visual.RenderOpen();

            DrawAuroraRibbon(
                dc,
                w,
                h,
                centerX: 0.25 + (Math.Sin(t * 0.22) * 0.14),
                centerY: 0.12 + (Math.Cos(t * 0.18) * 0.05),
                core: Color.FromArgb((byte)(46 + (Math.Sin(t * 0.5) * 14)), 16, 185, 129),
                mid: Color.FromArgb(22, 6, 182, 212)
            );
            DrawAuroraRibbon(
                dc,
                w,
                h,
                centerX: 0.72 + (Math.Cos(t * 0.19) * 0.15),
                centerY: 0.15 + (Math.Sin(t * 0.25) * 0.05),
                core: Color.FromArgb((byte)(34 + (Math.Cos(t * 0.45) * 12)), 168, 85, 247),
                mid: Color.FromArgb(18, 234, 179, 8)
            );

            DrawShootingStar(dc, w, h, t);

            DrawSantaSleigh(dc, w, h, t);

            for (int i = 0; i < MaxStars; i++)
            {
                ref FairyStar star = ref _stars[i];
                double pulse = 0.5 + (0.5 * Math.Sin((t * star.Speed) + star.Phase));
                byte alpha = (byte)(25 + (pulse * 125));
                SolidColorBrush brush = new(
                    Color.FromArgb(alpha, star.BaseColor.R, star.BaseColor.G, star.BaseColor.B)
                );
                brush.Freeze();
                double r = star.Radius * (0.75 + (pulse * 0.45));
                dc.DrawEllipse(brush, null, new Point(star.NormX * w, star.NormY * h), r, r);
            }

            DrawTopGarland(dc, w, t);
            DrawSnowyHills(dc, w, h);
            DrawChristmasTree(dc, baseX: w * 0.09, baseY: h - 10, scale: 1.05, t: t, hasStar: true);
            DrawChristmasTree(
                dc,
                baseX: w * 0.17,
                baseY: h - 6,
                scale: 0.72,
                t: t + 1.1,
                hasStar: false
            );
            DrawChristmasTree(
                dc,
                baseX: w * 0.84,
                baseY: h - 12,
                scale: 0.88,
                t: t + 2.0,
                hasStar: true
            );
            DrawSnowman(dc, baseX: w * 0.92, baseY: h - 14, t: t);

            for (int i = 0; i < MaxSnow; i++)
            {
                ref SnowParticle s = ref _snow[i];
                s.Y += s.SpeedY * dt;
                if (s.Y > h + 10)
                {
                    s.Y = -10;
                    s.X = _rng.NextDouble() * w;
                }

                double x = s.X + (Math.Sin((t * s.DriftFreq) + s.Phase) * s.DriftAmp);
                SolidColorBrush snowBrush = new(Color.FromArgb(s.Alpha, 238, 248, 255));
                snowBrush.Freeze();
                dc.DrawEllipse(snowBrush, null, new Point(x, s.Y), s.Radius, s.Radius);
            }
        }

        private static void DrawAuroraRibbon(
            DrawingContext dc,
            double w,
            double h,
            double centerX,
            double centerY,
            Color core,
            Color mid
        )
        {
            RadialGradientBrush brush = new()
            {
                Center = new Point(centerX, centerY),
                GradientOrigin = new Point(centerX, centerY),
                RadiusX = 0.55,
                RadiusY = 0.42,
            };
            brush.GradientStops.Add(new GradientStop(core, 0.0));
            brush.GradientStops.Add(new GradientStop(mid, 0.48));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, mid.R, mid.G, mid.B), 1.0));
            brush.Freeze();

            dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
        }

        private static void DrawShootingStar(DrawingContext dc, double w, double h, double t)
        {
            double cycle = t % 8.5;
            if (cycle > 1.3)
            {
                return;
            }

            double p = cycle / 1.3;
            double startX = w * 0.78;
            double startY = h * 0.06;
            double headX = startX - (p * w * 0.36);
            double headY = startY + (p * h * 0.16);
            double tailX = headX + (w * 0.07);
            double tailY = headY - (h * 0.032);

            byte alpha = (byte)(Math.Sin(p * Math.PI) * 190);
            Pen tailPen = new(new SolidColorBrush(Color.FromArgb(alpha, 254, 240, 138)), 2.0);
            tailPen.Freeze();
            dc.DrawLine(tailPen, new Point(tailX, tailY), new Point(headX, headY));

            SolidColorBrush headBrush = new(Color.FromArgb(alpha, 255, 255, 255));
            headBrush.Freeze();
            dc.DrawEllipse(headBrush, null, new Point(headX, headY), 2.5, 2.5);
        }

        private static void DrawSantaSleigh(DrawingContext dc, double w, double h, double t)
        {
            double cycle = t % 24.0;
            if (cycle > 10.0)
            {
                return;
            }

            double p = cycle / 10.0;
            double x = -80 + (p * (w + 180));
            double y = (h * 0.15) + (Math.Sin(p * Math.PI * 2.0) * 18.0);

            SolidColorBrush silhouette = new(Color.FromArgb(65, 226, 232, 240));
            silhouette.Freeze();
            Pen reinPen = new(new SolidColorBrush(Color.FromArgb(45, 253, 224, 71)), 1.0);
            reinPen.Freeze();

            dc.DrawRoundedRectangle(silhouette, null, new Rect(x, y, 26, 10), 4, 4);
            dc.DrawLine(reinPen, new Point(x - 2, y + 12), new Point(x + 28, y + 12));

            for (int r = 1; r <= 3; r++)
            {
                double rx = x + 28 + (r * 16);
                double ry = y + 1 + (Math.Sin((t * 6.0) + r) * 2.0);
                dc.DrawEllipse(silhouette, null, new Point(rx, ry + 3), 5, 3);
                dc.DrawEllipse(silhouette, null, new Point(rx + 5, ry), 2.2, 2.2);
                dc.DrawLine(reinPen, new Point(x + 25, y + 4), new Point(rx, ry + 3));
            }
        }

        private static void DrawTopGarland(DrawingContext dc, double w, double t)
        {
            int swags = Math.Max(4, (int)(w / 220));
            double swagWidth = w / swags;
            Pen wirePen = new(new SolidColorBrush(Color.FromArgb(80, 20, 83, 45)), 2.0);
            wirePen.Freeze();

            Color[] bulbColors =
            [
                Color.FromRgb(239, 68, 68),
                Color.FromRgb(250, 204, 21),
                Color.FromRgb(34, 197, 94),
                Color.FromRgb(56, 189, 248),
            ];

            int bulbIndex = 0;
            for (int s = 0; s < swags; s++)
            {
                double x0 = s * swagWidth;
                double x1 = (s + 1) * swagWidth;
                double midX = (x0 + x1) * 0.5;

                PathGeometry geo = new();
                PathFigure fig = new() { StartPoint = new Point(x0, 2) };
                fig.Segments.Add(
                    new QuadraticBezierSegment(new Point(midX, 22), new Point(x1, 2), true)
                );
                geo.Figures.Add(fig);
                geo.Freeze();
                dc.DrawGeometry(null, wirePen, geo);

                for (int b = 1; b <= 4; b++)
                {
                    double u = b / 5.0;
                    double bx = ((1 - u) * (1 - u) * x0) + (2 * (1 - u) * u * midX) + (u * u * x1);
                    double by = ((1 - u) * (1 - u) * 2) + (2 * (1 - u) * u * 22) + (u * u * 2);

                    Color c = bulbColors[bulbIndex % bulbColors.Length];
                    double wave = 0.45 + (0.55 * Math.Sin((t * 3.0) + (bulbIndex * 0.9)));
                    byte glowAlpha = (byte)(35 + (wave * 75));
                    byte coreAlpha = (byte)(110 + (wave * 130));

                    SolidColorBrush glow = new(Color.FromArgb(glowAlpha, c.R, c.G, c.B));
                    glow.Freeze();
                    SolidColorBrush core = new(Color.FromArgb(coreAlpha, c.R, c.G, c.B));
                    core.Freeze();

                    dc.DrawEllipse(glow, null, new Point(bx, by + 2), 8, 8);
                    dc.DrawEllipse(core, null, new Point(bx, by + 2), 3.2, 3.8);
                    bulbIndex++;
                }
            }
        }

        private static void DrawSnowyHills(DrawingContext dc, double w, double h)
        {
            SolidColorBrush backHill = new(Color.FromArgb(26, 186, 230, 253));
            backHill.Freeze();
            SolidColorBrush frontHill = new(Color.FromArgb(38, 241, 245, 249));
            frontHill.Freeze();

            PathGeometry backGeo = new();
            PathFigure backFig = new() { StartPoint = new Point(0, h) };
            backFig.Segments.Add(new LineSegment(new Point(0, h - 38), false));
            backFig.Segments.Add(
                new BezierSegment(
                    new Point(w * 0.30, h - 75),
                    new Point(w * 0.65, h - 20),
                    new Point(w, h - 55),
                    false
                )
            );
            backFig.Segments.Add(new LineSegment(new Point(w, h), false));
            backGeo.Figures.Add(backFig);
            backGeo.Freeze();
            dc.DrawGeometry(backHill, null, backGeo);

            PathGeometry frontGeo = new();
            PathFigure frontFig = new() { StartPoint = new Point(0, h) };
            frontFig.Segments.Add(new LineSegment(new Point(0, h - 22), false));
            frontFig.Segments.Add(
                new BezierSegment(
                    new Point(w * 0.28, h - 5),
                    new Point(w * 0.72, h - 52),
                    new Point(w, h - 24),
                    false
                )
            );
            frontFig.Segments.Add(new LineSegment(new Point(w, h), false));
            frontGeo.Figures.Add(frontFig);
            frontGeo.Freeze();
            dc.DrawGeometry(frontHill, null, frontGeo);
        }

        private static void DrawChristmasTree(
            DrawingContext dc,
            double baseX,
            double baseY,
            double scale,
            double t,
            bool hasStar
        )
        {
            double sway = Math.Sin(t * 1.4) * 3.5 * scale;

            SolidColorBrush trunkBrush = new(Color.FromArgb(110, 120, 53, 15));
            trunkBrush.Freeze();
            dc.DrawRectangle(
                trunkBrush,
                null,
                new Rect(baseX - (5 * scale), baseY - (16 * scale), 10 * scale, 16 * scale)
            );

            Color[] tierColors =
            [
                Color.FromArgb(95, 20, 83, 45),
                Color.FromArgb(105, 22, 101, 52),
                Color.FromArgb(115, 21, 128, 61),
            ];

            for (int tier = 0; tier < 3; tier++)
            {
                double tierBottomY = baseY - ((14 + (tier * 24)) * scale);
                double tierTopY = tierBottomY - ((38 - (tier * 4)) * scale);
                double halfW = (34 - (tier * 7)) * scale;
                double tierSway = sway * ((tier + 1) / 3.0);

                PathGeometry tri = new();
                PathFigure fig = new() { StartPoint = new Point(baseX + tierSway, tierTopY) };
                fig.Segments.Add(
                    new LineSegment(new Point(baseX - halfW + (tierSway * 0.4), tierBottomY), true)
                );
                fig.Segments.Add(
                    new LineSegment(new Point(baseX + halfW + (tierSway * 0.4), tierBottomY), true)
                );
                fig.IsClosed = true;
                tri.Figures.Add(fig);
                tri.Freeze();

                SolidColorBrush pineBrush = new(tierColors[tier]);
                pineBrush.Freeze();
                dc.DrawGeometry(pineBrush, null, tri);

                Color ornColor =
                    tier == 0
                        ? Color.FromRgb(248, 113, 113)
                        : (tier == 1 ? Color.FromRgb(250, 204, 21) : Color.FromRgb(96, 165, 250));
                byte ornAlpha = (byte)(120 + (110 * (0.5 + (0.5 * Math.Sin((t * 3.2) + tier)))));
                SolidColorBrush ornBrush = new(
                    Color.FromArgb(ornAlpha, ornColor.R, ornColor.G, ornColor.B)
                );
                ornBrush.Freeze();
                dc.DrawEllipse(
                    ornBrush,
                    null,
                    new Point(baseX - (halfW * 0.45) + (tierSway * 0.6), tierBottomY - (8 * scale)),
                    2.6 * scale,
                    2.6 * scale
                );
                dc.DrawEllipse(
                    ornBrush,
                    null,
                    new Point(
                        baseX + (halfW * 0.42) + (tierSway * 0.6),
                        tierBottomY - (12 * scale)
                    ),
                    2.6 * scale,
                    2.6 * scale
                );
            }

            if (hasStar)
            {
                double starX = baseX + sway;
                double starY = baseY - (98 * scale);
                byte starAlpha = (byte)(150 + (95 * (0.5 + (0.5 * Math.Sin(t * 2.8)))));
                SolidColorBrush starGlow = new(Color.FromArgb((byte)(starAlpha / 3), 253, 224, 71));
                starGlow.Freeze();
                SolidColorBrush starCore = new(Color.FromArgb(starAlpha, 254, 240, 138));
                starCore.Freeze();
                dc.DrawEllipse(starGlow, null, new Point(starX, starY), 10 * scale, 10 * scale);
                dc.DrawEllipse(starCore, null, new Point(starX, starY), 4 * scale, 4 * scale);
            }
        }

        private static void DrawSnowman(DrawingContext dc, double baseX, double baseY, double t)
        {
            SolidColorBrush snow = new(Color.FromArgb(135, 241, 245, 249));
            snow.Freeze();
            SolidColorBrush dark = new(Color.FromArgb(170, 15, 23, 42));
            dark.Freeze();
            SolidColorBrush red = new(Color.FromArgb(185, 220, 38, 38));
            red.Freeze();
            SolidColorBrush carrot = new(Color.FromArgb(200, 249, 115, 22));
            carrot.Freeze();
            SolidColorBrush gold = new(Color.FromArgb(190, 250, 204, 21));
            gold.Freeze();

            double bob = Math.Sin(t * 2.0) * 1.2;

            dc.DrawEllipse(snow, null, new Point(baseX, baseY - 18), 18, 17);
            dc.DrawEllipse(snow, null, new Point(baseX, baseY - 43 + (bob * 0.5)), 13, 12);
            dc.DrawEllipse(snow, null, new Point(baseX, baseY - 62 + bob), 9.5, 9.5);

            dc.DrawEllipse(dark, null, new Point(baseX, baseY - 46 + (bob * 0.5)), 1.6, 1.6);
            dc.DrawEllipse(dark, null, new Point(baseX, baseY - 39 + (bob * 0.5)), 1.6, 1.6);
            dc.DrawEllipse(dark, null, new Point(baseX - 3.2, baseY - 64 + bob), 1.3, 1.3);
            dc.DrawEllipse(dark, null, new Point(baseX + 3.2, baseY - 64 + bob), 1.3, 1.3);

            dc.DrawEllipse(carrot, null, new Point(baseX + 4, baseY - 61 + bob), 4.0, 1.6);

            dc.DrawRoundedRectangle(
                red,
                null,
                new Rect(baseX - 9, baseY - 54 + bob, 18, 4.5),
                2,
                2
            );
            double scarfWave = Math.Sin(t * 3.1) * 4.0;
            Pen scarfPen = new(red, 4.0);
            scarfPen.Freeze();
            dc.DrawLine(
                scarfPen,
                new Point(baseX + 4, baseY - 52 + bob),
                new Point(baseX + 14 + scarfWave, baseY - 41 + bob)
            );

            dc.DrawRoundedRectangle(
                dark,
                null,
                new Rect(baseX - 11, baseY - 72 + bob, 22, 3),
                1,
                1
            );
            dc.DrawRoundedRectangle(
                dark,
                null,
                new Rect(baseX - 7, baseY - 84 + bob, 14, 12),
                2,
                2
            );
            dc.DrawRectangle(gold, null, new Rect(baseX - 7, baseY - 75 + bob, 14, 2.2));

            Pen armPen = new(new SolidColorBrush(Color.FromArgb(150, 120, 53, 15)), 2.0);
            armPen.Freeze();
            double waveAngle = Math.Sin(t * 3.5) * 6.0;
            dc.DrawLine(
                armPen,
                new Point(baseX - 12, baseY - 44 + (bob * 0.5)),
                new Point(baseX - 24, baseY - 53 + waveAngle)
            );
            dc.DrawLine(
                armPen,
                new Point(baseX + 12, baseY - 44 + (bob * 0.5)),
                new Point(baseX + 24, baseY - 53 - waveAngle)
            );
        }
    }
}
