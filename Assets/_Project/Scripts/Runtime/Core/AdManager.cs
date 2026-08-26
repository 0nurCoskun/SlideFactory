using System;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>
/// Reklam (ads mediation) sisteminin tek merkezi. AudioManager/SceneFader ile aynı desen:
/// DontDestroyOnLoad singleton, tek bir sahnede ("_AdManager" gibi) yaşar, ilk açılan sahne
/// hangisiyse o hayatta kalır.
///
/// Bu sınıf HANGİ reklam SDK'sının kullanıldığını dışarıya SIZDIRMAZ - GameManager/View
/// katmanı sadece ShowRewardedAd/NotifyLevelEnded çağırır ve event'leri dinler. İleride
/// Unity LevelPlay yerine başka bir mediation'a geçilse bile dışarıdaki hiçbir çağrı satırı
/// değişmeyecek şekilde tasarlandı (GameManager'ın oynanış kurallarını View katmanından
/// soyutlamasıyla birebir aynı gerekçe).
///
/// Rewarded/Interstitial ad NESNELERİ tek seferlik değil - bir kez oluşturulup Init
/// başarılı olduğunda LoadAd() ile doldurulur, gösterildikten SONRA yeniden LoadAd()
/// çağrılıp bir sonraki gösterime hazırlanır (LevelPlay reklamları "tek kullanımlık").
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Header("LevelPlay Uygulama Anahtarları")]
    [Tooltip("Play Console üzerinden LevelPlay panelinde oluşturulan Android App Key. " +
             "Gerçek App Key eklenene kadar test/örnek anahtar kullanılabilir.")]
    [SerializeField] private string androidAppKey = "";
    [Tooltip("iOS için LevelPlay App Key.")]
    [SerializeField] private string iosAppKey = "";

    [Header("Reklam Birimi (Ad Unit) ID'leri")]
    [Tooltip("LevelPlay panelinde tanımlanan Android Rewarded Video ad unit ID'si.")]
    [SerializeField] private string androidRewardedAdUnitId = "";
    [Tooltip("LevelPlay panelinde tanımlanan iOS Rewarded Video ad unit ID'si.")]
    [SerializeField] private string iosRewardedAdUnitId = "";
    [Tooltip("LevelPlay panelinde tanımlanan Android Interstitial ad unit ID'si.")]
    [SerializeField] private string androidInterstitialAdUnitId = "";
    [Tooltip("LevelPlay panelinde tanımlanan iOS Interstitial ad unit ID'si.")]
    [SerializeField] private string iosInterstitialAdUnitId = "";

    [Header("Interstitial Sıklık Sınırlaması")]
    [Tooltip("Bir interstitial gösterildikten sonra bir SONRAKİ interstitial için kaç level " +
             "daha bitmesi (kazanılsın/kaybedilsin fark etmez) gerektiği. Her level sonunda " +
             "reklam göstermek oyuncuyu yorar - bu sayı ile aralık açılır.")]
    [SerializeField] private int levelsBetweenInterstitials = 3;

    [Header("Debug")]
    [Tooltip("SDK/gösterim çağrılarını Console'a yazar - entegrasyonu test ederken işe yarar.")]
    [SerializeField] private bool verboseLogging = true;

    private LevelPlayRewardedAd _rewardedAd;
    private LevelPlayInterstitialAd _interstitialAd;

    private int _levelsSinceLastInterstitial;

    // ShowRewardedAd çağrısı ile OnAdRewarded/OnAdClosed event'leri arasındaki köprü:
    // reklam kapanana kadar "ödül verildi mi" bilgisini taşır. Aynı anda tek bir rewarded
    // reklam gösterilebileceği için tek bir bekleyen callback yeterli.
    private Action<bool> _pendingRewardedCallback;
    private bool _pendingRewardGranted;

    /// <summary>SDK başarıyla ilklendirildi mi.</summary>
    public bool IsSdkInitialized { get; private set; }

    /// <summary>Şu an gösterilmeye hazır bir rewarded reklam var mı.</summary>
    public bool IsRewardedAdReady { get; private set; }

    /// <summary>Şu an gösterilmeye hazır bir interstitial reklam var mı.</summary>
    public bool IsInterstitialReady { get; private set; }

    /// <summary>"Reklamları Kaldır" IAP'ı satın alınmış mı - MonetizationProgress'in ince bir kısayolu.</summary>
    public bool AdsRemoved => MonetizationProgress.AreAdsRemoved();

    public event Action OnSdkInitialized;
    public event Action<bool> OnRewardedAdReadyChanged;
    public event Action<bool> OnInterstitialReadyChanged;

    /// <summary>Interstitial reklam kapandığında bir kere tetiklenir.</summary>
    public event Action OnInterstitialClosed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Awake()'te bu bir kopyaysa Destroy(gameObject) çağrılmıştı, ama Destroy()
        // yıkımı bu frame'in SONUNA erteler - Start() yine de çalışır. Bu kontrol
        // olmazsa kopya, SDK'yı İKİNCİ KEZ ilklendirir (ör. MainMenu sahnesine her
        // dönüşte): LevelPlay.Init tekrar çağrılır ve OnInitSuccess/OnInitFailed'a
        // artık var olmayan bir instance için kalıcı bir abonelik eklenir.
        if (Instance != this) return;

        InitializeSdk();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        LevelPlay.OnInitSuccess -= HandleInitSuccess;
        LevelPlay.OnInitFailed -= HandleInitFailed;

        _rewardedAd?.Dispose();
        _interstitialAd?.Dispose();
    }

    /// <summary>
    /// Bir level bittiğinde (kazanıldı/kaybedildi fark etmez) View katmanı bunu çağırır.
    /// AdManager kendi içinde sıklık sınırlamasını uygular - çağıran taraf "her level
    /// bitiminde çağır, aşırı sıklığı ben engellerim" der. Bkz. AdTriggerView.
    /// </summary>
    public void NotifyLevelEnded()
    {
        ShowInterstitialIfDue();
    }

    /// <summary>
    /// Rewarded reklamı gösterir. onComplete, reklam kapandığında GERÇEKTEN ödül
    /// kazanılıp kazanılmadığıyla çağrılır (izlemeden kapatma = false). SDK hazır
    /// değilken GÜVENLİ TARAF seçilir: false döner, çağıran kod asla "ödül verildi"
    /// sanıp yanlışlıkla bonus vermez.
    /// </summary>
    public void ShowRewardedAd(Action<bool> onComplete)
    {
        if (AdsRemoved || _rewardedAd == null || !IsRewardedAdReady)
        {
            Log($"ShowRewardedAd reddedildi (adsRemoved={AdsRemoved}, ready={IsRewardedAdReady}).");
            onComplete?.Invoke(false);
            return;
        }

        _pendingRewardedCallback = onComplete;
        _pendingRewardGranted = false;
        _rewardedAd.ShowAd();
    }

    private void ShowInterstitialIfDue()
    {
        if (AdsRemoved) return;

        _levelsSinceLastInterstitial++;
        if (_levelsSinceLastInterstitial < levelsBetweenInterstitials) return;

        if (_interstitialAd == null || !IsInterstitialReady)
        {
            Log("Interstitial sırası geldi ama reklam hazır değil, atlanıyor.");
            return;
        }

        _levelsSinceLastInterstitial = 0;
        _interstitialAd.ShowAd();
    }

    private void InitializeSdk()
    {
        LevelPlay.OnInitSuccess += HandleInitSuccess;
        LevelPlay.OnInitFailed += HandleInitFailed;

        string appKey = IsAndroid ? androidAppKey : iosAppKey;
        if (string.IsNullOrEmpty(appKey))
        {
            Debug.LogWarning("[AdManager] App Key boş - LevelPlay ilklendirilmiyor. " +
                              "Inspector'dan androidAppKey/iosAppKey doldurulmalı.");
            return;
        }

        LevelPlay.Init(appKey);
    }

    private void HandleInitSuccess(LevelPlayConfiguration configuration)
    {
        IsSdkInitialized = true;
        Log("LevelPlay SDK ilklendirildi.");
        OnSdkInitialized?.Invoke();

        SetupRewardedAd();
        SetupInterstitialAd();
    }

    private void HandleInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[AdManager] LevelPlay init hatası: {error}");
    }

    /// <summary>
    /// LevelPlayRewardedAd/LevelPlayInterstitialAd zaten UNITY_ANDROID/UNITY_IOS derleme
    /// sembollerine göre doğru platform implementasyonunu seçiyor (bkz. paket kaynağı) -
    /// burada Application.platform kullanılması sadece HANGİ ad unit ID string'inin
    /// verileceğine karar vermek için, SDK'nın kendi platform seçimine karışmıyor.
    /// </summary>
    private static bool IsAndroid => Application.platform == RuntimePlatform.Android;

    private void SetupRewardedAd()
    {
        string adUnitId = IsAndroid ? androidRewardedAdUnitId : iosRewardedAdUnitId;
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("[AdManager] Rewarded ad unit ID boş - rewarded reklam yüklenmiyor.");
            return;
        }

        _rewardedAd = new LevelPlayRewardedAd(adUnitId);
        _rewardedAd.OnAdLoaded += HandleRewardedAdLoaded;
        _rewardedAd.OnAdLoadFailed += HandleRewardedAdLoadFailed;
        _rewardedAd.OnAdRewarded += HandleRewardedAdRewarded;
        _rewardedAd.OnAdDisplayFailed += HandleRewardedAdDisplayFailed;
        _rewardedAd.OnAdClosed += HandleRewardedAdClosed;

        _rewardedAd.LoadAd();
    }

    private void SetupInterstitialAd()
    {
        string adUnitId = IsAndroid ? androidInterstitialAdUnitId : iosInterstitialAdUnitId;
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("[AdManager] Interstitial ad unit ID boş - interstitial reklam yüklenmiyor.");
            return;
        }

        _interstitialAd = new LevelPlayInterstitialAd(adUnitId);
        _interstitialAd.OnAdLoaded += HandleInterstitialAdLoaded;
        _interstitialAd.OnAdLoadFailed += HandleInterstitialAdLoadFailed;
        _interstitialAd.OnAdClosed += HandleInterstitialAdClosed;

        _interstitialAd.LoadAd();
    }

    // --- Rewarded event handler'ları ---

    private void HandleRewardedAdLoaded(LevelPlayAdInfo info)
    {
        SetRewardedReady(true);
    }

    private void HandleRewardedAdLoadFailed(LevelPlayAdError error)
    {
        SetRewardedReady(false);
        Log($"Rewarded reklam yüklenemedi: {error}");
    }

    private void HandleRewardedAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        // Reklam KAPANMADAN önce gelir - gerçek "ödülü ver" kararı OnAdClosed'da
        // uygulanır ki oyuncu reklamı izlerken oyun tarafında hiçbir şey değişmesin.
        _pendingRewardGranted = true;
    }

    private void HandleRewardedAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Log($"Rewarded reklam gösterilemedi: {error}");

        Action<bool> callback = _pendingRewardedCallback;
        _pendingRewardedCallback = null;
        callback?.Invoke(false);

        SetRewardedReady(false);
        _rewardedAd?.LoadAd();
    }

    private void HandleRewardedAdClosed(LevelPlayAdInfo info)
    {
        Action<bool> callback = _pendingRewardedCallback;
        bool granted = _pendingRewardGranted;

        _pendingRewardedCallback = null;
        _pendingRewardGranted = false;

        callback?.Invoke(granted);

        // Gösterilen reklam tek kullanımlık - bir sonraki gösterim için hemen yeniden yükle.
        SetRewardedReady(false);
        _rewardedAd?.LoadAd();
    }

    private void SetRewardedReady(bool ready)
    {
        if (IsRewardedAdReady == ready) return;
        IsRewardedAdReady = ready;
        OnRewardedAdReadyChanged?.Invoke(ready);
    }

    // --- Interstitial event handler'ları ---

    private void HandleInterstitialAdLoaded(LevelPlayAdInfo info)
    {
        SetInterstitialReady(true);
    }

    private void HandleInterstitialAdLoadFailed(LevelPlayAdError error)
    {
        SetInterstitialReady(false);
        Log($"Interstitial reklam yüklenemedi: {error}");
    }

    private void HandleInterstitialAdClosed(LevelPlayAdInfo info)
    {
        OnInterstitialClosed?.Invoke();

        SetInterstitialReady(false);
        _interstitialAd?.LoadAd();
    }

    private void SetInterstitialReady(bool ready)
    {
        if (IsInterstitialReady == ready) return;
        IsInterstitialReady = ready;
        OnInterstitialReadyChanged?.Invoke(ready);
    }

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log($"[AdManager] {message}");
    }
}
