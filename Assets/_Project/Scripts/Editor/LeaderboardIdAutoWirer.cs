using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Google Play Console > Play Oyun Hizmetleri > Skor tabloları'nda elle oluşturulan 31
/// leaderboard'un ID'lerini, LevelData.levelId eşlemesi üzerinden ilgili LevelData
/// asset'lerine otomatik yazar. Sadece leaderboardId alanı boş olan asset'ler güncellenir,
/// mevcut bir değer asla üzerine yazılmaz - Console'da ID'ler değişirse elle temizleyip
/// tekrar çalıştırman gerekir.
///
/// ID eşlemesi aşağıda hardcoded - Console'da yeni level/leaderboard eklendikçe bu
/// tabloya elle satır eklenip tekrar çalıştırılmalı. Tutorial level (levelId: "tutorial")
/// bilerek bu tabloda YOK - CLAUDE.md kuralı gereği tutorial'lar skor tablosuna hiç
/// gönderilmiyor, leaderboardId'si boş kalmalı.
/// </summary>
public static class LeaderboardIdAutoWirer
{
    private const string LevelsRoot = "Assets/_Project/ScriptableObjects/Levels";

    private static readonly Dictionary<string, string> LeaderboardIdByLevelId = new()
    {
        { "level1k", "CgkI6fbRhMcNEAIQAQ" },
        { "level2k", "CgkI6fbRhMcNEAIQAg" },
        { "level3k", "CgkI6fbRhMcNEAIQAw" },
        { "level4k", "CgkI6fbRhMcNEAIQBA" },
        { "level5k", "CgkI6fbRhMcNEAIQBQ" },
        { "level6k", "CgkI6fbRhMcNEAIQBg" },
        { "level7k", "CgkI6fbRhMcNEAIQBw" },
        { "level8k", "CgkI6fbRhMcNEAIQCA" },
        { "level9k", "CgkI6fbRhMcNEAIQCQ" },
        { "level10k", "CgkI6fbRhMcNEAIQCg" },
        { "level11k", "CgkI6fbRhMcNEAIQCw" },
        { "level12k", "CgkI6fbRhMcNEAIQDA" },
        { "level13k", "CgkI6fbRhMcNEAIQDQ" },
        { "level14k", "CgkI6fbRhMcNEAIQDg" },
        { "level15k", "CgkI6fbRhMcNEAIQDw" },
        { "level16k", "CgkI6fbRhMcNEAIQEA" },
        { "level17k", "CgkI6fbRhMcNEAIQEQ" },
        { "level18k", "CgkI6fbRhMcNEAIQEg" },
        { "level19k", "CgkI6fbRhMcNEAIQEw" },
        { "level20k", "CgkI6fbRhMcNEAIQFA" },
        { "level1m", "CgkI6fbRhMcNEAIQFQ" },
        { "level2m", "CgkI6fbRhMcNEAIQFg" },
        { "level3m", "CgkI6fbRhMcNEAIQFw" },
        { "level4m", "CgkI6fbRhMcNEAIQGA" },
        { "level5m", "CgkI6fbRhMcNEAIQGQ" },
        { "level6m", "CgkI6fbRhMcNEAIQGg" },
        { "level7m", "CgkI6fbRhMcNEAIQGw" },
        { "level8m", "CgkI6fbRhMcNEAIQHA" },
        { "level9m", "CgkI6fbRhMcNEAIQHQ" },
        { "level10m", "CgkI6fbRhMcNEAIQHg" },
        { "level3a", "CgkI6fbRhMcNEAIQHw" },
    };

    [MenuItem("CardCraft/Auto-Wire Leaderboard IDs")]
    public static void WireAllFromMenu()
    {
        var levelGuids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelsRoot });
        int wired = 0;
        int alreadySet = 0;
        int noMatch = 0;

        foreach (var guid in levelGuids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
            if (level == null) continue;

            if (!string.IsNullOrEmpty(level.leaderboardId))
            {
                alreadySet++;
                continue;
            }

            if (string.IsNullOrEmpty(level.levelId) || !LeaderboardIdByLevelId.TryGetValue(level.levelId, out var leaderboardId))
            {
                if (!level.isTutorial)
                    Debug.LogWarning($"[LeaderboardIdAutoWirer] Eşleşme yok, levelId: '{level.levelId}' ({assetPath})");
                noMatch++;
                continue;
            }

            level.leaderboardId = leaderboardId;
            EditorUtility.SetDirty(level);
            wired++;
            Debug.Log($"[LeaderboardIdAutoWirer] {level.levelId} -> {leaderboardId}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[LeaderboardIdAutoWirer] Tamamlandı. Bağlanan: {wired}, zaten doluydu: {alreadySet}, eşleşme yok: {noMatch}");
    }
}
