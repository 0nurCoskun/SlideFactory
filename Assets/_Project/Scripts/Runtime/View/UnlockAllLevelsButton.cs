using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test/debug amaçlı - tamamlama/yıldız kayıtlarına dokunmadan tüm level'ları
/// kilitsiz sayar. Level Select ekranına, Reset Progress butonunun yanına
/// eklenir.
///
/// SADECE EDİTÖRDE görünür - bkz. ResetProgressButton'daki aynı not. Bu koruma
/// olmadan gerçek bir build'i indiren HERHANGİ BİR test kullanıcısı tüm level'ları
/// bedavaya açabilirdi (yıldız/skor eşiklerini tamamen anlamsızlaştırır).
/// </summary>
public class UnlockAllLevelsButton : MonoBehaviour
{
    private void Awake()
    {
        if (!Application.isEditor) gameObject.SetActive(false);
    }

    /// <summary>Butonun OnClick()'ine bağlanacak.</summary>
    public void OnUnlockAllLevelsButtonPressed()
    {
        LevelProgress.UnlockAllLevels();

        // Sahneyi yeniden yükleyip tüm LevelButton'ların kilit durumunu
        // (OnEnable üzerinden) tazelemenin en basit yolu bu - ResetProgressButton
        // ile aynı desen, tek tek her butonu bulup RefreshLockState() çağırmaya
        // gerek kalmıyor.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
