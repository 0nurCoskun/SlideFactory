using UnityEngine.Localization.Settings;

/// <summary>
/// CardData/StationData/LevelData'nın displayName'i yerine, LocalizationSetup editör
/// script'inin oluşturduğu String Table'lardan (CardNames/StationNames/LevelNames/UI)
/// mevcut dile göre metin çeken tek merkezi yardımcı. Tablo satırı bulunamazsa
/// (ör. henüz "CardCraft/Localization/Setup Locales And Tables" çalıştırılmadıysa)
/// data asset'indeki İngilizce displayName'e düşer - oyun asla boş metinle kalmaz.
/// </summary>
public static class GameLocalization
{
    public const string CardNamesTable = "CardNames";
    public const string StationNamesTable = "StationNames";
    public const string LevelNamesTable = "LevelNames";
    public const string UITable = "UI";

    public static string GetCardName(CardData data)
    {
        if (data == null) return string.Empty;
        return GetOrFallback(CardNamesTable, data.cardId, data.displayName);
    }

    public static string GetStationName(StationData data)
    {
        if (data == null) return string.Empty;
        return GetOrFallback(StationNamesTable, data.stationId, data.displayName);
    }

    public static string GetLevelName(LevelData data)
    {
        if (data == null) return string.Empty;
        return GetOrFallback(LevelNamesTable, data.levelId, data.displayName);
    }

    public static string GetUIString(string key, params object[] arguments)
    {
        // LocalizationSettings henüz initialize olmadan GetLocalizedString çağırmak,
        // Addressables'ın senkron WaitForCompletion çağrısını kendi async callback
        // zincirinin İÇİNDEN tetiklemesine ve "Reentering the Update method is not
        // allowed" exception'ına yol açabiliyor. Init bitene kadar İngilizce anahtarı
        // döndürüyoruz - LocalizationManager, init tamamlanınca OnLanguageChanged'i
        // tetikleyip tüm View'ları zaten yeniden çiziyor.
        if (!IsInitializationReady()) return key;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(UITable, key, arguments);
        return string.IsNullOrEmpty(localized) ? key : localized;
    }

    private static string GetOrFallback(string table, string key, string fallback)
    {
        if (string.IsNullOrEmpty(key) || !IsInitializationReady()) return fallback;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    private static bool IsInitializationReady()
    {
        var operation = LocalizationSettings.InitializationOperation;
        return operation.IsValid() && operation.IsDone;
    }
}
