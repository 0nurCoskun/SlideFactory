using UnityEngine;

/// <summary>
/// Hangi level'ların tamamlandığını PlayerPrefs üzerinden KALICI olarak
/// (uygulama kapansa/silinse bile - PlayerPrefs cihazda kalır) takip eder.
/// LevelButton bunu kullanarak bir level'ın açık mı kilitli mi olduğuna karar verir.
/// </summary>
public static class LevelProgress
{
    private const string CompletedKeyPrefix = "level_completed_";

    /// <summary>Her LevelData'nın kayıt için kullanacağı benzersiz anahtar. levelId boşsa asset ismini kullanır.</summary>
    private static string GetKey(LevelData level)
    {
        string id = !string.IsNullOrEmpty(level.levelId) ? level.levelId : level.name;
        return CompletedKeyPrefix + id;
    }

    public static bool IsLevelCompleted(LevelData level)
    {
        if (level == null) return false;
        return PlayerPrefs.GetInt(GetKey(level), 0) == 1;
    }

    public static void MarkLevelCompleted(LevelData level)
    {
        if (level == null) return;
        PlayerPrefs.SetInt(GetKey(level), 1);
        PlayerPrefs.Save();
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

    /// <summary>Test/debug amaçlı - tüm ilerlemeyi sıfırlar.</summary>
    public static void ResetAllProgress()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
