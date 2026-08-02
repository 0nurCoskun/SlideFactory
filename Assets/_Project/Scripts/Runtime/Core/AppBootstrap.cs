using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplyScreenSettings();
        StartCoroutine(ApplyPerformanceSettingsDelayed());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Uygulama açılışında Screen.resolutions/currentResolution bazı Android
        // cihazlarda henüz gerçek ekran modunu yansıtmıyor (işletim sistemi ilk
        // birkaç frame'de düşük güç moduyla açılıp asıl desteklenen yenileme hızına
        // sonradan geçiyor). Bu yüzden her sahne geçişinde ölçümü tekrarlıyoruz;
        // ilk ölçüm yanlışsa (örn. menüde 30fps hissi), bir sonraki sahne geçişinde
        // kendi kendine düzeliyor.
        StartCoroutine(ApplyPerformanceSettingsDelayed());
    }

    private IEnumerator ApplyPerformanceSettingsDelayed()
    {
        // Ekranın gerçek desteklenen mod listesini (Screen.resolutions) doğru
        // raporlaması için birkaç frame bekliyoruz; Awake anında okumak
        // (özellikle soğuk açılışta) çoğu zaman yanlış/düşük bir değer veriyor.
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        ApplyPerformanceSettings();
    }

    private void ApplyPerformanceSettings()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = GetAdaptiveTargetFrameRate();
    }

    /// <summary>
    /// Sabit bir değer (örn. 60 veya 120) yazmak yerine, cihazın EKRANININ gerçekten
    /// desteklediği EN YÜKSEK yenileme hızını okuyup onu hedefliyoruz. Böylece:
    /// - 120Hz ekranlı bir telefonda oyun 120'ye çıkabiliyor (daha akıcı swipe hissi),
    /// - 90Hz'i geçemeyen bir ekranda 90 hedefleniyor,
    /// - 60Hz ekranlı eski/bütçe bir telefonda gereksiz yere 120'ye zorlanmıyor
    ///   (zaten ekran gösteremeyeceği için boşa batarya/ısı harcanır).
    ///
    /// Android'de Screen.currentResolution çoğu zaman cihazın o anki (genelde 60Hz)
    /// ekran moduna denk gelir, cihazın desteklediği maksimuma değil - bu yüzden
    /// Screen.resolutions listesindeki TÜM modlara bakıp en yükseğini alıyoruz.
    /// </summary>
    private int GetAdaptiveTargetFrameRate()
    {
        double highestRefreshRate = Screen.currentResolution.refreshRateRatio.value;

        Resolution[] resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            double candidate = resolutions[i].refreshRateRatio.value;
            if (candidate > highestRefreshRate)
            {
                highestRefreshRate = candidate;
            }
        }

        if (highestRefreshRate <= 0)
        {
            return fallbackFrameRate;
        }

        return Mathf.RoundToInt((float)highestRefreshRate);
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