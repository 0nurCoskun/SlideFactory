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
    [Tooltip("Sabit, güvenilir bir hedef. Cihazın panel Hz'ini Screen API'siyle tahmin " +
             "etmeye ÇALIŞMIYORUZ artık (Android'de erken/yanlış düşük değer okuyup gerçek " +
             "bir bug'a yol açtı). Bu değer sadece Swappy'e (androidUseSwappy) 'panel'i " +
             "bu hıza çıkar' talebini iletmek için üst sınır görevi görür; cihaz daha " +
             "düşük bir maksimuma sahipse zaten ona düşer.")]
    [SerializeField] private int targetFrameRate = 120;

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
        // vSyncCount=1 ile test ettiğimizde bile ana menü fiziksel panelde gerçekten
        // 30Hz'e düşüyordu (Profiler'da doğrulandı) - yani sorun Unity'nin kendi
        // kendine uyuması değil, Android'in adaptif yenileme hızı denetleyicisinin
        // (SurfaceFlinger) "düşük hareketli" gördüğü menü içeriği için paneli bilerek
        // düşük Hz'de sürmesiydi. vSyncCount tek başına sadece "panel o an ne
        // hızdaysa ona göre bekle" der, panelden daha YÜKSEK bir hız TALEP ETMEZ.
        //
        // Android'den yüksek Hz'i açıkça talep eden mekanizma Swappy (androidUseSwappy,
        // bkz. Player Settings > Android > Optimized Frame Pacing) - bunu daha önce
        // kapatmıştık ki bu hataydı. Şimdi vSyncCount=0 + sabit/güvenilir bir
        // targetFrameRate ile geri açıyoruz ki Swappy bu hedefi Android'in Frame Rate
        // API'sine iletip paneli gerçekten hızlandırabilsin.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
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
