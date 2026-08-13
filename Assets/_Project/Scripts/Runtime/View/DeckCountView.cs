using TMPro;
using UnityEngine;

/// <summary>
/// Destede kaç kart kaldığını "kalan/toplam" biçiminde ekranda gösterir.
/// GameManager'ın kart event'lerini dinler, kuralları BİLMEZ - ScoreHudView'ın
/// ScoreManager'a bakışıyla birebir aynı mantık.
///
/// Toplam kart sayısı ActiveLevel.initialDeck'ten (boş/None slotlar hariç,
/// GameManager.BuildInitialDeck ile aynı filtre) OnEnable'da bir kez hesaplanır -
/// BeginLevelPlay çağrılmadan (Recipe Preview paneli açıkken) deste henüz
/// kurulmadığı için RemainingCardCount o ana kadar 0'dır, ama toplam sayı
/// ActiveLevel her zaman hazır olduğu an bilinebilir.
/// </summary>
public class DeckCountView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Görsel Referans")]
    [SerializeField] private TMP_Text deckCountText;

    private int _totalCount;

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnCardChanged += HandleCardChanged;
            gameManager.OnDeckEmptied += HandleDeckEmptied;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;

        _totalCount = CountInitialDeck();

        // Script çalışma sırası yüzünden ilk değeri kaçırmamak için bir kere
        // manuel senkronize et (LevelTimerView.OnEnable ile aynı gerekçe).
        Redraw();
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnCardChanged -= HandleCardChanged;
            gameManager.OnDeckEmptied -= HandleDeckEmptied;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
    }

    private int CountInitialDeck()
    {
        if (gameManager == null || gameManager.ActiveLevel == null || gameManager.ActiveLevel.initialDeck == null)
            return 0;

        int count = 0;
        foreach (var data in gameManager.ActiveLevel.initialDeck)
        {
            if (data != null) count++;
        }

        return count;
    }

    private void HandleCardChanged(CardInstance _)
    {
        Redraw();
    }

    private void HandleDeckEmptied()
    {
        Redraw();
    }

    private void HandleLanguageChanged(LocalizationManager.Language language)
    {
        Redraw();
    }

    private void Redraw()
    {
        if (deckCountText == null) return;

        int remaining = gameManager != null ? gameManager.RemainingCardCount : 0;
        deckCountText.text = GameLocalization.GetUIString("ui_cards_remaining", remaining, _totalCount);
    }
}
