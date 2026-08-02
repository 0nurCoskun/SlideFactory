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
             "bir bug'a yol açtı). Bu değer Window.setFrameRate() ile Android'e doğrudan " +
             "iletilir (RequestHighDisplayRefreshRateOnAndroid); cihaz daha düşük bir " +
             "maksimuma sahipse zaten ona düşer.")]
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
        // Logcat'te doğrudan görüldü: Swappy (androidUseSwappy) kendi kendine
        // setFrameRate(120) İLE setFrameRate(30) arasında gidip geliyordu - yani
        // ana menüdeki geçici bir yavaşlamayı (asset yükleme/GC/ilk shader derlemesi)
        // ölçüp "bu cihaz sadece 30 kaldırabilir" diye düşünüp kendi isteğini
        // düşürüyordu. Swappy'yi (ProjectSettings'te androidUseSwappy=0) kapalı
        // tutuyoruz ki bu otomatik/değişken davranış devre dışı kalsın; tek yenileme
        // hızı talebi RequestHighDisplayRefreshRateOnAndroid()'deki TEK SEFERLİK,
        // sabit Window.setFrameRate() çağrısı olsun.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        RequestHighDisplayRefreshRateOnAndroid();
    }

    /// <summary>
    /// Swappy açıkken kendi setFrameRate çağrılarının 120 ile 30 arasında gidip
    /// geldiği logcat'te doğrudan görüldü (kendi iç ölçümüne göre otomatik
    /// düşürüyordu). Bunun yerine Android'in Frame Rate API'sini (Window.setFrameRate,
    /// API 30+, bkz. developer.android.com/games/optimize/display-refresh-rate-change)
    /// TEK SEFERLİK ve SABİT bir değerle biz çağırıyoruz ki kimse sonradan düşürmesin.
    ///
    /// Not: Google'ın kendi dokümantasyonu bile bu çağrının GARANTİ olmadığını
    /// belirtiyor ("the system might still limit the refresh rate"), bu yüzden
    /// bu sadece elimizdeki EN GÜÇLÜ sinyal - kesin çözüm garantisi değil.
    /// </summary>
    private void RequestHighDisplayRefreshRateOnAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            const int AndroidApiLevel30_R = 30; // Window.setFrameRate API 30'da eklendi.

            // Bu iki değeri artık TAHMİN ETMİYORUZ: cihazdaki gerçek logcat çıktısında
            // Swappy'nin kendi setFrameRate çağrılarını "Default, OnlySeamless" olarak
            // gördük, yani FRAME_RATE_COMPATIBILITY_DEFAULT = 0 ve
            // CHANGE_FRAME_RATE_ONLY_IF_SEAMLESS = 0 olduğu doğrulandı.
            // (Önceki denemede bu sabiti android.view.Window sınıfından okumaya
            // çalışmıştık ve NoSuchFieldError almıştık - sabit aslında android.view.Surface
            // sınıfında tanımlı; Window.setFrameRate ise aynı int değerini kabul ediyor.)
            const int FrameRateCompatibilityDefault = 0;
            const int ChangeFrameRateOnlyIfSeamless = 0;

            using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                int sdkInt = versionClass.GetStatic<int>("SDK_INT");
                if (sdkInt < AndroidApiLevel30_R)
                {
                    return;
                }
            }

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
            {
                window.Call("setFrameRate", (float)targetFrameRate, FrameRateCompatibilityDefault, ChangeFrameRateOnlyIfSeamless);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"AppBootstrap: Window.setFrameRate isteği başarısız oldu, Swappy/vSync yoluna güveniliyor. {ex}");
        }
#endif
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
