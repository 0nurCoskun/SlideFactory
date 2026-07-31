using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Sahne geçişlerinde tam ekran siyaha kararan/açılan basit bir "dark screen" geçişi.
/// AppBootstrap/AudioManager gibi DontDestroyOnLoad singleton - hangi sahne önce açılırsa
/// o hayatta kalır, ikinci sahnedeki kopya kendini yok eder (aynı dedup deseni).
///
/// Sahnede tam ekran siyah bir Image + CanvasGroup'a eklenir (örn. "_SceneFader"),
/// hem MainMenu hem Game sahnesine konur ki hangisi önce açılırsa çalışsın.
/// </summary>
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("Bağımlılık")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Ayarlar")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private Ease fadeEase = Ease.InOutQuad;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
    }

    /// <summary>Ekranı karartır, verilen sahneyi yükler, sonra ekranı tekrar açar.</summary>
    public void FadeToScene(string sceneName)
    {
        if (fadeCanvasGroup == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.DOFade(1f, fadeDuration).SetEase(fadeEase).OnComplete(() =>
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.LoadScene(sceneName);
        });
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.DOFade(0f, fadeDuration).SetEase(fadeEase).OnComplete(() =>
        {
            fadeCanvasGroup.blocksRaycasts = false;
        });
    }
}
