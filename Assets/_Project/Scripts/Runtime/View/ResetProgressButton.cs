using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test/debug amaçlı - tüm level ilerlemesini (tamamlanan level kayıtlarını) sıfırlar.
/// Level Select ekranına küçük bir "Reset Progress" butonuna eklenir.
///
/// SADECE EDİTÖRDE görünür - önceden hiçbir gizleme YOKTU, yani gerçek bir cihaz
/// build'inde (test kullanıcıları dahil) bu buton da HERKESE görünür ve tıklanabilirdi.
/// Application.isEditor kontrolü Awake'te GameObject'i kapatıyor; script kendisi build'e
/// yine dahil olur (sahnede referans var), ama runtime'da devre dışı kalır.
/// </summary>
public class ResetProgressButton : MonoBehaviour
{
    private void Awake()
    {
        if (!Application.isEditor) gameObject.SetActive(false);
    }

    /// <summary>Butonun OnClick()'ine bağlanacak.</summary>
    public void OnResetProgressButtonPressed()
    {
        LevelProgress.ResetAllProgress();

        // Sahneyi yeniden yükleyip tüm LevelButton'ların kilit durumunu
        // (OnEnable üzerinden) tazelemenin en basit yolu bu - tek tek
        // her butonu bulup RefreshLockState() çağırmaya gerek kalmıyor.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
