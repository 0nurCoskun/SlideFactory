using UnityEngine;

/// <summary>
/// Oyunun kart kurallarıyla HİÇBİR ilgisi olmayan, sadece uygulama başlarken
/// bir kere ayarlanması gereken cihaz/ekran/performans ayarlarından sorumludur.
///
/// GameManager'dan bilinçli olarak ayrıldı: GameManager sadece "kart kuralı" bilsin,
/// bu script sadece "cihaz ayarı" bilsin (Single Responsibility).
///
/// Sahnede tek bir GameObject'e (örn. "_Bootstrap") eklenmesi yeterlidir.
/// </summary>
public class AppBootstrap : MonoBehaviour
{
    // Sahneler arası (MainMenu -> Level1 -> Level2 ...) geçişlerde her sahnede
    // yeniden yaratılmasını engellemek için basit bir singleton koruması.
    public static AppBootstrap Instance { get; private set; }

    [Header("Performans")]
    [Tooltip("Cihazın kendi ekran yenileme hızı (60/90/120Hz vb.) OTOMATİK okunup hedeflenir. " +
             "Bu değer sadece o okuma başarısız olursa (nadir bazı cihazlarda) devreye giren yedek değerdir.")]
    [SerializeField] private int fallbackFrameRate = 60;

    [Header("Ekran")]
    [Tooltip("Oyun dikey formatta tasarlandığı için varsayılan Portrait.")]
    [SerializeField] private ScreenOrientation screenOrientation = ScreenOrientation.Portrait;

    [Tooltip("Cihaz uykuya geçip ekranı kapatmasın (swipe tabanlı oyunlarda kullanıcı bazen birkaç saniye duraksar).")]
    [SerializeField] private bool disableScreenTimeout = true;

    private void Awake()
    {
        // Zaten bir AppBootstrap varsa (önceki sahneden DontDestroyOnLoad ile gelen),
        // bu yeni kopyayı yok et - ayarları tekrar tekrar uygulamaya gerek yok.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyPerformanceSettings();
        ApplyScreenSettings();
    }

    private void ApplyPerformanceSettings()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = GetAdaptiveTargetFrameRate();
    }

    /// <summary>
    /// Sabit bir değer (örn. 60 veya 120) yazmak yerine, cihazın EKRANININ gerçekten
    /// desteklediği yenileme hızını okuyup onu hedefliyoruz. Böylece:
    /// - 120Hz ekranlı bir telefonda oyun 120'ye çıkabiliyor (daha akıcı swipe hissi),
    /// - 60Hz ekranlı eski/bütçe bir telefonda gereksiz yere 120'ye zorlanmıyor
    ///   (zaten ekran gösteremeyeceği için boşa batarya/ısı harcanır).
    /// </summary>
    private int GetAdaptiveTargetFrameRate()
    {
        double refreshRate = Screen.currentResolution.refreshRateRatio.value;

        if (refreshRate <= 0)
        {
            return fallbackFrameRate;
        }

        return Mathf.RoundToInt((float)refreshRate);
    }

    private void ApplyScreenSettings()
    {
        Screen.orientation = screenOrientation;

        if (disableScreenTimeout)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}