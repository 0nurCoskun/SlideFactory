using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Ana menüdeki panel geçişlerini (MainMenuPanel <-> LevelSelectPanel <-> SettingsPanel)
/// ve Quit butonunu yönetir. Artık doğrudan sahne değiştirmiyor - sadece hangi panelin
/// göründüğünü kontrol ediyor. Gerçek sahne geçişi (Game'e gitmek) LevelButton.cs
/// üzerinden, oyuncu bir level seçtiğinde olur.
///
/// Play -> Level Select: MainMenuPanel sola kayarak çıkar, LevelSelectPanel sağdan gelir.
/// Level Select -> geri: TERSİ - LevelSelectPanel sağa çıkar, MainMenuPanel soldan gelir.
/// Settings -> açılış: MainMenuPanel yukarı kayarak çıkar, SettingsPanel aşağıdan gelir.
/// Settings -> geri: TERSİ - SettingsPanel aşağı çıkar, MainMenuPanel yukarıdan gelir.
/// Tüm paneller aynı Canvas altında, tam ekran gerdirilmiş (stretch) kardeşler - bu
/// yüzden anchoredPosition offsetleri panelin kendi rect boyutuna (rect.width/height)
/// göre hesaplanıyor, ekran çözünürlüğünden bağımsız çalışıyor.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private RectTransform mainMenuPanel;
    [SerializeField] private RectTransform levelSelectPanel;
    [SerializeField] private RectTransform settingsPanel;

    [Header("Ayarlar Paneli")]
    [Tooltip("Settings paneli açılırken slider'ları güncel değerlerle senkronize etmek için.")]
    [SerializeField] private SettingsView settingsView;

    [Header("Tutorial")]
    [SerializeField] private LevelData tutorialLevelData;
    [SerializeField] private string gameSceneName = "Game";

    [Header("Geçiş Animasyonu")]
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private Ease transitionEase = Ease.OutCubic;

    [Header("Tutorial'dan Dönüş Animasyonu")]
    [SerializeField] private float returnFromTutorialDuration = 0.4f;
    [SerializeField] private Ease returnFromTutorialEase = Ease.OutBack;

    private bool _isTransitioning;

    private void Awake()
    {
        if (LevelSession.OpenLevelSelectDirectly)
        {
            LevelSession.OpenLevelSelectDirectly = false;
            ShowInstant(levelSelectPanel, mainMenuPanel, settingsPanel);
        }
        else if (LevelSession.ReturnFromTutorialToMenu)
        {
            LevelSession.ReturnFromTutorialToMenu = false;
            ShowInstant(mainMenuPanel, levelSelectPanel, settingsPanel);
            PlayReturnFromTutorialAnimation();
        }
        else
        {
            ShowInstant(mainMenuPanel, levelSelectPanel, settingsPanel);
        }
    }

    /// <summary>Tutorial butonuna bağlanacak - ekranı karartıp Tutorial level'ı Game sahnesinde açar.</summary>
    public void OnTutorialButtonPressed()
    {
        if (tutorialLevelData == null)
        {
            Debug.LogError("[MainMenuController] tutorialLevelData atanmamış.");
            return;
        }

        LevelSession.SelectedLevel = tutorialLevelData;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(gameSceneName);
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    /// <summary>MainMenuPanel, tutorial'dan dönüldüğünde anlık göstermek yerine bir pop-in animasyonu oynatır.</summary>
    private void PlayReturnFromTutorialAnimation()
    {
        if (mainMenuPanel == null) return;

        mainMenuPanel.DOKill();
        mainMenuPanel.localScale = Vector3.zero;
        mainMenuPanel.DOScale(Vector3.one, returnFromTutorialDuration).SetEase(returnFromTutorialEase);
    }

    /// <summary>Play butonuna bağlanacak - Level Select ekranını sağdan içeri kaydırarak açar.</summary>
    public void OnPlayButtonPressed()
    {
        SlideTransition(mainMenuPanel, levelSelectPanel, Vector2.left);
    }

    /// <summary>Level Select ekranındaki "Geri" butonuna bağlanacak.</summary>
    public void OnBackButtonPressed()
    {
        SlideTransition(levelSelectPanel, mainMenuPanel, Vector2.right);
    }

    /// <summary>Settings butonuna bağlanacak - Ayarlar panelini aşağıdan içeri kaydırarak açar.</summary>
    public void OnSettingsButtonPressed()
    {
        if (_isTransitioning) return;

        if (settingsView != null) settingsView.RefreshSliders();
        SlideTransition(mainMenuPanel, settingsPanel, Vector2.up);
    }

    /// <summary>Ayarlar panelindeki "Geri/Kapat" butonuna bağlanacak.</summary>
    public void OnSettingsBackButtonPressed()
    {
        SlideTransition(settingsPanel, mainMenuPanel, Vector2.down);
    }

    /// <summary>Quit butonuna bağlanacak. Editor'de test ederken Play modundan çıkar.</summary>
    public void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// outgoing panel exitDirection yönünde ekran dışına kayar, incoming panel ise
    /// TAM TERS yönden (exitDirection * -1) ekrana girer - böylece "geldiği yere geri
    /// dönme" hissi her zaman tutarlı olur.
    /// </summary>
    private void SlideTransition(RectTransform outgoing, RectTransform incoming, Vector2 exitDirection)
    {
        if (_isTransitioning || outgoing == null || incoming == null) return;
        _isTransitioning = true;

        Vector2 size = outgoing.rect.size;
        Vector2 exitOffset = new Vector2(exitDirection.x * size.x, exitDirection.y * size.y);

        outgoing.gameObject.SetActive(true);
        incoming.gameObject.SetActive(true);

        outgoing.DOKill();
        incoming.DOKill();

        outgoing.anchoredPosition = Vector2.zero;
        incoming.anchoredPosition = -exitOffset;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(outgoing.DOAnchorPos(exitOffset, transitionDuration).SetEase(transitionEase));
        sequence.Join(incoming.DOAnchorPos(Vector2.zero, transitionDuration).SetEase(transitionEase));
        sequence.OnComplete(() =>
        {
            outgoing.gameObject.SetActive(false);
            outgoing.anchoredPosition = Vector2.zero;
            _isTransitioning = false;
        });
    }

    /// <summary>Animasyonsuz, anlık panel durumu ayarlamak için (uygulama ilk açılırken).</summary>
    private void ShowInstant(RectTransform visiblePanel, params RectTransform[] hiddenPanels)
    {
        if (visiblePanel != null)
        {
            visiblePanel.gameObject.SetActive(true);
            visiblePanel.anchoredPosition = Vector2.zero;
        }

        foreach (RectTransform hidden in hiddenPanels)
        {
            if (hidden == null) continue;
            hidden.gameObject.SetActive(false);
            hidden.anchoredPosition = Vector2.zero;
        }
    }
}
