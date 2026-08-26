using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Tutorial level oynanırken deste bitince gösterilen tamamlanma bandı. UI elemanlarını
/// (deste sayacı, tarif butonu, istasyon etiketleri, skor, süre) anlatan eski oynanış-içi
/// ipuçları (ilk kart/yanlış/doğru swipe) artık TutorialSpotlightView'in level başlamadan
/// önceki kılavuzlu turuna taşındığı için buradan kaldırıldı - aynı bilgiyi iki kez, iki
/// farklı yerde göstermemek için. Level "isTutorial" değilse (normal level) bu bileşen
/// kendini tamamen devre dışı bırakır - sahnede her zaman durabilir, zararsızdır.
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

    [Header("Tamamlanma")]
    [Tooltip("'Tebrikler' metni gösterildikten kaç saniye sonra (oyuncu erken dokunmazsa) ana menüye dönülür. " +
             "Bu metin autoHide:false ile gösterildiği için ekrandan hiç kaybolmaz - sadece sahne geçişini geciktirir.")]
    [SerializeField] private float delayBeforeReturn = 6.5f;
    [Tooltip("Opsiyonel: tam ekran görünmez bir buton atanırsa, oyuncu tamamlanma metnini okuduktan sonra " +
             "beklemeden dokunup ana menüye geçebilir. Boş bırakılırsa sadece delayBeforeReturn süresi beklenir.")]
    [SerializeField] private Button completionTapCatcher;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Tamamlanma metni "UI" String Table'dan (Assets/_Project/Localization/UIStrings.json
    // kaynaklı) GameLocalization.GetUIString ile çekiliyor - bkz. tutorial_completion.

    [Header("Tarif ('?') Butonu Shake Uyarısı")]
    [Tooltip("Yanlış swipe'ta dikkat çekmesi için sallanacak 'Show Recipe (?)' butonu. Oyuncu butona her bastığında (StopRecipeButtonShake Inspector'da OnClick'e bağlanmalı) sallanma durur; sonraki yanlış swipe'ta tekrar başlar.")]
    [SerializeField] private RectTransform showRecipeButtonTransform;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeStrength = 15f;
    [SerializeField] private int shakeVibrato = 10;

    private bool _isTutorialActive;
    private Sequence _hintSequence;

    private Vector2 _showRecipeButtonRestPos;
    private bool _showRecipeButtonRestPosCaptured;
    private Tween _showRecipeButtonShakeTween;

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

        gameManager.OnInvalidSwipe += HandleInvalidSwipe;
        gameManager.OnLevelWon += HandleLevelWon;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;

        gameManager.OnInvalidSwipe -= HandleInvalidSwipe;
        gameManager.OnLevelWon -= HandleLevelWon;

        _hintSequence?.Kill();
        _showRecipeButtonShakeTween?.Kill();
    }

    private void HandleInvalidSwipe(SwipeDirection direction, StationData station)
    {
        TriggerRecipeButtonShake();
    }

    private void HandleLevelWon(int stars)
    {
        ShowHint(GameLocalization.GetUIString("tutorial_completion"));

        if (completionTapCatcher != null)
        {
            completionTapCatcher.gameObject.SetActive(true);
            completionTapCatcher.onClick.AddListener(HandleCompletionTapped);
        }

        Invoke(nameof(ReturnToMainMenu), delayBeforeReturn);
    }

    /// <summary>Oyuncu completionTapCatcher'a dokununca beklemeden ana menüye geçer.</summary>
    private void HandleCompletionTapped()
    {
        CancelInvoke(nameof(ReturnToMainMenu));
        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        if (completionTapCatcher != null)
        {
            completionTapCatcher.onClick.RemoveListener(HandleCompletionTapped);
            completionTapCatcher.gameObject.SetActive(false);
        }

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

    private void EnsureShowRecipeButtonRestPosCaptured()
    {
        if (_showRecipeButtonRestPosCaptured || showRecipeButtonTransform == null) return;

        _showRecipeButtonRestPos = showRecipeButtonTransform.anchoredPosition;
        _showRecipeButtonRestPosCaptured = true;
    }

    private void TriggerRecipeButtonShake()
    {
        if (showRecipeButtonTransform == null) return;

        EnsureShowRecipeButtonRestPosCaptured();

        showRecipeButtonTransform.anchoredPosition = _showRecipeButtonRestPos;
        _showRecipeButtonShakeTween?.Kill();
        _showRecipeButtonShakeTween = showRecipeButtonTransform
            .DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato)
            .SetLoops(-1)
            .SetEase(Ease.Linear);
    }

    /// <summary>"?" (Show Recipe) butonunun OnClick listesine, RecipePreviewView.OnShowRecipeButtonPressed
    /// ile BİRLİKTE bağlanmalı - oyuncu butona basınca sallanmayı durdurup normale döndürür.</summary>
    public void StopRecipeButtonShake()
    {
        if (showRecipeButtonTransform == null) return;

        EnsureShowRecipeButtonRestPosCaptured();

        _showRecipeButtonShakeTween?.Kill();
        _showRecipeButtonShakeTween = null;
        showRecipeButtonTransform.anchoredPosition = _showRecipeButtonRestPos;
    }

    /// <summary>Şu an tek kullanım yeri tamamlanma metni - autoHide YOK, sadece sahne geçişini geciktirir.</summary>
    private void ShowHint(string text)
    {
        if (hintCanvasGroup == null || hintText == null) return;

        hintText.text = text;

        _hintSequence?.Kill();
        _hintSequence = DOTween.Sequence();
        _hintSequence.Append(hintCanvasGroup.DOFade(1f, fadeDuration));
    }
}
