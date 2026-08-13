using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test/debug amaçlı - tamamlama/yıldız kayıtlarına dokunmadan tüm level'ları
/// kilitsiz sayar. Level Select ekranına, Reset Progress butonunun yanına
/// eklenir.
/// </summary>
public class UnlockAllLevelsButton : MonoBehaviour
{
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
