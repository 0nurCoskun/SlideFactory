using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// Tutorial level oynanırken GameManager event'lerine göre kısa, tek seferlik ipuçları
/// gösteren küçük bir alt bant. Level "isTutorial" değilse (normal level) bu bileşen
/// kendini tamamen devre dışı bırakır - sahnede her zaman durabilir, zararsızdır.
///
/// Adım adım anlatım "Next" tuşuna basılan bir sihirbaz DEĞİL - oyuncu gerçekten
/// oynarken (ilk kart geldiğinde, ilk yanlış/doğru swipe'ta, deste bitince) tetiklenir.
/// RecipePreviewView zaten üretim zincirini (ham -> istasyon -> ürün) oynanışdan ÖNCE
/// gösteriyor - bu "0. adım" hiç değiştirilmeden aynen kullanılıyor.
/// </summary>
public class TutorialFlowView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("İpucu Bandı")]
    [SerializeField] private CanvasGroup hintCanvasGroup;
    [SerializeField] private TMP_Text hintText;

    [Header("Animasyon")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float hintDisplayDuration = 4.5f;

    [Header("Tamamlanma")]
    [SerializeField] private string completionText = "Tutorial complete! You're ready to play for real.";
    [SerializeField] private float delayBeforeReturn = 1.8f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("İpucu Metinleri")]
    [SerializeField] private string firstCardHint = "Swipe the card toward the station shown on that edge. Wrong station? No worries - it just resets!";
    [SerializeField] private string wrongSwipeHint = "Oops! Wrong station - the card went back to raw. Watch the station labels!";
    [SerializeField] private string correctSwipeHint = "Nice! Correct swipes move the card forward. Stations reshuffle over time, so stay alert!";

    private bool _isTutorialActive;
    private bool _hasShownFirstCardHint;
    private bool _hasShownWrongHint;
    private bool _hasShownCorrectHint;
    private Sequence _hintSequence;

    private void Awake()
    {
        _isTutorialActive = gameManager != null && gameManager.ActiveLevel != null && gameManager.ActiveLevel.isTutorial;

        if (!_isTutorialActive)
        {
            gameObject.SetActive(false);
            return;
        }

        if (hintCanvasGroup != null) hintCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        if (!_isTutorialActive || gameManager == null) return;

        gameManager.OnCardChanged += HandleCardChanged;
        gameManager.OnInvalidSwipe += HandleInvalidSwipe;
        gameManager.OnCardProcessed += HandleCardProcessed;
        gameManager.OnCardCompleted += HandleCardCompleted;
        gameManager.OnLevelWon += HandleLevelWon;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;

        gameManager.OnCardChanged -= HandleCardChanged;
        gameManager.OnInvalidSwipe -= HandleInvalidSwipe;
        gameManager.OnCardProcessed -= HandleCardProcessed;
        gameManager.OnCardCompleted -= HandleCardCompleted;
        gameManager.OnLevelWon -= HandleLevelWon;

        _hintSequence?.Kill();
    }

    private void HandleCardChanged(CardInstance card)
    {
        if (_hasShownFirstCardHint) return;
        _hasShownFirstCardHint = true;
        ShowHint(firstCardHint);
    }

    private void HandleInvalidSwipe(SwipeDirection direction, StationData station)
    {
        if (_hasShownWrongHint) return;
        _hasShownWrongHint = true;
        ShowHint(wrongSwipeHint);
    }

    private void HandleCardProcessed(CardInstance card, CardData resultData)
    {
        TryShowCorrectSwipeHint();
    }

    private void HandleCardCompleted(CardInstance card)
    {
        TryShowCorrectSwipeHint();
    }

    private void TryShowCorrectSwipeHint()
    {
        if (_hasShownCorrectHint) return;
        _hasShownCorrectHint = true;
        ShowHint(correctSwipeHint);
    }

    private void HandleLevelWon(int stars)
    {
        ShowHint(completionText, autoHide: false);
        Invoke(nameof(ReturnToMainMenu), delayBeforeReturn);
    }

    private void ReturnToMainMenu()
    {
        LevelSession.ReturnFromTutorialToMenu = true;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void ShowHint(string text, bool autoHide = true)
    {
        if (hintCanvasGroup == null || hintText == null) return;

        hintText.text = text;

        _hintSequence?.Kill();
        _hintSequence = DOTween.Sequence();
        _hintSequence.Append(hintCanvasGroup.DOFade(1f, fadeDuration));

        if (autoHide)
        {
            _hintSequence.AppendInterval(hintDisplayDuration);
            _hintSequence.Append(hintCanvasGroup.DOFade(0f, fadeDuration));
        }
    }
}
