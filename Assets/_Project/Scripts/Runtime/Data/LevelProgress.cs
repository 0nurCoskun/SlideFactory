using UnityEngine;

/// <summary>
/// Hangi level'ların tamamlandığını ve kaç yıldızla tamamlandığını PlayerPrefs
/// üzerinden KALICI olarak (uygulama kapansa/silinse bile - PlayerPrefs cihazda
/// kalır) takip eder. LevelButton bunu kullanarak bir level'ın açık mı kilitli
/// mi olduğuna karar verir, ayrıca en yüksek yıldız skorunu da gösterebilir.
/// </summary>
public static class LevelProgress
{
    private const string CompletedKeyPrefix = "level_completed_";
    private const string StarsKeyPrefix = "level_stars_";

    /// <summary>Her LevelData'nın kayıt için kullanacağı benzersiz kimlik. levelId boşsa asset ismini kullanır.</summary>
    private static string GetLevelIdentifier(LevelData level)
    {
        return !string.IsNullOrEmpty(level.levelId) ? level.levelId : level.name;
    }

    public static bool IsLevelCompleted(LevelData level)
    {
        if (level == null) return false;
        return PlayerPrefs.GetInt(CompletedKeyPrefix + GetLevelIdentifier(level), 0) == 1;
    }

    public static void MarkLevelCompleted(LevelData level)
    {
        if (level == null) return;
        PlayerPrefs.SetInt(CompletedKeyPrefix + GetLevelIdentifier(level), 1);
        PlayerPrefs.Save();
    }

    /// <summary>Bu level'da şimdiye kadar kazanılan EN YÜKSEK yıldız sayısını döner (0-3).</summary>
    public static int GetStars(LevelData level)
    {
        if (level == null) return 0;
        return PlayerPrefs.GetInt(StarsKeyPrefix + GetLevelIdentifier(level), 0);
    }

    /// <summary>
    /// Yeni kazanılan yıldız sayısı, önceki EN YÜKSEK skordan fazlaysa kaydeder.
    /// Böylece oyuncu bir level'ı daha düşük yıldızla tekrar oynarsa, önceki
    /// yüksek skoru KAYBETMEZ.
    /// </summary>
    public static void SetStarsIfHigher(LevelData level, int stars)
    {
        if (level == null) return;

        int current = GetStars(level);
        if (stars > current)
        {
            PlayerPrefs.SetInt(StarsKeyPrefix + GetLevelIdentifier(level), stars);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Bir level'ın oynanabilir olup olmadığını hesaplar. requiredPreviousLevel boşsa
    /// (genelde ilk level) her zaman true döner. Değilse, o önceki level'ın
    /// tamamlanmış olması gerekir.
    /// </summary>
    public static bool IsLevelUnlocked(LevelData level)
    {
        if (level == null) return false;
        if (level.requiredPreviousLevel == null) return true;
        return IsLevelCompleted(level.requiredPreviousLevel);
    }

    /// <summary>Test/debug amaçlı - tüm ilerlemeyi (tamamlama + yıldız kayıtları dahil) sıfırlar.</summary>
    public static void ResetAllProgress()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}