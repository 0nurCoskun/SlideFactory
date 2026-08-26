using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Tutorial level'da BeginLevelPlay() fiilen çalıştığında (GameManager.OnLevelBegun) devreye giren,
/// oyuncuyu ekrandaki önemli UI elemanları (deste sayacı, tarif butonu, istasyon etiketleri, skor,
/// süre) üzerinden tek tek gezdiren kılavuzlu tur. Level normal (isTutorial=false) ise bu bileşen
/// kendini tamamen devre dışı bırakır - sahnede her zaman durabilir, zararsızdır.
///
/// Her adımda: hedef elemanın etrafında dikdörtgen bir "delik" bırakan koyu bir perde çizilir
/// (ekranın geri kalanı odaktan uzaklaşsın diye), altına açıklama metni konur ve oyuncu ekrana
/// dokunana kadar (otomatik kapanma YOK) bir sonraki adıma geçilmez - TutorialFlowView.HandleLevelWon
/// gibi metnin "çok hızlı kaybolması" şikayetine karşı bilinçli tercih.
///
/// Tur boyunca level PauseLevel() ile donmuş tutulur (süre akmaz, istasyonlar karışmaz, swipe
/// yok sayılır); tur bitince ResumeLevel() ile kaldığı yerden devam eder.
/// </summary>
public class TutorialSpotlightView : MonoBehaviour
{
    [System.Serializable]
    private class SpotlightStep
    {
        [Tooltip("Bu adımda vurgulanacak eleman(lar) - birden fazlaysa (örn. 4 istasyon etiketi) hepsini saran tek bir dikdörtgen delik çizilir.")]
        public RectTransform[] targets;
        [Tooltip("UI String Table anahtarı (GameLocalization.GetUIString ile çekilir).")]
        public string localizationKey;
    }

    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Vurgulanacak UI Elemanları (bu sırayla gösterilir)")]
    [SerializeField] private RectTransform deckCountTarget;
    [SerializeField] private RectTransform showRecipeButtonTarget;
    [Tooltip("StationLabelsView'in 4 yön etiketi - hepsi TEK bir delikte birlikte gösterilir.")]
    [SerializeField] private RectTransform[] stationLabelTargets;
    [SerializeField] private RectTransform scoreDisplayTarget;
    [SerializeField] private RectTransform timerTarget;

    [Header("Görünüm")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private float cutoutPadding = 18f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private int overlaySortingOrder = 500;
    [Tooltip("Ana canvas'lardakiyle (Game.unity) aynı olmalı, aksi halde hedeflerin ekran koordinatları uyuşmaz.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
    [SerializeField] private TMP_FontAsset captionFont;
    [SerializeField] private float captionFontSize = 52f;
    [SerializeField] private Color captionTextColor = Color.white;

    private bool _isTutorialActive;
    private readonly List<SpotlightStep> _steps = new List<SpotlightStep>();
    private int _stepIndex = -1;

    private RectTransform _overlayRect;
    private CanvasGroup _overlayCanvasGroup;
    private RectTransform _barTop, _barBottom, _barLeft, _barRight;
    private TMP_Text _captionText;
    private TMP_Text _tapHintText;
    private Button _tapCatcher;
    private Sequence _fadeSequence;

    private void Awake()
    {
        _isTutorialActive = gameManager != null && gameManager.ActiveLevel != null && gameManager.ActiveLevel.isTutorial;

        if (!_isTutorialActive)
        {
            enabled = false;
            return;
        }

        BuildStepList();
        BuildOverlayHierarchy();
    }

    private void OnEnable()
    {
        if (!_isTutorialActive || gameManager == null) return;
        gameManager.OnLevelBegun += HandleLevelBegun;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.OnLevelBegun -= HandleLevelBegun;

        _fadeSequence?.Kill();
    }

    private void BuildStepList()
    {
        AddStep(deckCountTarget, "tutorial_explain_deck_count");
        AddStep(showRecipeButtonTarget, "tutorial_explain_show_recipe");
        AddStep(stationLabelTargets, "tutorial_explain_stations");
        AddStep(scoreDisplayTarget, "tutorial_explain_score");
        AddStep(timerTarget, "tutorial_explain_timer");
    }

    private void AddStep(RectTransform target, string localizationKey)
    {
        if (target == null) return;
        _steps.Add(new SpotlightStep { targets = new[] { target }, localizationKey = localizationKey });
    }

    private void AddStep(RectTransform[] targets, string localizationKey)
    {
        if (targets == null || targets.Length == 0) return;

        List<RectTransform> valid = new List<RectTransform>();
        foreach (RectTransform t in targets)
        {
            if (t != null) valid.Add(t);
        }
        if (valid.Count == 0) return;

        _steps.Add(new SpotlightStep { targets = valid.ToArray(), localizationKey = localizationKey });
    }

    private void HandleLevelBegun()
    {
        if (_steps.Count == 0) return; // Inspector'da hiç hedef atanmamışsa turu hiç başlatma.

        gameManager.PauseLevel();
        _stepIndex = -1;
        ShowNextStep();
    }

    private void ShowNextStep()
    {
        _stepIndex++;

        if (_stepIndex >= _steps.Count)
        {
            HideOverlay();
            gameManager.ResumeLevel();
            return;
        }

        SpotlightStep step = _steps[_stepIndex];
        PositionCutout(step.targets);
        _captionText.text = GameLocalization.GetUIString(step.localizationKey);
        _tapHintText.text = GameLocalization.GetUIString("tutorial_tap_to_continue");

        ShowOverlay();
    }

    private void HandleTapToContinue()
    {
        ShowNextStep();
    }

    // --- Overlay kurulumu (tamamen kod içinde inşa edilir - sahneye prefab eklemeye gerek yok) ---

    private void BuildOverlayHierarchy()
    {
        GameObject canvasGO = new GameObject("TutorialSpotlightCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortingOrder;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        canvasGO.AddComponent<GraphicRaycaster>();

        _overlayCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
        _overlayCanvasGroup.alpha = 0f;
        _overlayCanvasGroup.blocksRaycasts = false;
        _overlayCanvasGroup.interactable = false;

        _overlayRect = canvasGO.GetComponent<RectTransform>();
        _overlayRect.anchorMin = Vector2.zero;
        _overlayRect.anchorMax = Vector2.one;
        _overlayRect.offsetMin = Vector2.zero;
        _overlayRect.offsetMax = Vector2.zero;

        // Tüm ekranı kaplayan, görünmez ama tıklamayı yakalayan "her yere dokun" katmanı - en altta.
        GameObject catcherGO = CreateStretchedChild("TapCatcher", _overlayRect);
        Image catcherImage = catcherGO.AddComponent<Image>();
        catcherImage.color = new Color(0f, 0f, 0f, 0f);
        _tapCatcher = catcherGO.AddComponent<Button>();
        _tapCatcher.transition = Selectable.Transition.None;
        _tapCatcher.onClick.AddListener(HandleTapToContinue);

        // Delik etrafındaki 4 koyu bar - görsel amaçlı, tıklamayı TapCatcher'a bıraksın diye raycastTarget kapalı.
        _barTop = CreateBar("SpotlightBarTop");
        _barBottom = CreateBar("SpotlightBarBottom");
        _barLeft = CreateBar("SpotlightBarLeft");
        _barRight = CreateBar("SpotlightBarRight");

        // Açıklama metni - ekranın alt üçte birinde sabit, hangi eleman vurgulanırsa vurgulansın okunabilir kalsın diye.
        GameObject captionPanelGO = new GameObject("CaptionPanel", typeof(RectTransform));
        captionPanelGO.transform.SetParent(_overlayRect, false);
        RectTransform captionPanelRect = captionPanelGO.GetComponent<RectTransform>();
        captionPanelRect.anchorMin = new Vector2(0.5f, 0f);
        captionPanelRect.anchorMax = new Vector2(0.5f, 0f);
        captionPanelRect.pivot = new Vector2(0.5f, 0f);
        captionPanelRect.sizeDelta = new Vector2(referenceResolution.x - 120f, 340f);
        captionPanelRect.anchoredPosition = new Vector2(0f, 160f);
        Image captionBg = captionPanelGO.AddComponent<Image>();
        captionBg.color = new Color(0f, 0f, 0f, 0.55f);
        captionBg.raycastTarget = false;

        _captionText = CreateCaptionText(captionPanelRect, "CaptionText", captionFontSize, 0.62f);
        _tapHintText = CreateCaptionText(captionPanelRect, "TapHintText", captionFontSize * 0.55f, 0.14f);
        _tapHintText.color = new Color(captionTextColor.r, captionTextColor.g, captionTextColor.b, 0.75f);
        _tapHintText.fontStyle = FontStyles.Italic;
    }

    private GameObject CreateStretchedChild(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private RectTransform CreateBar(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_overlayRect, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.AddComponent<Image>();
        image.color = backdropColor;
        image.raycastTarget = false;

        return rt;
    }

    private TMP_Text CreateCaptionText(RectTransform parent, string name, float fontSize, float anchorYBottom)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, anchorYBottom);
        rt.anchorMax = new Vector2(0.95f, anchorYBottom + 0.42f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        if (captionFont != null) text.font = captionFont;
        text.fontSize = fontSize;
        text.color = captionTextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return text;
    }

    /// <summary>
    /// Verilen hedef(ler)i saran, Overlay canvas'ın kendi local koordinat uzayındaki dikdörtgeni
    /// hesaplayıp 4 barı bu dikdörtgenin ETRAFINA (delik kalacak şekilde) yerleştirir.
    /// Hedefler Overlay canvas'ta olmak ZORUNDA DEĞİL - RectTransform.GetWorldCorners() zaten
    /// world-space köşeleri verir, Screen Space Overlay canvas'larda world pozisyonu ekran
    /// pikseline eşit olduğundan (kamera dönüşümü yok) doğrudan bu overlay'in local uzayına
    /// çevrilebilir.
    /// </summary>
    private void PositionCutout(RectTransform[] targets)
    {
        Vector2 localMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 localMax = new Vector2(float.MinValue, float.MinValue);
        Vector3[] corners = new Vector3[4];
        bool foundAny = false;

        foreach (RectTransform target in targets)
        {
            if (target == null) continue;
            target.GetWorldCorners(corners);

            for (int i = 0; i < 4; i++)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRect, corners[i], null, out Vector2 local))
                    continue;

                localMin = Vector2.Min(localMin, local);
                localMax = Vector2.Max(localMax, local);
                foundAny = true;
            }
        }

        if (!foundAny) return;

        localMin -= new Vector2(cutoutPadding, cutoutPadding);
        localMax += new Vector2(cutoutPadding, cutoutPadding);

        Rect overlayRect = _overlayRect.rect;

        // Üst bar: deliğin üstünden ekranın tepesine kadar, tam genişlik.
        SetBar(_barTop,
            centerX: overlayRect.center.x, centerY: (localMax.y + overlayRect.yMax) * 0.5f,
            width: overlayRect.width, height: Mathf.Max(0f, overlayRect.yMax - localMax.y));

        // Alt bar: ekranın dibinden deliğin altına kadar, tam genişlik.
        SetBar(_barBottom,
            centerX: overlayRect.center.x, centerY: (overlayRect.yMin + localMin.y) * 0.5f,
            width: overlayRect.width, height: Mathf.Max(0f, localMin.y - overlayRect.yMin));

        // Sol/sağ bar: sadece deliğin yüksekliği kadar, üst/alt barın kapladığı alanı tekrar kapsamaz.
        float midHeight = Mathf.Max(0f, localMax.y - localMin.y);
        SetBar(_barLeft,
            centerX: (overlayRect.xMin + localMin.x) * 0.5f, centerY: (localMin.y + localMax.y) * 0.5f,
            width: Mathf.Max(0f, localMin.x - overlayRect.xMin), height: midHeight);

        SetBar(_barRight,
            centerX: (localMax.x + overlayRect.xMax) * 0.5f, centerY: (localMin.y + localMax.y) * 0.5f,
            width: Mathf.Max(0f, overlayRect.xMax - localMax.x), height: midHeight);
    }

    private static void SetBar(RectTransform bar, float centerX, float centerY, float width, float height)
    {
        bar.anchoredPosition = new Vector2(centerX, centerY);
        bar.sizeDelta = new Vector2(width, height);
    }

    private void ShowOverlay()
    {
        _fadeSequence?.Kill();
        _overlayCanvasGroup.blocksRaycasts = true;
        _overlayCanvasGroup.interactable = true;
        _fadeSequence = DOTween.Sequence();
        _fadeSequence.Append(_overlayCanvasGroup.DOFade(1f, fadeDuration));
    }

    private void HideOverlay()
    {
        _fadeSequence?.Kill();
        _fadeSequence = DOTween.Sequence();
        _fadeSequence.Append(_overlayCanvasGroup.DOFade(0f, fadeDuration));
        _fadeSequence.OnComplete(() =>
        {
            _overlayCanvasGroup.blocksRaycasts = false;
            _overlayCanvasGroup.interactable = false;
        });
    }
}
