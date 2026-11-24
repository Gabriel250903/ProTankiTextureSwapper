using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IEquipmentTargetService
    {
        void Reload();
        bool TryGetResourceId(string category, string itemName, string tier, out long resourceId);
        int GetMappedCount(string tier);
        int GetTotalEquipmentCount();
        bool HasAnyMappedTargets(string tier);
        string? ResolveBaseM3ModelPath(string category, string itemName);
        SkinTierResolution ResolveSkinForTier(SkinModel skin, string targetTier);
    }
}
