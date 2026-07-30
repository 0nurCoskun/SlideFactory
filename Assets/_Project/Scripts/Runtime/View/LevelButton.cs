using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

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

    [Header("Yeni Yıldız Animasyonu (highscore - opsiyonel)")]
    [Tooltip("Bu level'da YENİ bir yıldız rekoru kırılmışsa (LevelProgress.HasPendingStarReveal), " +
             "Level Select ekranına dönüldüğünde yıldızlar anında değil bu animasyonla, BİR KERELİĞİNE belirir.")]
    [SerializeField] private float starPopDuration = 0.35f;
    [SerializeField] private float starStagger = 0.15f;
    [SerializeField] private Ease starPopEase = Ease.OutBack;

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

        if (LevelProgress.HasPendingStarReveal(levelData))
        {
            // Bu level'da YENİ bir rekor var ve henüz kimseye gösterilmedi -
            // hemen tüketiyoruz (ClearPendingStarReveal) ki panel her açılıp
            // kapandığında tekrar tekrar oynamasın, sadece BİR KEZ oynasın.
            LevelProgress.ClearPendingStarReveal(levelData);
            AnimateStars(stars);
        }
        else
        {
            SetStarsInstant(stars);
        }
    }

    /// <summary>Butonlar küçük olduğu için yıldızlar burada WinPanel'dekinden daha küçük (0.5) hedef scale'e sahip.</summary>
    private static readonly Vector3 TargetStarScale = new Vector3(0.5f, 0.5f, 0.5f);

    private void SetStarsInstant(int stars)
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;
            starImages[i].transform.DOKill();
            starImages[i].transform.localScale = TargetStarScale;
            starImages[i].sprite = (i < stars) ? filledStarSprite : emptyStarSprite;
        }
    }

    private void AnimateStars(int stars)
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            Image starImage = starImages[i];
            if (starImage == null) continue;

            starImage.sprite = (i < stars) ? filledStarSprite : emptyStarSprite;

            Transform starTransform = starImage.transform;
            starTransform.DOKill();
            starTransform.localScale = Vector3.zero;
            starTransform.DOScale(TargetStarScale, starPopDuration).SetEase(starPopEase).SetDelay(i * starStagger);
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