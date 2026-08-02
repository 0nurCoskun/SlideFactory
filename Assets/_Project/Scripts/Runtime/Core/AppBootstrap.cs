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
        // Application.targetFrameRate + vSyncCount=0 kombinasyonu, hedefi Screen
        // API'sinden (Screen.currentResolution/Screen.resolutions) okuyarak
        // hesaplamaya çalıştığımızda ciddi bir soruna yol açtı: Android'de bu API
        // bazen erken/yanlış bir düşük değer (örn. 30Hz) raporluyor, Unity de
        // Application.targetFrameRate'i o düşük değere göre ayarlayıp her frame'in
        // büyük kısmını "WaitForTargetFPS" içinde bilinçli olarak uyuyarak
        // geçiriyordu (Profiler'da doğrulandı: ~33ms'lik frame'in ~27ms'si bekleme).
        // Bunun sonucu ana menüde gerçek 15-30fps'e düşüyorduk, panelin kendisi
        // 120Hz çalışsa bile.
        //
        // Çözüm: yenileme hızını YAZILIMDA tahmin etmeye çalışmak yerine, gerçek
        // donanım vsync sinyaline senkronize oluyoruz (vSyncCount=1). Böylece
        // Unity hiçbir zaman kendi kendine düşük bir hedefe göre uyumuyor;
        // ekran o an gerçekten hangi hızda çalışıyorsa (60/90/120/adaptif) ona
        // göre kare basıyoruz.
        QualitySettings.vSyncCount = 1;
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
