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

    [Header("Level Bilgi Paneli")]
    [Tooltip("Atanırsa, tıklanınca direkt sahne açmak yerine önce bu bilgi paneli gösterilir.")]
    [SerializeField] private LevelInfoPanelView levelInfoPanelView;

    [Header("Kilit Görseli (opsiyonel)")]
    [Tooltip("Level kilitliyse aktif edilecek bir kilit ikonu/overlay. Boş bırakılabilir.")]
    [SerializeField] private GameObject lockIcon;

    [Header("Yıldız Gösterimi (opsiyonel)")]
    [Tooltip("Level hiç tamamlanmamışsa (0 yıldız) tüm bu konteyner gizlenir.")]
    [SerializeField] private GameObject starsContainer;
    [SerializeField] private Image[] starImages;
    [SerializeField] private Sprite filledStarSprite;
    [SerializeField] private Sprite emptyStarSprite;

    private Button _button;
    private CanvasGroup _canvasGroup;
    public LevelData LevelData => levelData;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>(); // yoksa null kalır, aşağıda null kontrolü var
    }

    private void OnEnable()
    {
        RefreshLockState();
        RefreshStarDisplay();
    }

    private void RefreshLockState()
    {
        bool unlocked = LevelProgress.IsLevelUnlocked(levelData);

        if (_button != null) _button.interactable = unlocked;
        if (lockIcon != null) lockIcon.SetActive(!unlocked);

        // CanvasGroup varsa, kilitliyken TÜM pointer event'lerini (OnPointerDown/Up
        // dahil, kendi yazdığın custom script'ler dahil) tek satırda engeller.
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = unlocked;
            _canvasGroup.interactable = unlocked;
        }
    }

    private void RefreshStarDisplay()
    {
        if (starsContainer == null || levelData == null) return;

        int stars = LevelProgress.GetStars(levelData);

        // Hiç tamamlanmamışsa (0 yıldız), yıldız satırını tamamen gizle -
        // henüz oynanmamış bir level'da boş yıldız göstermek gereksiz görsel gürültü.
        bool hasAnyStars = stars > 0;
        starsContainer.SetActive(hasAnyStars);

        if (!hasAnyStars || starImages == null) return;

        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;
            starImages[i].sprite = (i < stars) ? filledStarSprite : emptyStarSprite;
        }
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

        if (levelInfoPanelView != null)
        {
            // Direkt sahne açmak yerine önce bilgi panelini göster - Play/Geri
            // kararı artık o panelde, sahne açma sorumluluğu da ona devredildi.
            levelInfoPanelView.ShowForLevel(levelData);
        }
        else
        {
            LevelSession.SelectedLevel = levelData;
            SceneManager.LoadScene(gameSceneName);
        }
    }
}