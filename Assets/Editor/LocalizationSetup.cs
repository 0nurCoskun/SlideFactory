using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Tek seferlik editör kurulum script'i - Türkçe/İngilizce Locale asset'lerini ve
/// CardNames/StationNames/LevelNames/UI String Table Collection'larını oluşturur,
/// Assets/_Project/Localization/*.json dosyalarındaki çevirilerle doldurur.
///
/// Unity Editor içinden "CardCraft/Localization/Setup Locales And Tables" menüsünden
/// çalıştırılmalı. Idempotent'tir - tekrar çalıştırmak var olan tabloları günceller,
/// kopya oluşturmaz.
/// </summary>
public static class LocalizationSetup
{
    private const string LocalesDirectory = "Assets/_Project/Localization/Locales";
    private const string TablesDirectory = "Assets/_Project/Localization/Tables";
    private const string DataDirectory = "Assets/_Project/Localization";

    [MenuItem("CardCraft/Localization/Setup Locales And Tables")]
    public static void SetupAll()
    {
        EnsureDirectory(LocalesDirectory);
        EnsureDirectory(TablesDirectory);

        EnsureLocalizationSettings();

        Locale english = EnsureLocale("en", SystemLanguage.English);
        Locale turkish = EnsureLocale("tr", SystemLanguage.Turkish);
        var locales = new List<Locale> { english, turkish };

        StringTableCollection cardNames = EnsureStringTableCollection("CardNames", locales);
        StringTableCollection stationNames = EnsureStringTableCollection("StationNames", locales);
        StringTableCollection levelNames = EnsureStringTableCollection("LevelNames", locales);
        StringTableCollection ui = EnsureStringTableCollection("UI", locales);

        int cardCount = PopulateFromCardData(cardNames, english, turkish);
        int stationCount = PopulateFromStationData(stationNames, english, turkish);
        int levelCount = PopulateFromLevelData(levelNames, english, turkish);
        int uiCount = PopulateUiTable(ui, english, turkish);

        // KRİTİK: AddEntry() yeni bir anahtar eklerken id'yi SharedTableData'ya yazar,
        // ama SharedTableData'yı dirty İŞARETLEMEZSE SaveAssets() bu değişikliği diske
        // yazmaz. Sonraki Editor oturumunda SharedTableData eski haline (yeni anahtar
        // YOK) döner, halbuki UI_en/UI_tr tabloları o anahtarın id'sini hâlâ taşır -
        // GetLocalizedString id'yi SharedTableData'da bulamayıp "No translation found"
        // uyarısı basar. Bu script her çalıştırıldığında da aynı anahtar için YENİ bir
        // id üretilip tekrar kaybolur, tabloya sürekli yetim (orphan) satır birikir.
        EditorUtility.SetDirty(cardNames.SharedData);
        EditorUtility.SetDirty(stationNames.SharedData);
        EditorUtility.SetDirty(levelNames.SharedData);
        EditorUtility.SetDirty(ui.SharedData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LocalizationSetup] Done. Cards: {cardCount}, Stations: {stationCount}, Levels: {levelCount}, UI strings: {uiCount}.");
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    // "There is no active LocalizationSettings" hatası, projede hiç aktif bir
    // LocalizationSettings asset'i olmadığında (Edit > Project Settings > Localization
    // boşsa) fırlatılır. Locale/Table oluşturmak bu asset'i OTOMATİK yaratmıyor -
    // bu yüzden burada elle oluşturup LocalizationEditorSettings.ActiveLocalizationSettings
    // ile projeye aktif olarak bağlıyoruz.
    private static void EnsureLocalizationSettings()
    {
        if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;

        var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
        string path = $"{DataDirectory}/LocalizationSettings.asset";
        AssetDatabase.CreateAsset(settings, path);
        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
    }

    private static Locale EnsureLocale(string code, SystemLanguage systemLanguage)
    {
        foreach (Locale existing in LocalizationEditorSettings.GetLocales())
        {
            if (existing.Identifier.Code == code) return existing;
        }

        Locale locale = Locale.CreateLocale(systemLanguage);
        string path = $"{LocalesDirectory}/Locale-{code}.asset";
        AssetDatabase.CreateAsset(locale, path);
        LocalizationEditorSettings.AddLocale(locale);
        return locale;
    }

    private static StringTableCollection EnsureStringTableCollection(string tableName, List<Locale> locales)
    {
        StringTableCollection existing = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (existing != null) return existing;

        return LocalizationEditorSettings.CreateStringTableCollection(tableName, TablesDirectory, locales);
    }

    private static int PopulateFromCardData(StringTableCollection collection, Locale en, Locale tr)
    {
        Dictionary<string, string> turkish = LoadJsonDict($"{DataDirectory}/CardNames.json");
        StringTable enTable = collection.GetTable(en.Identifier) as StringTable;
        StringTable trTable = collection.GetTable(tr.Identifier) as StringTable;

        var seen = new HashSet<string>();
        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null || string.IsNullOrEmpty(card.cardId) || !seen.Add(card.cardId)) continue;

            enTable.AddEntry(card.cardId, card.displayName);
            trTable.AddEntry(card.cardId, turkish.TryGetValue(card.cardId, out string t) ? t : card.displayName);
            count++;
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(trTable);
        return count;
    }

    private static int PopulateFromStationData(StringTableCollection collection, Locale en, Locale tr)
    {
        Dictionary<string, string> turkish = LoadJsonDict($"{DataDirectory}/StationNames.json");
        StringTable enTable = collection.GetTable(en.Identifier) as StringTable;
        StringTable trTable = collection.GetTable(tr.Identifier) as StringTable;

        var seen = new HashSet<string>();
        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:StationData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StationData station = AssetDatabase.LoadAssetAtPath<StationData>(path);
            if (station == null || string.IsNullOrEmpty(station.stationId) || !seen.Add(station.stationId)) continue;

            enTable.AddEntry(station.stationId, station.displayName);
            trTable.AddEntry(station.stationId, turkish.TryGetValue(station.stationId, out string t) ? t : station.displayName);
            count++;
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(trTable);
        return count;
    }

    private static int PopulateFromLevelData(StringTableCollection collection, Locale en, Locale tr)
    {
        Dictionary<string, string> turkish = LoadJsonDict($"{DataDirectory}/LevelNames.json");
        StringTable enTable = collection.GetTable(en.Identifier) as StringTable;
        StringTable trTable = collection.GetTable(tr.Identifier) as StringTable;

        var seen = new HashSet<string>();
        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null || string.IsNullOrEmpty(level.levelId) || !seen.Add(level.levelId)) continue;

            enTable.AddEntry(level.levelId, level.displayName);
            trTable.AddEntry(level.levelId, turkish.TryGetValue(level.levelId, out string t) ? t : level.displayName);
            count++;
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(trTable);
        return count;
    }

    private static int PopulateUiTable(StringTableCollection collection, Locale en, Locale tr)
    {
        string json = File.ReadAllText($"{DataDirectory}/UIStrings.json");
        var entries = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
        StringTable enTable = collection.GetTable(en.Identifier) as StringTable;
        StringTable trTable = collection.GetTable(tr.Identifier) as StringTable;

        foreach (var kvp in entries)
        {
            enTable.AddEntry(kvp.Key, kvp.Value["en"]);
            trTable.AddEntry(kvp.Key, kvp.Value["tr"]);
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(trTable);
        return entries.Count;
    }

    private static Dictionary<string, string> LoadJsonDict(string path)
    {
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
    }
}
