using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

/// <summary>
/// Firebase Analytics/Crashlytics/Remote Config için tek giriş noktası.
/// AudioManager/SceneFader gibi DontDestroyOnLoad singleton - hangi sahne önce açılırsa o
/// hayatta kalır, ikinci sahnedeki kopya kendini yok eder (aynı dedup deseni).
///
/// Firebase bağımlılıkları asenkron kontrol edildiği için Awake() sırasında senkron sonuç
/// beklenmemeli (bkz. LocalizationManager'daki aynı uyarı). IsReady false iken çağrılan
/// LogEvent/GetRemoteConfig* metotları sessizce no-op/varsayılan döner, oyunun akışını bloklamaz.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    /// <summary>Firebase bağımlılıkları çözülüp FirebaseApp hazır olduğunda true olur.</summary>
    public static bool IsReady { get; private set; }

    [Header("Remote Config Varsayılanları")]
    [Tooltip("Sunucudan henüz veri çekilmemişse veya fetch başarısız olursa kullanılacak " +
             "varsayılan key/value çiftleri.")]
    [SerializeField] private List<RemoteConfigDefault> remoteConfigDefaults = new();

    [Serializable]
    private struct RemoteConfigDefault
    {
        public string key;
        public string value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Firebase C++ SDK'nın masaüstü native binary'leri (FirebaseCppApp-*.dll) projeye hiç
        // eklenmemiş — sadece Android/iOS/tvOS native'leri var. Editor'de bu native çağrı
        // DllNotFoundException ile patlar. IsReady zaten false kalıp tüm public metotlar
        // sessizce no-op/fallback döndüğü için Editor'de init'i baştan atlamak yeterli;
        // cihaz/gerçek build'lerde davranış değişmez.
        if (Application.isEditor)
        {
            Debug.LogWarning("[FirebaseManager] Editor'de masaüstü native Firebase binary'leri yok, init atlanıyor.");
            return;
        }

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(HandleDependencyCheck);
    }

    private void HandleDependencyCheck(System.Threading.Tasks.Task<DependencyStatus> task)
    {
        if (task.Exception != null)
        {
            Debug.LogError($"[FirebaseManager] Bağımlılık kontrolü hata verdi: {task.Exception}");
            return;
        }

        var dependencyStatus = task.Result;
        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError($"[FirebaseManager] Firebase bağımlılıkları çözülemedi: {dependencyStatus}");
            return;
        }

        var app = FirebaseApp.DefaultInstance;
        IsReady = true;

        // Crashlytics çöküş toplama varsayılan olarak açık gelir; yakalanmamış exception'ları
        // fatal olarak işaretlemesi zaten default davranış, ekstra bir şey yapmaya gerek yok.
        Crashlytics.IsCrashlyticsCollectionEnabled = true;

        FetchAndActivateRemoteConfig();

        Debug.Log($"[FirebaseManager] Hazır. App: {app.Name}");
    }

    private async void FetchAndActivateRemoteConfig()
    {
        var defaults = new Dictionary<string, object>();
        foreach (var entry in remoteConfigDefaults)
        {
            if (!string.IsNullOrEmpty(entry.key))
                defaults[entry.key] = entry.value;
        }

        var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        await remoteConfig.SetDefaultsAsync(defaults);

        try
        {
            await remoteConfig.FetchAndActivateAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirebaseManager] Remote Config fetch başarısız, varsayılanlar kullanılacak: {e.Message}");
        }
    }

    /// <summary>Analytics event'i kaydeder. Firebase hazır değilse sessizce no-op.</summary>
    public static void LogEvent(string eventName, params Parameter[] parameters)
    {
        if (!IsReady) return;

        if (parameters is { Length: > 0 })
            FirebaseAnalytics.LogEvent(eventName, parameters);
        else
            FirebaseAnalytics.LogEvent(eventName);
    }

    /// <summary>Crashlytics raporlarına eklenecek özel bir key/value (örn. son açılan seviye).</summary>
    public static void SetCrashlyticsCustomKey(string key, string value)
    {
        if (!IsReady) return;
        Crashlytics.SetCustomKey(key, value);
    }

    /// <summary>Fatal olmayan bir exception'ı manuel olarak Crashlytics'e bildirir.</summary>
    public static void LogException(Exception exception)
    {
        if (!IsReady) return;
        Crashlytics.LogException(exception);
    }

    /// <summary>Firebase hazır değilse veya key yoksa fallback döner.</summary>
    public static string GetRemoteConfigString(string key, string fallback = "")
    {
        if (!IsReady) return fallback;
        return FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
    }

    /// <summary>Firebase hazır değilse veya key yoksa fallback döner.</summary>
    public static long GetRemoteConfigLong(string key, long fallback = 0)
    {
        if (!IsReady) return fallback;
        return FirebaseRemoteConfig.DefaultInstance.GetValue(key).LongValue;
    }

    /// <summary>Firebase hazır değilse veya key yoksa fallback döner.</summary>
    public static bool GetRemoteConfigBool(string key, bool fallback = false)
    {
        if (!IsReady) return fallback;
        return FirebaseRemoteConfig.DefaultInstance.GetValue(key).BooleanValue;
    }
}
