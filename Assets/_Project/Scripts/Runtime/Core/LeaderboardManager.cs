using System;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

/// <summary>
/// Google Play Games Services (Play Console'daki "Play Oyun Hizmetleri") ile giriş ve
/// skor tablosu gönderimini yönetir. AudioManager/SceneFader ile aynı desen: singleton +
/// DontDestroyOnLoad, sahnede tek bir GameObject'e ("_LeaderboardManager") eklenir.
///
/// GPGS girişi ANONİM DEĞİL - oyuncunun gerçek Google hesabıyla oluyor ve görünen ad
/// otomatik Play Games profilinden geliyor. Ayrı bir takma ad / nickname akışına gerek yok.
///
/// Her level'ın hangi skor tablosuna gideceği LevelData.leaderboardId alanından okunur
/// (boşsa - örn. tutorial - hiçbir şey gönderilmez). Ağ hatası/oturum açık olmaması gibi
/// durumlar sessizce yutulur; ScoreProgress zaten skoru kalıcı olarak PlayerPrefs'te
/// tutuyor, leaderboard gönderimi bunun üzerine ekstra bir "best effort" katman.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    /// <summary>Giriş denemesi tamamlandığında (başarılı ya da başarısız) tetiklenir.</summary>
    public event Action<bool> OnSignInFinished;

    public bool IsAuthenticated => PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PlayGamesPlatform.Activate();
    }

    private void Start()
    {
        SignIn();
    }

    /// <summary>
    /// Play Games girişini başlatır. Oyuncu daha önce izin verdiyse sessizce (arka planda)
    /// tamamlanır; ilk kullanımda sistem bir hesap seçim ekranı gösterebilir.
    /// </summary>
    public void SignIn()
    {
        PlayGamesPlatform.Instance.Authenticate(OnAuthenticationFinished);
    }

    private void OnAuthenticationFinished(SignInStatus status)
    {
        bool success = status == SignInStatus.Success;
        if (!success)
        {
            Debug.Log($"[LeaderboardManager] Giriş başarısız: {status}");
        }
        OnSignInFinished?.Invoke(success);
    }

    /// <summary>
    /// Bu level için skor tablosuna skoru gönderir. level.leaderboardId boşsa (tutorial gibi)
    /// ya da oyuncu giriş yapmamışsa hiçbir şey yapmaz - level bitişini bloklamaz.
    /// </summary>
    public void SubmitScore(LevelData level, int score)
    {
        if (level == null || string.IsNullOrEmpty(level.leaderboardId)) return;

        if (!IsAuthenticated)
        {
            Debug.Log($"[LeaderboardManager] Giriş yapılmamış, skor gönderilmedi: {level.leaderboardId}");
            return;
        }

        // Social.ReportScore yerine bilerek PlayGamesPlatform.Instance.ReportScore kullanılıyor -
        // Unity'nin ISocialPlatform/Social API'si (UnityEngine.SocialPlatforms) obsolete işaretli.
        PlayGamesPlatform.Instance.ReportScore(score, level.leaderboardId, success =>
        {
            if (!success)
                Debug.Log($"[LeaderboardManager] Skor gönderimi başarısız: {level.leaderboardId}");
        });
    }

    /// <summary>Bu level'ın skor tablosunu Play Games'in kendi native UI'ında açar.</summary>
    public void ShowLeaderboardUI(LevelData level)
    {
        if (level == null || string.IsNullOrEmpty(level.leaderboardId)) return;
        if (!IsAuthenticated) return;

        PlayGamesPlatform.Instance.ShowLeaderboardUI(level.leaderboardId);
    }
}
