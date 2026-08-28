using Firebase.Analytics;
using UnityEngine;

/// <summary>
/// GameManager'ın level yaşam döngüsü event'lerini dinleyip Firebase Analytics'e event
/// olarak yazar. ScoreManager gibi TEK YÖNLÜ bir dinleyicidir - GameManager bunun
/// varlığından haberdar değildir (Dependency Inversion, bkz. GameManager sınıf başı notu).
/// Ayrıca level başladığında Crashlytics'e "şu an hangi level oynanıyor" bilgisini custom
/// key olarak bırakır - bir crash geldiğinde raporda hangi level'da olunduğunu görmek için.
///
/// Event isimleri bilerek Firebase'in "reserved" oyun event sabitlerine (EventLevelStart vb.)
/// bağlanmadı - SDK'nın hangi sabiti hangi string'e eşlediği canlı bir Editor'da
/// doğrulanamadığı için düz string kullanıldı (level_start/level_complete/level_failed).
/// Bu event'ler Analytics > DebugView/Realtime'da bu isimlerle görünür.
///
/// Sahnede GameManager/ScoreManager ile aynı (HER ZAMAN AKTİF) objeye eklenmeli -
/// objesi kapalı başlarsa event'leri sessizce kaçırır.
/// </summary>
public class AnalyticsEventLogger : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] private GameManager gameManager;
    [Tooltip("Atanırsa level_complete/level_failed event'lerine skor da eklenir. Boş bırakılabilir.")]
    [SerializeField] private ScoreManager scoreManager;

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelBegun += HandleLevelBegun;
            gameManager.OnLevelWon += HandleLevelWon;
            gameManager.OnLevelFailed += HandleLevelFailed;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelBegun -= HandleLevelBegun;
            gameManager.OnLevelWon -= HandleLevelWon;
            gameManager.OnLevelFailed -= HandleLevelFailed;
        }
    }

    private void HandleLevelBegun()
    {
        string levelName = GetLevelName();

        FirebaseManager.LogEvent("level_start",
            new Parameter("level_name", levelName),
            new Parameter("is_tutorial", IsTutorial() ? 1L : 0L));

        FirebaseManager.SetCrashlyticsCustomKey("current_level", levelName);
    }

    private void HandleLevelWon(int stars)
    {
        FirebaseManager.LogEvent("level_complete",
            new Parameter("level_name", GetLevelName()),
            new Parameter("stars", (long)stars),
            new Parameter("score", (long)GetScore()),
            new Parameter("is_new_best", scoreManager != null && scoreManager.IsNewBestScore ? 1L : 0L));
    }

    private void HandleLevelFailed()
    {
        FirebaseManager.LogEvent("level_failed",
            new Parameter("level_name", GetLevelName()),
            new Parameter("score", (long)GetScore()));
    }

    private string GetLevelName()
    {
        LevelData level = gameManager != null ? gameManager.ActiveLevel : null;
        return level != null ? level.name : "unknown";
    }

    private bool IsTutorial()
    {
        LevelData level = gameManager != null ? gameManager.ActiveLevel : null;
        return level != null && level.isTutorial;
    }

    private int GetScore()
    {
        return scoreManager != null ? scoreManager.TotalScore : 0;
    }
}
