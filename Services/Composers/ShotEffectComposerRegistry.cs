using TextureSwapper.Core.Interfaces;

namespace TextureSwapper.Services.Composers
{
    public sealed class ShotEffectComposerRegistry(
        IFirebirdShotComposer firebird,
        IFreezeShotComposer freeze,
        IRicochetShotComposer ricochet,
        ITwinsShotComposer twins,
        IRailgunShotComposer railgun,
        IHammerShotComposer hammer,
        IIsidaShotComposer isida,
        IShaftShotComposer shaft,
        IVulcanShotComposer vulcan,
        IThunderShotComposer thunder,
        IDustEffectComposer dustEffect,
        ISmokyShotComposer smoky
    ) : IShotEffectComposerRegistry
    {
        public IFirebirdShotComposer Firebird { get; } = firebird;
        public IFreezeShotComposer Freeze { get; } = freeze;
        public IRicochetShotComposer Ricochet { get; } = ricochet;
        public ITwinsShotComposer Twins { get; } = twins;
        public IRailgunShotComposer Railgun { get; } = railgun;
        public IHammerShotComposer Hammer { get; } = hammer;
        public IIsidaShotComposer Isida { get; } = isida;
        public IShaftShotComposer Shaft { get; } = shaft;
        public IVulcanShotComposer Vulcan { get; } = vulcan;
        public IThunderShotComposer Thunder { get; } = thunder;
        public IDustEffectComposer DustEffect { get; } = dustEffect;
        public ISmokyShotComposer Smoky { get; } = smoky;
    }
}
