using UnityEngine;

/// <summary>
/// Her level'da elde edilen EN YÜKSEK puanı PlayerPrefs üzerinden KALICI olarak
/// takip eder. LevelProgress'in yıldız/tamamlanma kaydıyla birebir aynı deseni
/// izler - tek farkı sakladığı şeyin yıldız değil PUAN olması.
///
/// Neden ayrı bir sınıf: LevelProgress "bu level açıldı mı / kaç yıldız aldı"
/// sorusunun sahibi, burası ise combo sisteminin ürettiği skorun sahibi. İkisini
/// tek dosyada birleştirmek LevelProgress'i iki sorumluluğa sokardı.
///
/// ÖNEMLİ: Kaybedilen bir denemede buraya HİÇBİR ŞEY yazılmaz - kaydedilen puan
/// her zaman "TAMAMLANMIŞ en iyi koşu" anlamına gelir (bkz. GameManager.EndLevel
/// içindeki persist parametresi).
/// </summary>
public static class ScoreProgress
{
    // LevelProgress'teki "level_stars_" / "level_stars_pending_reveal_" ikilisinde
    // biri diğerinin ön eki. O tuzağı burada TEKRARLAMA: bu ön ekin altına ikinci
    // bir "level_best_score_xxx_" ön eki daha eklenmemeli.
    private const string BestScoreKeyPrefix = "level_best_score_";

    /// <summary>Bu level'da şimdiye kadar alınan en yüksek puan. Hiç oynanmamışsa 0.</summary>
    public static int GetBestScore(LevelData level)
    {
        if (level == null) return 0;
        return PlayerPrefs.GetInt(BestScoreKeyPrefix + LevelProgress.GetLevelIdentifier(level), 0);
    }

    /// <summary>
    /// Yeni puan, önceki EN YÜKSEK puandan fazlaysa kaydeder. Böylece oyuncu bir
    /// level'ı daha kötü bir skorla tekrar oynarsa rekorunu KAYBETMEZ.
    ///
    /// Kimlik çözümü için LevelProgress.GetLevelIdentifier kullanılıyor - o metod
    /// tam olarak bu sebeple public (kendi XML dokümantasyonunda yazıyor). Aynı
    /// kuralı burada tekrar yazmak, ileride levelId kuralı değişirse iki yerin
    /// birbirinden sapmasına yol açardı.
    /// </summary>
    /// <returns>Puan gerçekten yükseldiyse (yeni rekor) true, yoksa false.</returns>
    public static bool SetBestScoreIfHigher(LevelData level, int score)
    {
        if (level == null) return false;

        if (score > GetBestScore(level))
        {
            PlayerPrefs.SetInt(BestScoreKeyPrefix + LevelProgress.GetLevelIdentifier(level), score);
            PlayerPrefs.Save();
            return true;
        }

        return false;
    }

    // NOT: Buraya bilerek bir "ResetAllScores" eklenmedi.
    // LevelProgress.ResetAllProgress() zaten PlayerPrefs.DeleteAll() kullandığı
    // için puan kayıtları da kendiliğinden siliniyor.
}
