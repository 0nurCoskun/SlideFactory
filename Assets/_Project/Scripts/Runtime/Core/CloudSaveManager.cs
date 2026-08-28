using System;
using System.Collections.Generic;
using System.Text;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using UnityEngine;

/// <summary>
/// Play Oyun Hizmetleri "Kaydedilmiş Oyunlar" (Saved Games / cloud save) entegrasyonu.
/// Level ilerlemesini (tamamlanma + yıldız + en iyi skor) PlayerPrefs'in üzerine EK bir
/// katman olarak oyuncunun Google hesabına yedekler, böylece cihaz değiştirince veya
/// uygulama yeniden yüklenince ilerleme geri yüklenebilir. LeaderboardManager ile aynı
/// desen: singleton + DontDestroyOnLoad, "best effort" - hata/oturum yoksa sessizce yutulur;
/// PlayerPrefs (LevelProgress/ScoreProgress) her zaman tek gerçek kaynak (source of truth)
/// olmaya devam eder, bulut sadece bunun bir yedeği/senkron kanalıdır.
///
/// LeaderboardManager.OnSignInFinished'a abone olur; kendi PlayGamesPlatform.Activate()/
/// Authenticate() çağrısı YAPMAZ - o LeaderboardManager'ın sorumluluğu, burada tekrarlamak
/// çifte Activate/Authenticate çağrısına yol açardı.
///
/// Senkron her zaman İKİ YÖNLÜ ve SADECE YÜKSELTİCİ çalışır: önce buluttaki değerler
/// LevelProgress/SetStarsIfHigher ve ScoreProgress/SetBestScoreIfHigher'ın "if higher"
/// kurallarıyla yerel kayda uygulanır (hiçbir alan düşürülmez), sonra yerelin GÜNCEL hali
/// (artık iki cihazın en iyisini içeren birleşim) tekrar buluta yazılır. Böylece iki cihaz
/// arasında ilerleme kaybı olmaz, hangi cihaz önce senkronlarsa onun avantajı da olmaz.
/// </summary>
public class CloudSaveManager : MonoBehaviour
{
    private const string SaveFileName = "cardcraft_progress";

    [Serializable]
    private class LevelEntry
    {
        public string levelId;
        public bool completed;
        public int stars;
        public int bestScore;
    }

    [Serializable]
    private class CloudData
    {
        public int version = 1;
        public List<LevelEntry> levels = new List<LevelEntry>();
        public string lastPlayedLevelId;
    }

    [Tooltip("Senkronize edilecek TÜM level'ları numaralandırmak için - Level Select'in " +
             "kullandığı aynı katalog asset'i atanmalı.")]
    [SerializeField] private LevelCatalog levelCatalog;

    public static CloudSaveManager Instance { get; private set; }

    private bool _syncInFlight;
    private bool _pendingResync;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (levelCatalog == null)
            Debug.LogError("[CloudSaveManager] levelCatalog atanmamış, bulut senkronizasyonu çalışmayacak.");
    }

    private void Start()
    {
        // Awake sırası GameObject'ler arasında garanti değil, o yüzden LeaderboardManager.Instance'a
        // güvenli erişim için Start'ı bekliyoruz - bu noktada tüm Awake'ler tamamlanmış olur.
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnSignInFinished += HandleSignInFinished;

            if (LeaderboardManager.Instance.IsAuthenticated)
                HandleSignInFinished(true);
        }
    }

    private void OnDestroy()
    {
        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.OnSignInFinished -= HandleSignInFinished;
    }

    private void HandleSignInFinished(bool success)
    {
        if (success) SyncNow();
    }

    /// <summary>
    /// GameManager, bir level tamamlandığında (LevelProgress/ScoreProgress'e yazıldıktan
    /// HEMEN sonra) bunu çağırır. Giriş yapılmamışsa ya da katalog atanmamışsa sessizce
    /// çıkar - level akışını asla bloklamaz. Zaten bir senkron sürüyorsa isteği kuyruğa
    /// almak yerine "bitince bir kez daha çalıştır" bayrağı bırakır, GPGS istemcisini
    /// çakışan eşzamanlı çağrılarla boğmamak için.
    /// </summary>
    public void SyncNow()
    {
        if (levelCatalog == null) return;
        if (LeaderboardManager.Instance == null || !LeaderboardManager.Instance.IsAuthenticated) return;

        if (_syncInFlight)
        {
            _pendingResync = true;
            return;
        }

        _syncInFlight = true;
        PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(
            SaveFileName,
            DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseLongestPlaytime,
            OnSavedGameOpened);
    }

    private void OnSavedGameOpened(SavedGameRequestStatus status, ISavedGameMetadata metadata)
    {
        if (status != SavedGameRequestStatus.Success)
        {
            Debug.Log($"[CloudSaveManager] Kaydedilmiş oyun açılamadı: {status}");
            FinishSync();
            return;
        }

        PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(metadata,
            (readStatus, data) => OnSavedGameRead(readStatus, data, metadata));
    }

    private void OnSavedGameRead(SavedGameRequestStatus status, byte[] data, ISavedGameMetadata metadata)
    {
        if (status != SavedGameRequestStatus.Success)
        {
            Debug.Log($"[CloudSaveManager] Kaydedilmiş oyun okunamadı: {status}");
            FinishSync();
            return;
        }

        MergeCloudIntoLocal(DeserializeOrEmpty(data));

        string json = JsonUtility.ToJson(BuildSnapshotFromLocal());
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        SavedGameMetadataUpdate update = new SavedGameMetadataUpdate.Builder().Build();

        PlayGamesPlatform.Instance.SavedGame.CommitUpdate(metadata, update, bytes, (commitStatus, _) =>
        {
            if (commitStatus != SavedGameRequestStatus.Success)
                Debug.Log($"[CloudSaveManager] Bulut kaydı gönderilemedi: {commitStatus}");
            FinishSync();
        });
    }

    private void FinishSync()
    {
        _syncInFlight = false;
        if (_pendingResync)
        {
            _pendingResync = false;
            SyncNow();
        }
    }

    /// <summary>
    /// Buluttaki değerleri yerel PlayerPrefs'e uygular - SADECE YÜKSELTİR, hiçbir alanı
    /// düşürmez (LevelProgress/ScoreProgress'in kendi "if higher" kurallarını kullanır).
    /// </summary>
    private void MergeCloudIntoLocal(CloudData cloudData)
    {
        if (cloudData?.levels == null) return;

        foreach (LevelEntry entry in cloudData.levels)
        {
            if (entry == null || string.IsNullOrEmpty(entry.levelId)) continue;
            if (!levelCatalog.TryGetIndexById(entry.levelId, out int index)) continue;

            LevelData level = levelCatalog.GetLevelAtIndex(index);
            if (level == null) continue;

            if (entry.completed)
                LevelProgress.MarkLevelCompleted(level);

            if (entry.stars > 0)
                LevelProgress.SetStarsIfHigher(level, entry.stars);

            if (entry.bestScore > 0)
                ScoreProgress.SetBestScoreIfHigher(level, entry.bestScore);
        }
    }

    /// <summary>Katalogdaki tüm level'lar için yerel PlayerPrefs'in GÜNCEL halinden bir anlık görüntü kurar.</summary>
    private CloudData BuildSnapshotFromLocal()
    {
        var data = new CloudData { lastPlayedLevelId = LevelProgress.GetLastPlayedLevelId() };

        levelCatalog.EnsureBuilt();
        foreach (LevelData level in levelCatalog.Levels)
        {
            bool completed = LevelProgress.IsLevelCompleted(level);
            int stars = LevelProgress.GetStars(level);
            int bestScore = ScoreProgress.GetBestScore(level);

            if (!completed && stars == 0 && bestScore == 0) continue; // hiç oynanmamış, atla

            data.levels.Add(new LevelEntry
            {
                levelId = LevelProgress.GetLevelIdentifier(level),
                completed = completed,
                stars = stars,
                bestScore = bestScore
            });
        }

        return data;
    }

    private static CloudData DeserializeOrEmpty(byte[] data)
    {
        if (data == null || data.Length == 0) return new CloudData();

        try
        {
            CloudData parsed = JsonUtility.FromJson<CloudData>(Encoding.UTF8.GetString(data));
            return parsed ?? new CloudData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudSaveManager] Bulut verisi parse edilemedi, boş kabul ediliyor: {e.Message}");
            return new CloudData();
        }
    }
}
