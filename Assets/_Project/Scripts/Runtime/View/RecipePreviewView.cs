using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
    [SerializeField] private string stepSeparator = "  <b><size=115%>→</size></b>  ";
    [SerializeField] private string stationWrapFormat = "<color=#A6551A><b>[{0}]</b></color>"; // istasyon ismini köşeli parantez içine alır, vurgu rengiyle

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

        foreach (CardData rawCard in level.initialDeck)
        {
            if (rawCard == null || alreadyShown.Contains(rawCard)) continue;
            alreadyShown.Add(rawCard);

            List<ProductionChainUtility.ChainStep> steps = ProductionChainUtility.BuildChain(rawCard);
            string line = BuildChainDisplayText(steps, chainNumber);
            chainNumber++;

            TMP_Text row = Instantiate(chainRowPrefab, chainListContainer);
            row.text = line;
            row.gameObject.SetActive(true);
        }

        _hasPopulatedOnce = true;
    }

    private string BuildChainDisplayText(List<ProductionChainUtility.ChainStep> steps, int chainNumber)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(string.Format(chainIndexFormat, chainNumber));

        for (int i = 0; i < steps.Count; i++)
        {
            string cardName = steps[i].Card != null ? GameLocalization.GetCardName(steps[i].Card) : "???";
            sb.Append(string.Format(cardNameFormat, cardName));

            if (steps[i].StationToNext != null)
            {
                sb.Append(stepSeparator);
                sb.Append(string.Format(stationWrapFormat, GameLocalization.GetStationName(steps[i].StationToNext)));
            }

            // Son adım değilse, bir sonraki adımı YENİ SATIRA (ve zincirin numarasıyla hizalı girintiye) yaz.
            if (i < steps.Count - 1)
            {
                sb.Append('\n');
                sb.Append("      ");
            }
        }

        return sb.ToString();
    }
}