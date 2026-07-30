using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("Bilgi Metinleri")]
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private TMP_Text rawMaterialCountText;
    [SerializeField] private TMP_Text stationShuffleText;
    [SerializeField] private TMP_Text totalDurationText;
    [SerializeField] private TMP_Text threeStarTimeText;
    [SerializeField] private TMP_Text twoStarTimeText;

    [Header("Sahne")]
    [SerializeField] private string gameSceneName = "Game";

    private LevelData _currentLevel;

    private void Awake()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    /// <summary>LevelButton bunu çağırır - direkt sahne açmak yerine önce bu paneli gösterir.</summary>
    public void ShowForLevel(LevelData level)
    {
        if (level == null) return;

        _currentLevel = level;
        PopulateInfo(level);

        if (infoPanel != null) infoPanel.SetActive(true);
    }

    private void PopulateInfo(LevelData level)
    {
        if (levelNameText != null) levelNameText.text = level.displayName;

        int uniqueRawCount = CountUniqueRawMaterials(level);
        if (rawMaterialCountText != null) rawMaterialCountText.text = $"Raw Materials: {uniqueRawCount}";

        if (stationShuffleText != null)
        {
            stationShuffleText.text = $"Station Shuffle: {level.minStationShuffleInterval:0.#}-{level.maxStationShuffleInterval:0.#}s";
        }

        if (totalDurationText != null)
        {
            totalDurationText.text = $"Time Limit: {FormatSeconds(level.levelDuration)}";
        }

        // Yıldız eşiği "kalan süre oranı" olarak tutuluyor (LevelData'da), burada
        // oyuncuya daha anlamlı gelecek şekilde "kaç saniyeDE bitirmesi gerekiyor"a çeviriyoruz.
        if (threeStarTimeText != null)
        {
            float maxRemainingForThreeStars = level.levelDuration * level.threeStarRemainingRatio;
            float timeLimitForThreeStars = level.levelDuration - maxRemainingForThreeStars;
            threeStarTimeText.text = $": finish within {FormatSeconds(timeLimitForThreeStars)}";
        }

        if (twoStarTimeText != null)
        {
            float maxRemainingForTwoStars = level.levelDuration * level.twoStarRemainingRatio;
            float timeLimitForTwoStars = level.levelDuration - maxRemainingForTwoStars;
            twoStarTimeText.text = $": finish within {FormatSeconds(timeLimitForTwoStars)}";
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
        if (infoPanel != null) infoPanel.SetActive(false);
        // Level Select paneli hiç kapatılmadı, bu panel sadece onun ÜSTÜNDE
        // duruyordu - kapanınca otomatik olarak Level Select tekrar görünür olur.
    }
}