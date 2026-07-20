using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test/debug amaçlı - tüm level ilerlemesini (tamamlanan level kayıtlarını) sıfırlar.
/// Level Select ekranına küçük bir "Reset Progress" butonuna eklenir.
/// </summary>
public class ResetProgressButton : MonoBehaviour
{
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
