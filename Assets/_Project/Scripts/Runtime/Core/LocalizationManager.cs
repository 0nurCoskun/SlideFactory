using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Dil (Türkçe/İngilizce) seçimini yöneten tek merkez. AudioManager ile aynı deseni izler:
/// PlayerPrefs ile KALICI olarak saklanır, singleton + DontDestroyOnLoad.
///
/// Unity Localization paketi Addressables üzerinden ASENKRON başlar - bu yüzden Awake'te
/// direkt SelectedLocale atamak yerine LocalizationSettings.InitializationOperation'ın
/// bitmesini bekleriz (ApplySavedLocale). Bu tamamlanmadan önce View'lar varsayılan (İngilizce)
/// metinle açılabilir; OnLanguageChanged event'i tetiklenince kendilerini günceller.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public enum Language
    {
        English = 0,
        Turkish = 1
    }

    private const string LanguageKey = "settings_language";
    private const string EnglishLocaleCode = "en";
    private const string TurkishLocaleCode = "tr";

    public static LocalizationManager Instance { get; private set; }

    public delegate void LanguageChanged(Language language);
    public event LanguageChanged OnLanguageChanged;

    public Language CurrentLanguage { get; private set; } = Language.English;
    public bool IsReady { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentLanguage = (Language)PlayerPrefs.GetInt(LanguageKey, (int)Language.English);

        StartCoroutine(ApplySavedLocaleWhenReady());
    }

    private IEnumerator ApplySavedLocaleWhenReady()
    {
        yield return LocalizationSettings.InitializationOperation;

        ApplyLocale(CurrentLanguage);
        IsReady = true;

        // Init bitmeden önce GameLocalization çağrıları İngilizce fallback'e düşmüş
        // olabilir (bkz. GameLocalization.IsInitializationReady) - burada dili
        // "değişmemiş" olsa bile bildirerek tüm View'ların gerçek tabloyla
        // yeniden çizilmesini sağlıyoruz.
        OnLanguageChanged?.Invoke(CurrentLanguage);
    }

    /// <summary>Dili değiştirir, PlayerPrefs'e kaydeder ve tüm dinleyicileri (View'lar) bilgilendirir.</summary>
    public void SetLanguage(Language language)
    {
        CurrentLanguage = language;

        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.Save();

        ApplyLocale(language);

        OnLanguageChanged?.Invoke(CurrentLanguage);
    }

    private void ApplyLocale(Language language)
    {
        string localeCode = language == Language.Turkish ? TurkishLocaleCode : EnglishLocaleCode;
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);

        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
        }
    }
}
