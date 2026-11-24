namespace TextureSwapper.Core.Models
{
    public sealed class EquipmentTierTarget
    {
        public long? ResourceId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class EquipmentTargetsCatalog
    {
        public Dictionary<string, Dictionary<string, EquipmentTierTarget>> Turrets { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, EquipmentTierTarget>> Hulls { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public enum SkinTierResolutionMode
    {
        NativeM3,
        NativeTier,
        CustomModelFallback,
        BaseM3ModelFallback,
        MissingTargetId,
        MissingBaseModel,
        Unsupported,
    }

    public sealed record SkinTierResolution(
        SkinTierResolutionMode Mode,
        bool IsAvailable,
        bool UsesM3ModelFallback,
        string? UnavailableReason,
        long? TargetResourceId,
        SkinModel? ResolvedSkin
    );
}
