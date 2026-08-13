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
    private const string PendingStarRevealKeyPrefix = "level_stars_pending_reveal_";
    private const string LastPlayedLevelKey = "level_last_played_id";
    private const string UnlockAllLevelsKey = "debug_unlock_all_levels";

    /// <summary>
    /// Her LevelData'nın kayıt için kullanacağı benzersiz kimlik. levelId boşsa asset ismini kullanır.
    /// LevelCatalog da "en son oynanan level" kaydını çözmek için AYNI kuralı kullanmak
    /// zorunda olduğu için public.
    /// </summary>
    public static string GetLevelIdentifier(LevelData level)
    {
        if (level == null) return string.Empty;
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
    /// yüksek skoru KAYBETMEZ. Skor GERÇEKTEN yükseldiyse (yeni bir "highscore"),
    /// bunu bir de "pending reveal" olarak işaretler - LevelButton, Level Select
    /// ekranına dönüldüğünde bunu görüp yıldızları BİR KERELİĞİNE animasyonla gösterir.
    /// </summary>
    /// <returns>Skor gerçekten yükseldiyse true, yoksa false.</returns>
    public static bool SetStarsIfHigher(LevelData level, int stars)
    {
        if (level == null) return false;

        int current = GetStars(level);
        if (stars > current)
        {
            string identifier = GetLevelIdentifier(level);
            PlayerPrefs.SetInt(StarsKeyPrefix + identifier, stars);
            PlayerPrefs.SetInt(PendingStarRevealKeyPrefix + identifier, 1);
            PlayerPrefs.Save();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Bu level için henüz Level Select ekranında "gösterilmemiş" (animasyonla
    /// açıklanmamış) yeni bir yıldız rekoru var mı? LevelButton bunu OnEnable'da
    /// kontrol eder.
    /// </summary>
    public static bool HasPendingStarReveal(LevelData level)
    {
        if (level == null) return false;
        return PlayerPrefs.GetInt(PendingStarRevealKeyPrefix + GetLevelIdentifier(level), 0) == 1;
    }

    /// <summary>
    /// LevelButton, yeni yıldız animasyonunu oynattıktan sonra bunu çağırır -
    /// böylece aynı rekor bir daha asla tekrar animasyonla gösterilmez.
    /// </summary>
    public static void ClearPendingStarReveal(LevelData level)
    {
        if (level == null) return;
        PlayerPrefs.DeleteKey(PendingStarRevealKeyPrefix + GetLevelIdentifier(level));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Bir level'ın oynanabilir olup olmadığını hesaplar. Önce debug "tümünü aç"
    /// bayrağına bakar - o açıksa tamamlama kaydına hiç bakmadan true döner.
    /// Değilse: requiredPreviousLevel boşsa (genelde ilk level) her zaman true,
    /// aksi halde o önceki level'ın tamamlanmış olması gerekir.
    /// </summary>
    public static bool IsLevelUnlocked(LevelData level)
    {
        if (level == null) return false;
        if (PlayerPrefs.GetInt(UnlockAllLevelsKey, 0) == 1) return true;
        if (level.requiredPreviousLevel == null) return true;
        return IsLevelCompleted(level.requiredPreviousLevel);
    }

    /// <summary>
    /// Test/debug amaçlı - tamamlama/yıldız kayıtlarına DOKUNMADAN tüm level'ları
    /// kilitsiz say. IsLevelUnlocked bu bayrağı ResetAllProgress ile aynı
    /// PlayerPrefs.DeleteAll çağrısıyla otomatik temizlenir, ayrı bir "kilitle"
    /// fonksiyonuna gerek yok.
    /// </summary>
    public static void UnlockAllLevels()
    {
        PlayerPrefs.SetInt(UnlockAllLevelsKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Oyuncunun EN SON hangi level'ı açtığını kaydeder. GameManager, level sahnesi
    /// açılırken bunu çağırır - böylece Level Select ekranı bir dahaki açılışında
    /// oyuncuyu 1. sayfaya değil, kaldığı sayfaya götürebiliyor.
    ///
    /// Tutorial KAYDEDİLMEZ: katalogda yer almadığı için bir sayfaya karşılık gelmiyor,
    /// ayrıca ilerleme kaydı tutmama kuralıyla da tutarlı (bkz. GameManager'daki
    /// isTutorial kontrolü).
    /// </summary>
    public static void SetLastPlayedLevel(LevelData level)
    {
        if (level == null || level.isTutorial) return;

        PlayerPrefs.SetString(LastPlayedLevelKey, GetLevelIdentifier(level));
        PlayerPrefs.Save();
    }

    /// <summary>Hiç level oynanmamışsa boş string döner.</summary>
    public static string GetLastPlayedLevelId()
    {
        return PlayerPrefs.GetString(LastPlayedLevelKey, string.Empty);
    }

    /// <summary>
    /// Test/debug amaçlı - tüm ilerlemeyi (tamamlama + yıldız kayıtları dahil) sıfırlar.
    /// DeleteAll kullandığı için "en son oynanan level" kaydı da kendiliğinden silinir.
    /// </summary>
    public static void ResetAllProgress()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}