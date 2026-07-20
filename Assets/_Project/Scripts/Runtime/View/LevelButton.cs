using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level Select ekranındaki HER BUTONA ayrı ayrı eklenir. Her buton kendi
/// LevelData'sını taşır, tıklanınca LevelSession'a yazıp Game sahnesini açar.
///
/// Ayrıca bu level'ın KİLİTLİ olup olmadığını kontrol eder (LevelProgress
/// üzerinden) - kilitliyse buton tıklanamaz hale gelir ve varsa bir kilit
/// ikonu gösterilir.
/// </summary>
public class LevelButton : MonoBehaviour
{
    [Header("Bu Butonun Temsil Ettiği Level")]
    [SerializeField] private LevelData levelData;

    [Header("Sahne")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Kilit Görseli (opsiyonel)")]
    [Tooltip("Level kilitliyse aktif edilecek bir kilit ikonu/overlay. Boş bırakılabilir.")]
    [SerializeField] private GameObject lockIcon;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        RefreshLockState();
    }

    private void RefreshLockState()
    {
        bool unlocked = LevelProgress.IsLevelUnlocked(levelData);

        if (_button != null) _button.interactable = unlocked;
        if (lockIcon != null) lockIcon.SetActive(!unlocked);
    }

    /// <summary>Button'un OnClick()'ine bağlanacak.</summary>
    public void OnLevelButtonPressed()
    {
        if (levelData == null)
        {
            Debug.LogError($"[LevelButton] '{gameObject.name}' objesine bir LevelData atanmamış.");
            return;
        }

        // Interactable=false zaten tıklamayı engeller ama ekstra bir güvenlik katmanı olsun.
        if (!LevelProgress.IsLevelUnlocked(levelData))
        {
            Debug.LogWarning($"[LevelButton] '{levelData.displayName}' henüz kilitli, önce önceki level'ı tamamlaman gerekiyor.");
            return;
        }

        LevelSession.SelectedLevel = levelData;
        SceneManager.LoadScene(gameSceneName);
    }
}