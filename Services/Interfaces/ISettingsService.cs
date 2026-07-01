using TextureSwapper.Models;

namespace TextureSwapper.Services.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
