namespace TextureSwapper.Core.Interfaces
{
    public interface IShotEffectComposerRegistry
    {
        IFirebirdShotComposer Firebird { get; }
        IFreezeShotComposer Freeze { get; }
        IRicochetShotComposer Ricochet { get; }
        ITwinsShotComposer Twins { get; }
        IRailgunShotComposer Railgun { get; }
        IHammerShotComposer Hammer { get; }
        IIsidaShotComposer Isida { get; }
        IShaftShotComposer Shaft { get; }
        IVulcanShotComposer Vulcan { get; }
        IThunderShotComposer Thunder { get; }
        IDustEffectComposer DustEffect { get; }
        ISmokyShotComposer Smoky { get; }
    }
}
