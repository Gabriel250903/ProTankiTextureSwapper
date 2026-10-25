using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
