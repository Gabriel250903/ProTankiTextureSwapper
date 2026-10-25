namespace TextureSwapper.Core
{
    public static class Constants
    {
        public const string AppName = "ProTanki Texture Swapper";
        public const string GameProcessName = "ProTanki";
        public const string HullsSkinsJson = "skins_hulls.json";
        public const string TurretsSkinsJson = "skins_turrets.json";
        public const string SuppliesSkinsJson = "skins_supplies.json";
        public const string PaintsSkinsJson = "skins_paints.json";
        public const string InGamePaintsJson = "ingame_paints.json";
        public const string ShotEffectsJson = "shot_effects.json";
        public const string BackupsDir = "Backups";
        public const string OriginalsDir = "Originals";
        public const string LogsDir = "Logs";
        public const string AppLogFile = "app-log.txt";
        public const string StandaloneLoaderDir = "StandaloneLoader";
        public const string LocalStoreDir = "Local Store";
        public const string CacheDirName = "cache";
        public const string GitHubRepo = "Gabriel250903/ProTankiTextureSwapper";
        public const string GitHubRawUrl =
            "https://raw.githubusercontent.com/Gabriel250903/ProTankiTextureSwapper/main";
        public const string GitHubApiUrl =
            "https://api.github.com/repos/Gabriel250903/ProTankiTextureSwapper";
        public const string TexturesZipFileName = "textures.zip";
        public const string TexturesVersionFileName = ".textures_version";
        public const string TexturesLatestDownloadUrl =
            "https://github.com/Gabriel250903/ProTankiTextureSwapper/releases/latest/download/textures.zip";
        public const string PublisherPublicKey =
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEObAIYp8M4ou4UkucutCo69vQaXSEWij1TYvuuC8j1kWD3O+mEWvTPL8McPoIOb7KTd/S6kdGn7SUEevshUch+g==";
        public const string SpawnersSkinsJson = "skins_spawners.json";
        public const string GatekeeperWorkerUrl =
            "https://textureswapper-gatekeeper.flytoxp.workers.dev";
        public const string GatekeeperApiKey = "ts-gk-a7f3e9d1c4b8205f6e1a9c3d7b4f8e2a";
        public const string GumroadBuyUrl = "https://flytoxp.gumroad.com/l/mfdjom";
        public const string DiscordInviteUrl = "https://discord.gg/3Bgs7Jd9xk";
        public const string DiscordContactUsername = "noizy3345";

        public const string RicochetMainBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0NS8zMjAyMzUxMDM1Njc3Mi9pbWFnZS50YXJh";
        public const string RicochetBounceBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0NC8zMjAyMzUxMDM1NjYyNy9pbWFnZS50YXJh";
        public const string RicochetSparksTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0My8zMjAyMzUxMDM1NjU2NS9pbWFnZS50YXJh";

        public const string FreezeMainBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3My8zMjAyMzUxMDM2MTY1NS9pbWFnZS50YXJh";
        public const string FreezeBounceBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3NC8zMjAyMzUxMDM2MTcxNy9pbWFnZS50YXJh";

        public const string TwinsMainBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzM3NS8zMjAyMzUxMDM2MDIyNS9pbWFnZS50YXJh";
        public const string TwinsBounceBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzEvMzIwMjM1MTAzNjA0MDIvaW1hZ2UudGFyYQ==";

        public const string FirebirdMainBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzM2LzMyMDIzNTEwMzYxMjUyL2ltYWdlLnRhcmE=";
        public const string FirebirdBounceBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzM3LzMyMDIzNTEwMzYxNDA2L2ltYWdlLnRhcmE=";
        public const string FirebirdMuzzleConeTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMzLzMyMDIzNTEwMzYxMjA1L2ltYWdlLnRhcmE=";

        public const string RailgunCharge1Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI0LzMyMDIzNTEwMzYxMDc0L2ltYWdlLmpwZw==";
        public const string RailgunCharge2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI1LzMyMDIzNTEwMzYxMTEwL2ltYWdlLmpwZw==";
        public const string RailgunChargeSphereTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIzLzMyMDIzNTEwMzYxMDYwL2ltYWdlLmpwZw==";
        public const string RailgunBeamCoreTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzM0LzMyMDIzNTEwMzYxMjMwL2ltYWdlLmpwZw==";
        public const string RailgunBeamSpiralTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMyLzMyMDIzNTEwMzYxMTc0L2ltYWdlLmpwZw==";

        public const string RailgunLegacyCharge1Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI0LzMxNDY0MTQzMDExNzAxL2ltYWdlLmpwZw==";
        public const string RailgunLegacyCharge2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI1LzMxNDY0MTQzMDExNzM2L2ltYWdlLmpwZw==";
        public const string RailgunLegacyChargeSphereTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIzLzMxNDY0MTQzMDExNjQzL2ltYWdlLmpwZw==";
        public const string RailgunLegacyBeamCoreTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzM0LzMxNDY0MTQzMDEyMjQyL2ltYWdlLmpwZw==";
        public const string RailgunLegacyBeamSpiralTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMyLzMxNDY0MTQzMDEyMTQyL2ltYWdlLmpwZw==";

        public const string HammerShotAcrossTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzEwLzMyMDI1NDM2MjUyMjYwL2ltYWdlLnRhcmE=";
        public const string HammerShotAcrossRev2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC83My8yNjUvMzIwMjU0MzYyNTIzMDEvaW1hZ2UudGFyYQ==";
        public const string HammerShotAlongTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzcvMzIwMjU0MzYyNzU0MTYvaW1hZ2UudGFyYQ==";
        public const string HammerSmokeTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzYvMzIwMjM1MTAzNjA0NzQvaW1hZ2UudGFyYQ==";

        public const string HammerPelletTrailImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzEyLzMyMDI1NDM2MjI0NDM3L2ltYWdlLmpwZw==";
        public const string HammerPelletTrailAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzEyLzMyMDI1NDM2MjI0NDM3L2FscGhhLmpwZw==";
        public const string HammerPelletTrailImageRev2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC83My8yNjQvMzIwMjU0MzYyMjQ0NjMvaW1hZ2UuanBn";
        public const string HammerPelletTrailAlphaRev2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC83My8yNjQvMzIwMjU0MzYyMjQ0NjMvYWxwaGEuanBn";
        public const string HammerSparkleTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzExLzMyMDI1NDM2MzA3MTU0L2ltYWdlLmpwZw==";
        public const string HammerSparkleRev2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC83My8yNjcvMzIwMjU0MzYzMDcyMTMvaW1hZ2UuanBn";

        public const string IsidaDamageBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3Ni8zMjAyMzUxMDM2MjAxNS9pbWFnZS50YXJh";
        public const string IsidaDamageBeamImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3Ny8zMjAyMzUxMDM2MjEwMy9pbWFnZS5qcGc=";
        public const string IsidaDamageBeamAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3Ny8zMjAyMzUxMDM2MjEwMy9hbHBoYS5qcGc=";

        public const string IsidaHealBallTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMwMS8zMjAyMzUxMDM2MjEyNC9pbWFnZS50YXJh";
        public const string IsidaHealBeamImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMwMi8zMjAyMzUxMDM2MjIxNC9pbWFnZS5qcGc=";
        public const string IsidaHealBeamAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMwMi8zMjAyMzUxMDM2MjIxNC9hbHBoYS5qcGc=";

        public const string IsidaDamageImpactTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMxMi8zMjAyMzUxMDM2MjM3NS9pbWFnZS50YXJh";
        public const string IsidaDamageFlareTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMxNC8zMjAyMzUxMDM2MjQ0My9pbWFnZS50YXJh";

        public const string IsidaHealShaftImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMxNy8zMjAyMzUxMDM2MjUwMS9pbWFnZS5qcGc=";
        public const string IsidaHealShaftAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMxNy8zMjAyMzUxMDM2MjUwMS9hbHBoYS5qcGc=";

        public const string ShaftBurstTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIwLzMyMDIzNTEwMzYwNzc2L2ltYWdlLnRhcmE=";
        public const string ShaftTrailImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIxLzMyMDIzNTEwMzYxMDM2L2ltYWdlLmpwZw==";
        public const string ShaftTrailAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIxLzMyMDIzNTEwMzYxMDM2L2FscGhhLmpwZw==";
        public const string ShaftMuzzleSmokeImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIyLzMyMDIzNTEwMzYxMDUxL2ltYWdlLmpwZw==";
        public const string ShaftMuzzleSmokeAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzIyLzMyMDIzNTEwMzYxMDUxL2FscGhhLmpwZw==";
        public const string ShaftLaserDotImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI2LzMyMDIzNTEwMzYxMTIzL2ltYWdlLmpwZw==";
        public const string ShaftLaserDotAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI2LzMyMDIzNTEwMzYxMTIzL2FscGhhLmpwZw==";
        public const string ShaftMuzzleFlashTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3LzMyMDIzNTEwMzYxMTI3L2ltYWdlLnRhcmE=";
        public const string ShaftImpactTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzM1LzMyMDIzNTEwMzYxMjM1L2ltYWdlLnRhcmE=";

        public const string VulcanTracersTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzQxLzMyMDIzNTEwMzYxNTY2L2ltYWdlLnRhcmE=";
        public const string VulcanMuzzleBurstTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzE0LzMyMDIzNTEwMzYwNjI3L2ltYWdlLnRhcmE=";
        public const string VulcanMuzzleFlameTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzE1LzMyMDIzNTEwMzYwNjY3L2ltYWdlLnRhcmE=";
        public const string VulcanSmokePuffTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzE2LzMyMDIzNTEwMzYwNzI2L2ltYWdlLnRhcmE=";
        public const string VulcanSmokeCloudTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzEzLzMyMDIzNTEwMzYwNjEyL2ltYWdlLnRhcmE=";
        public const string VulcanOverheatTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzE3LzMyMDI1NDM2MzA3MTU3L2ltYWdlLnRhcmE=";

        public const string ThunderExplosionMainTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzEzMy8zMjAyMzUxMDM2NDAxMi9pbWFnZS50YXJh";
        public const string ThunderExplosionSmokeTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzEzNC8zMjAyMzUxMDM2NDE2MC9pbWFnZS50YXJh";
        public const string ThunderExplosionRingTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzEzNS8zMjAyMzUxMDM2NDIxNy9pbWFnZS50YXJh";
        public const string ThunderExplosionVar1Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0Mi8zMjAyMzUxMDM2NDI3My9pbWFnZS50YXJh";
        public const string ThunderExplosionVar2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0My8zMjAyMzUxMDM2NDQzMC9pbWFnZS50YXJh";
        public const string ThunderExplosionVar3Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0NC8zMjAyMzUxMDM2NDU2NS9pbWFnZS50YXJh";
        public const string ThunderExplosionVar4Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0NS8zMjAyMzUxMDM2NDcyNy9pbWFnZS50YXJh";
        public const string ThunderExplosionVar5Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1Mi8zMjAyMzUxMDM2NTExMC9pbWFnZS50YXJh";
        public const string ThunderExplosionVar6Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1My8zMjAyMzUxMDM2NTI0NS9pbWFnZS50YXJh";
        public const string ThunderExplosionVar7Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1NC8zMjAyMzUxMDM2NTQwMS9pbWFnZS50YXJh";
        public const string ThunderExplosionVar8Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1NS8zMjAyMzUxMDM2NTU0NC9pbWFnZS50YXJh";
        public const string ThunderProjectileShaftImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzExNy8zMjAyMzUxMDM2MzY3NC9pbWFnZS5qcGc=";
        public const string ThunderProjectileShaftAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzExNy8zMjAyMzUxMDM2MzY3NC9hbHBoYS5qcGc=";
        public const string ThunderProjectileHeadImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzEyMC8zMjAyMzUxMDM2MzcwNC9pbWFnZS5qcGc=";
        public const string ThunderProjectileHeadAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzEyMC8zMjAyMzUxMDM2MzcwNC9hbHBoYS5qcGc=";
        public const string ThunderShockwaveImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0MC8zMjAyMzUxMDM2NDI1NS9pbWFnZS5qcGc=";
        public const string ThunderShockwaveAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0MC8zMjAyMzUxMDM2NDI1NS9hbHBoYS5qcGc=";
        public const string ThunderMuzzleFlashImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0Ny8zMjAyMzUxMDM2NTA2MS9pbWFnZS5qcGc=";
        public const string ThunderMuzzleFlashAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE0Ny8zMjAyMzUxMDM2NTA2MS9hbHBoYS5qcGc=";
        public const string ThunderShockwave2ImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1MC8zMjAyMzUxMDM2NTA3MS9pbWFnZS5qcGc=";
        public const string ThunderShockwave2AlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1MC8zMjAyMzUxMDM2NTA3MS9hbHBoYS5qcGc=";
        public const string ThunderMuzzleFlashVarImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1Ny8zMjAyMzUxMDM2NTcxMi9pbWFnZS5qcGc=";
        public const string ThunderMuzzleFlashVarAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC80LzE1Ny8zMjAyMzUxMDM2NTcxMi9hbHBoYS5qcGc=";

        public const string DustMoundTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIyNS8zMjAyMzUxMDM1NjA3MS9pbWFnZS50YXJh";
        public const string DustSmokeTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIyNi8zMjAyMzUxMDM1NjIyMy9pbWFnZS50YXJh";
        public const string DustTracerTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzNS8zMjAyMzUxMDM1NjM0MS9pbWFnZS50YXJh";
        public const string DustTrailTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzNi8zMjAyMzUxMDM1NjM1MC9pbWFnZS50YXJh";

        public const string SmokyMuzzleFlashTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzNy8zMjAyMzUxMDM1NjM1Ny9pbWFnZS50YXJh";
        public const string SmokyCriticalHit1Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0MC8zMjAyMzUxMDM1NjQxNy9pbWFnZS50YXJh";
        public const string SmokyCriticalHit2Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0MS8zMjAyMzUxMDM1NjQ2MS9pbWFnZS50YXJh";
        public const string SmokyCriticalHit3Target =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0Mi8zMjAyMzUxMDM1NjUyMy9pbWFnZS50YXJh";

        public const string SmokyBulletImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzMy8zMjAyMzUxMDM1NjMyNC9pbWFnZS5qcGc=";
        public const string SmokyBulletAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzMy8zMjAyMzUxMDM1NjMyNC9hbHBoYS5qcGc=";
        public const string SmokyGlowImageTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzMi8zMjAyMzUxMDM1NjMxNS9pbWFnZS5qcGc=";
        public const string SmokyGlowAlphaTarget =
            "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzIzMi8zMjAyMzUxMDM1NjMxNS9hbHBoYS5qcGc=";
    }
}
