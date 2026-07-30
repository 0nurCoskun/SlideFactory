using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// GameManager'ın OnLevelWon / OnLevelFailed event'lerini dinler, ilgili paneli
/// açar ve Restart/Ana Menü butonlarının davranışını yönetir.
///
/// Sahnede tek bir GameObject'e eklenir (örn. "_LevelResultView"), iki panele
/// referans verir. Paneller varsayılan olarak KAPALI (inactive) durmalı.
/// </summary>
public class LevelResultView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Paneller (başlangıçta inactive olmalı)")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [Header("Next Level Butonu (WinPanel içinde)")]
    [Tooltip("Son level'da otomatik gizlenir (LevelData.nextLevel boşsa).")]
    [SerializeField] private GameObject nextLevelButton;

    [Header("Yıldız Görselleri (WinPanel içinde, soldan sağa 1-2-3 sırasıyla)")]
    [SerializeField] private UnityEngine.UI.Image[] starImages;
    [SerializeField] private Sprite filledStarSprite;
    [SerializeField] private Sprite emptyStarSprite;

    [Header("Giriş Animasyonu")]
    [SerializeField] private float popDuration = 0.4f;
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Yıldız Giriş Animasyonu (WinPanel açıldıktan sonra)")]
    [Tooltip("Panel'in kendi pop animasyonu bitince, yıldızlar patlamaya başlamadan önceki ek bekleme.")]
    [SerializeField] private float starStartDelay = 0.15f;
    [Tooltip("Her bir yıldızın kendi pop (0 -> 1 scale) süresi.")]
    [SerializeField] private float starPopDuration = 0.35f;
    [Tooltip("Yıldızlar arasındaki gecikme - art arda, tek tek patlasınlar diye.")]
    [SerializeField] private float starStagger = 0.15f;
    [SerializeField] private Ease starPopEase = Ease.OutBack;
    [Tooltip("Sadece KAZANILAN yıldızlar patladığında çalınır (boş yıldızlar sessiz belirir).")]
    [SerializeField] private AudioClip starPopSound;

    [Header("Sahne İsimleri")]
    [Tooltip("Restart'a basınca mevcut sahne yeniden yüklenir - bu alanı doldurmana gerek yok.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        // Baştan emin ol - Inspector'da yanlışlıkla aktif bırakılmış olabilir.
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelWon += HandleLevelWon;
            gameManager.OnLevelFailed += HandleLevelFailed;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelWon -= HandleLevelWon;
            gameManager.OnLevelFailed -= HandleLevelFailed;
        }
    }

    private void HandleLevelWon(int stars)
    {
        ShowPanel(winPanel);
        AnimateStars(stars);

        bool hasNextLevel = gameManager != null && gameManager.ActiveLevel != null && gameManager.ActiveLevel.nextLevel != null;
        if (nextLevelButton != null) nextLevelButton.SetActive(hasNextLevel);
    }

    /// <summary>
    /// Doğru sprite'ı (dolu/boş) hemen atar ama görsel olarak scale 0'da gizli tutar,
    /// sonra panel pop animasyonu bitince yıldızları tek tek (staggered) "patlatır".
    /// Kazanılan yıldızlar patlarken ayrıca starPopSound çalınır.
    /// </summary>
    private void AnimateStars(int stars)
    {
        if (starImages == null) return;

        Sequence starSequence = DOTween.Sequence();

        for (int i = 0; i < starImages.Length; i++)
        {
            UnityEngine.UI.Image starImage = starImages[i];
            if (starImage == null) continue;

            bool earned = i < stars;
            starImage.sprite = earned ? filledStarSprite : emptyStarSprite;

            Transform starTransform = starImage.transform;
            starTransform.DOKill();
            starTransform.localScale = Vector3.zero;

            float startTime = popDuration + starStartDelay + i * starStagger;
            starSequence.Insert(startTime, starTransform.DOScale(Vector3.one, starPopDuration).SetEase(starPopEase));

            if (earned)
            {
                starSequence.InsertCallback(startTime, () => AudioManager.Instance?.PlaySFX(starPopSound));
            }
        }
    }

    private void HandleLevelFailed()
    {
        ShowPanel(losePanel);
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;

        panel.SetActive(true);
        panel.transform.localScale = Vector3.zero;
        panel.transform.DOScale(Vector3.one, popDuration).SetEase(popEase);
    }

    /// <summary>Restart butonuna bağlanacak.</summary>
    public void OnRestartButtonPressed()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    /// <summary>Ana Menü butonuna bağlanacak (istersen kullan, zorunlu değil).</summary>
    public void OnMainMenuButtonPressed()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>WinPanel içindeki Next Level butonuna bağlanacak.</summary>
    public void OnNextLevelButtonPressed()
    {
        LevelData next = gameManager?.ActiveLevel?.nextLevel;
        if (next == null) return; // güvenlik - buton zaten gizli olmalıydı

        LevelSession.SelectedLevel = next;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}