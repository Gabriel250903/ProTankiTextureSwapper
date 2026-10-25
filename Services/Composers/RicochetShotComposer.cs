using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class RicochetShotComposer : BaseShotEffectComposer, IRicochetShotComposer
    {
        private static CachedTextureData? _ballGrayTemplateData;
        private static CachedTextureData? _ballAlphaTemplateData;
        private static CachedTextureData? _bounceGrayTemplateData;
        private static CachedTextureData? _bounceAlphaTemplateData;
        private static CachedTextureData? _sparksGrayTemplateData;
        private static CachedTextureData? _sparksAlphaTemplateData;

        public BitmapSource ComposeBall(
            RicochetShotRenderOptions options,
            RicochetShotComponent component = RicochetShotComponent.Main
        )
        {
            EnsureBallTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            return grayTemplate == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizeRgb(grayTemplate, options);
        }

        public BitmapSource ComposeBallPreview(
            RicochetShotRenderOptions options,
            RicochetShotComponent component = RicochetShotComponent.Main
        )
        {
            EnsureBallTemplatesLoaded();
            (CachedTextureData? grayTemplate, CachedTextureData? alphaTemplate, _) = GetTemplates(
                component
            );
            return grayTemplate == null || alphaTemplate == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizePreview(grayTemplate, alphaTemplate, options);
        }

        public void SaveBallTara(
            RicochetShotRenderOptions options,
            string outputPath,
            RicochetShotComponent component = RicochetShotComponent.Main
        )
        {
            EnsureBallTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);

            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Ricochet {component} alpha mask not found.",
                    alphaPath
                );
            }

            BitmapSource ballColorBmp = ComposeBall(options, component);
            byte[] colorJpegBytes = EncodeToJpeg(ballColorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        private static (
            CachedTextureData? Gray,
            CachedTextureData? Alpha,
            string AlphaFileName
        ) GetTemplates(RicochetShotComponent component)
        {
            return component switch
            {
                RicochetShotComponent.Bounce => (
                    _bounceGrayTemplateData,
                    _bounceAlphaTemplateData,
                    "ricochet_bounce_alpha.jpg"
                ),
                RicochetShotComponent.Sparks => (
                    _sparksGrayTemplateData,
                    _sparksAlphaTemplateData,
                    "ricochet_sparks_alpha.jpg"
                ),
                _ => (_ballGrayTemplateData, _ballAlphaTemplateData, "ricochet_ball_alpha.jpg"),
            };
        }

        private static void EnsureBallTemplatesLoaded()
        {
            if (
                _ballGrayTemplateData != null
                && _ballAlphaTemplateData != null
                && _bounceGrayTemplateData != null
                && _bounceAlphaTemplateData != null
                && _sparksGrayTemplateData != null
                && _sparksAlphaTemplateData != null
            )
            {
                return;
            }

            string dir = GetTexturesDirectory();

            string ballGrayPath = Path.Combine(dir, "ricochet_ball_gray.png");
            string ballAlphaPath = Path.Combine(dir, "ricochet_ball_alpha.jpg");
            string bounceGrayPath = Path.Combine(dir, "ricochet_bounce_gray.png");
            string bounceAlphaPath = Path.Combine(dir, "ricochet_bounce_alpha.jpg");
            string sparksGrayPath = Path.Combine(dir, "ricochet_sparks_gray.png");
            string sparksAlphaPath = Path.Combine(dir, "ricochet_sparks_alpha.jpg");

            if (File.Exists(ballGrayPath) && _ballGrayTemplateData == null)
            {
                _ballGrayTemplateData = LoadTextureData(ballGrayPath);
            }

            if (File.Exists(ballAlphaPath) && _ballAlphaTemplateData == null)
            {
                _ballAlphaTemplateData = LoadTextureData(ballAlphaPath);
            }

            if (File.Exists(bounceGrayPath) && _bounceGrayTemplateData == null)
            {
                _bounceGrayTemplateData = LoadTextureData(bounceGrayPath);
            }

            if (File.Exists(bounceAlphaPath) && _bounceAlphaTemplateData == null)
            {
                _bounceAlphaTemplateData = LoadTextureData(bounceAlphaPath);
            }

            if (File.Exists(sparksGrayPath) && _sparksGrayTemplateData == null)
            {
                _sparksGrayTemplateData = LoadTextureData(sparksGrayPath);
            }

            if (File.Exists(sparksAlphaPath) && _sparksAlphaTemplateData == null)
            {
                _sparksAlphaTemplateData = LoadTextureData(sparksAlphaPath);
            }
        }

        private static string GetTexturesDirectory()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Ricochet"
            );
            if (!Directory.Exists(path))
            {
                string alt = Path.Combine(
                    AppContext.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Ricochet"
                );
                if (Directory.Exists(alt))
                {
                    return alt;
                }

                string asmDir =
                    Path.GetDirectoryName(typeof(RicochetShotComposer).Assembly.Location) ?? "";
                string asmPath = Path.Combine(
                    asmDir,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Ricochet"
                );
                if (Directory.Exists(asmPath))
                {
                    return asmPath;
                }
            }

            return path;
        }
    }
}
