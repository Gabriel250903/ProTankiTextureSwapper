using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        IReadOnlyList<LanguageInfo> SupportedLanguages { get; }
        void SetLanguage(string languageCode);
        string GetString(string key);
        string GetString(string key, params object[] args);
        string this[string key] { get; }
        event EventHandler<string>? LanguageChanged;
    }
}
