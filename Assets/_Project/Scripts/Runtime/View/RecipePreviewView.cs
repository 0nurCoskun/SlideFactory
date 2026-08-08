using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Level başında (ve "?" butonuna her basıldığında) o level'ın üretim zincirini
/// gösteren paneli yönetir. Panel açıkken oyun duraklar (süre/istasyon karışması
/// donar, swipe'lar yok sayılır), panel kapanınca kaldığı yerden devam eder.
///
/// İlk açılışta (level daha hiç başlamamışken) panel kapatıldığında level
/// FİİLEN BAŞLAR (GameManager.BeginLevelPlay()). Sonraki her açılışta ise
/// sadece duraklatma/devam ettirme yapılır (GameManager.PauseLevel/ResumeLevel).
/// </summary>
public class RecipePreviewView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;
    [Tooltip("Level ilk kez başlarken deste karma animasyonunu oynatır. Boş bırakılırsa direkt BeginLevelPlay() çağrılır.")]
    [SerializeField] private DeckShuffleView deckShuffleView;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform chainListContainer;
    [Tooltip("Her bir zincir satırı için kullanılacak TMP_Text prefab'ı.")]
    [SerializeField] private TMP_Text chainRowPrefab;

    [Header("Açılış/Kapanış Animasyonu")]
    [Tooltip("panelRoot altındaki, aşağıdan yukarı kayarak açılıp kapanışta geri aşağı kayacak asıl içerik kutusu (RecipePanel).")]
    [SerializeField] private RectTransform recipePanelContainer;
    [Tooltip("Kartın dinlenme pozisyonunun ne kadar aşağısından yukarı kayarak geleceği.")]
    [SerializeField] private float slideDistance = 800f;
    [SerializeField] private float showDuration = 0.35f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private float hideDuration = 0.25f;
    [SerializeField] private Ease hideEase = Ease.InBack;

    private Vector2 _containerRestPos;
    private bool _containerRestPosCaptured;
    private Tween _containerTween;

    [Header("Görünüm")]
    [SerializeField] private string chainIndexFormat = "<b>{0}.</b>  "; // her zincirin başına 1., 2., ... eklenir - oyuncu zincirleri birbirinden kolayca ayırsın
    [SerializeField] private string cardNameFormat = "<b>{0}</b>";
    [SerializeField] private string stepSeparator = "  <b>→</b>  ";
    [SerializeField] private string stationWrapFormat = "<color=#A6551A><b>[{0}]</b></color>"; // istasyon ismini köşeli parantez içine alır, vurgu rengiyle
    [Tooltip("Aynı zincirin adımları arasındaki satır boşluğuna EK olarak, farklı zincirler arasına konan boşluk (piksel).")]
    [SerializeField] private float spaceBetweenChains = 40f;

    [Header("Panel Açıkken Gizlenecek Butonlar")]
    [Tooltip("Pause ve Show Recipe (?) butonları gibi - panel açıkken gizlenip, kapanınca tekrar gösterilir.")]
    [SerializeField] private GameObject[] gameplayOnlyButtons;

    private bool _hasPopulatedOnce;

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;

        _containerTween?.Kill();
    }

    private void HandleLanguageChanged(LocalizationManager.Language language)
    {
        if (_hasPopulatedOnce) PopulateChains();
    }

    private void Start()
    {
        // İlk açılış: level henüz başlamadığı için oyun zaten duraklamış durumda
        // sayılır (GameManager.BeginLevelPlay() hiç çağrılmadı) - burada ekstra
        // bir PauseLevel() çağırmaya gerek yok, sadece paneli gösteriyoruz.
        PopulateChains();
        panelRoot.SetActive(true);
        SetGameplayButtonsVisible(false);
        PlayShowAnimation();
    }

    /// <summary>Oyun sırasındaki "?" (Recipe'yi tekrar göster) butonuna bağlanacak.</summary>
    public void OnShowRecipeButtonPressed()
    {
        if (!_hasPopulatedOnce) PopulateChains();
        panelRoot.SetActive(true);
        SetGameplayButtonsVisible(false);
        PlayShowAnimation();
        // Bilerek PauseLevel() ÇAĞRILMIYOR - süre ve istasyon karışması akmaya devam etsin,
        // oyuncu "hile" yaparak süreyi durdurup rahatça bakamasın.
    }

    /// <summary>Panel içindeki "Kapat / Başla" butonuna bağlanacak.</summary>
    public void OnCloseButtonPressed()
    {
        PlayHideAnimation();
    }

    private void EnsureContainerRestPosCaptured()
    {
        if (_containerRestPosCaptured || recipePanelContainer == null) return;

        _containerRestPos = recipePanelContainer.anchoredPosition;
        _containerRestPosCaptured = true;
    }

    private void PlayShowAnimation()
    {
        if (recipePanelContainer == null) return;

        EnsureContainerRestPosCaptured();

        _containerTween?.Kill();
        recipePanelContainer.anchoredPosition = _containerRestPos + Vector2.down * slideDistance;
        _containerTween = recipePanelContainer.DOAnchorPos(_containerRestPos, showDuration).SetEase(showEase);
    }

    private void PlayHideAnimation()
    {
        if (recipePanelContainer == null)
        {
            CloseAndResumeGame();
            return;
        }

        EnsureContainerRestPosCaptured();

        _containerTween?.Kill();
        _containerTween = recipePanelContainer
            .DOAnchorPos(_containerRestPos + Vector2.down * slideDistance, hideDuration)
            .SetEase(hideEase)
            .OnComplete(CloseAndResumeGame);
    }

    private void CloseAndResumeGame()
    {
        panelRoot.SetActive(false);
        SetGameplayButtonsVisible(true);

        if (!gameManager.HasBegun)
        {
            if (deckShuffleView != null)
            {
                deckShuffleView.PlayShuffleThenBeginLevel();
            }
            else
            {
                gameManager.BeginLevelPlay();
            }
        }
        else
        {
            gameManager.ResumeLevel();
        }
    }

    private void SetGameplayButtonsVisible(bool visible)
    {
        if (gameplayOnlyButtons == null) return;

        foreach (GameObject button in gameplayOnlyButtons)
        {
            if (button != null) button.SetActive(visible);
        }
    }

    private void PopulateChains()
    {
        if (chainListContainer == null || chainRowPrefab == null || gameManager == null) return;

        foreach (Transform child in chainListContainer)
        {
            Destroy(child.gameObject);
        }

        LevelData level = gameManager.ActiveLevel;
        if (level == null || level.initialDeck == null) return;

        // Aynı ham madde türünden destede birden fazla olabilir (örn. 3x Ham Odun) -
        // bu durumda zinciri sadece BİR KERE göstermek yeterli, 3 kere tekrar etmesin.
        HashSet<CardData> alreadyShown = new HashSet<CardData>();
        int chainNumber = 1;
        bool isFirstChain = true;

        foreach (CardData rawCard in level.initialDeck)
        {
            if (rawCard == null || alreadyShown.Contains(rawCard)) continue;
            alreadyShown.Add(rawCard);

            // Zincirler arasına, aynı zincirin adımlarından daha büyük bir boşluk
            // koyuyoruz ki oyuncu farklı tarifleri satır satır kolayca ayırt edebilsin.
            if (!isFirstChain) InstantiateChainSpacer();
            isFirstChain = false;

            List<ProductionChainUtility.ChainStep> steps = ProductionChainUtility.BuildChain(rawCard);
            InstantiateChainRows(steps, chainNumber);
            chainNumber++;
        }

        _hasPopulatedOnce = true;
    }

    /// <summary>
    /// Bir üretim zincirinin HER adımını kendi satırına (kendi TMP_Text'ine) yerleştirir,
    /// böylece autosize her satırı bağımsız hesaplar - uzun bir adım kısa adımları
    /// küçültmez, tek bir zincir tek bir bloktaymış gibi orantısız küçülme olmaz.
    /// </summary>
    private void InstantiateChainRows(List<ProductionChainUtility.ChainStep> steps, int chainNumber)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            string cardName = steps[i].Card != null ? GameLocalization.GetCardName(steps[i].Card) : "???";
            string line = i == 0 ? string.Format(chainIndexFormat, chainNumber) : "";
            line += string.Format(cardNameFormat, cardName);

            if (steps[i].StationToNext != null)
            {
                line += stepSeparator;
                line += string.Format(stationWrapFormat, GameLocalization.GetStationName(steps[i].StationToNext));
            }

            TMP_Text row = Instantiate(chainRowPrefab, chainListContainer);
            row.text = line;
            row.gameObject.SetActive(true);
        }
    }

    /// <summary>Farklı zincirler arasına ekstra boşluk bırakan, görünmez bir layout satırı oluşturur.</summary>
    private void InstantiateChainSpacer()
    {
        GameObject spacer = new GameObject("ChainSpacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(chainListContainer, false);
        spacer.GetComponent<LayoutElement>().minHeight = spaceBetweenChains;
    }
}