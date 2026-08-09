using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// Level Select'te bir level butonuna basılınca AÇILAN, Recipe Preview'dan ÖNCE
/// gösterilen ara bilgi paneli. Ham madde sayısı, istasyon karışma hızı, toplam
/// süre ve yıldız eşiklerini gösterir. Play'e basınca Game sahnesi açılır,
/// Geri'ye basınca panel kapanır ve altındaki Level Select ekranı görünür olur
/// (Level Select paneli hiç kapatılmadığı için ekstra bir şey yapmaya gerek yok).
/// </summary>
public class LevelInfoPanelView : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject infoPanel;

    [Header("Açılış Animasyonu")]
    [Tooltip("LevelInfoPanel altındaki, aşağıdan yukarı kayarak açılacak asıl içerik kutusu.")]
    [SerializeField] private RectTransform levelInfoContainer;
    [Tooltip("Kartın dinlenme pozisyonunun ne kadar aşağısından yukarı kayarak geleceği.")]
    [SerializeField] private float slideDistance = 800f;
    [SerializeField] private float showDuration = 0.35f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private float hideDuration = 0.25f;
    [SerializeField] private Ease hideEase = Ease.InBack;

    private Vector2 _containerRestPos;
    private bool _containerRestPosCaptured;
    private Tween _containerTween;

    [Header("Bilgi Metinleri")]
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private TMP_Text rawMaterialCountText;
    [SerializeField] private TMP_Text stationShuffleText;
    [SerializeField] private TMP_Text totalDurationText;
    [SerializeField] private TMP_Text threeStarTimeText;
    [SerializeField] private TMP_Text twoStarTimeText;
    [SerializeField] private TMP_Text bestScoreText;

    [Header("Sahne")]
    [SerializeField] private string gameSceneName = "Game";

    private LevelData _currentLevel;

    private void Awake()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    private void OnDisable()
    {
        _containerTween?.Kill();
    }

    /// <summary>LevelButton bunu çağırır - direkt sahne açmak yerine önce bu paneli gösterir.</summary>
    public void ShowForLevel(LevelData level)
    {
        if (level == null) return;

        _currentLevel = level;
        PopulateInfo(level);

        if (infoPanel != null) infoPanel.SetActive(true);

        PlayShowAnimation();
    }

    private void EnsureContainerRestPosCaptured()
    {
        if (_containerRestPosCaptured || levelInfoContainer == null) return;

        _containerRestPos = levelInfoContainer.anchoredPosition;
        _containerRestPosCaptured = true;
    }

    private void PlayShowAnimation()
    {
        if (levelInfoContainer == null) return;

        EnsureContainerRestPosCaptured();

        _containerTween?.Kill();
        levelInfoContainer.anchoredPosition = _containerRestPos + Vector2.down * slideDistance;
        _containerTween = levelInfoContainer.DOAnchorPos(_containerRestPos, showDuration).SetEase(showEase);
    }

    private void PopulateInfo(LevelData level)
    {
        if (levelNameText != null) levelNameText.text = GameLocalization.GetLevelName(level);

        int uniqueRawCount = CountUniqueRawMaterials(level);
        if (rawMaterialCountText != null) rawMaterialCountText.text = GameLocalization.GetUIString("level_info_raw_materials", uniqueRawCount);

        if (stationShuffleText != null)
        {
            stationShuffleText.text = GameLocalization.GetUIString(
                "level_info_station_shuffle",
                level.minStationShuffleInterval.ToString("0.#"),
                level.maxStationShuffleInterval.ToString("0.#"));
        }

        if (totalDurationText != null)
        {
            totalDurationText.text = GameLocalization.GetUIString("level_info_time_limit", FormatSeconds(level.levelDuration));
        }

        // Yıldız eşiği ARTIK süreye değil, toplanan puanın par skoruna oranına bakıyor
        // (bkz. GameManager.CalculateStars). Bu yüzden burada eskiden yazan "şu kadar
        // saniyede bitir" ifadesi YANLIŞ olurdu: hızlı bitiren ama kötü oynayan bir
        // oyuncu sözü tutulmamış sanıp hata olduğunu düşünürdü.
        //
        // Par skoru bu sahnede HESAPLANAMAZ - puan ayarları ScoreManager
        // MonoBehaviour'ında duruyor ve o sadece Game sahnesinde var. Ama yüzde
        // göstermek için par'a hiç ihtiyaç yok: oran zaten LevelData'da.
        if (threeStarTimeText != null)
        {
            threeStarTimeText.text = GameLocalization.GetUIString(
                "level_info_three_star_score", Mathf.RoundToInt(level.threeStarScoreRatio * 100f));
        }

        if (twoStarTimeText != null)
        {
            twoStarTimeText.text = GameLocalization.GetUIString(
                "level_info_two_star_score", Mathf.RoundToInt(level.twoStarScoreRatio * 100f));
        }

        // ScoreProgress puanı ScoreManager gibi ScoreManager MonoBehaviour'ına değil,
        // PlayerPrefs'e KALICI olarak yazıyor - o yüzden bu sahnede (Game sahnesi
        // hiç açılmamışken bile) doğrudan okunabiliyor.
        if (bestScoreText != null)
        {
            int bestScore = ScoreProgress.GetBestScore(level);
            bestScoreText.text = GameLocalization.GetUIString("ui_best_score", bestScore.ToString("N0"));
        }
    }

    private int CountUniqueRawMaterials(LevelData level)
    {
        if (level.initialDeck == null) return 0;

        HashSet<CardData> unique = new HashSet<CardData>();
        foreach (CardData card in level.initialDeck)
        {
            if (card != null) unique.Add(card);
        }

        return unique.Count;
    }

    private string FormatSeconds(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return minutes > 0 ? $"{minutes}:{secs:00}" : $"{secs}s";
    }

    /// <summary>Panel içindeki Play butonuna bağlanacak.</summary>
    public void OnPlayButtonPressed()
    {
        if (_currentLevel == null) return;

        LevelSession.SelectedLevel = _currentLevel;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Panel içindeki Geri butonuna bağlanacak - Level Select ekranına döner.</summary>
    public void OnBackButtonPressed()
    {
        // Level Select paneli hiç kapatılmadı, bu panel sadece onun ÜSTÜNDE
        // duruyordu - kapanınca otomatik olarak Level Select tekrar görünür olur.
        if (levelInfoContainer == null)
        {
            if (infoPanel != null) infoPanel.SetActive(false);
            return;
        }

        EnsureContainerRestPosCaptured();

        _containerTween?.Kill();
        _containerTween = levelInfoContainer
            .DOAnchorPos(_containerRestPos + Vector2.down * slideDistance, hideDuration)
            .SetEase(hideEase)
            .OnComplete(() =>
            {
                if (infoPanel != null) infoPanel.SetActive(false);
                levelInfoContainer.anchoredPosition = _containerRestPos;
            });
    }
}