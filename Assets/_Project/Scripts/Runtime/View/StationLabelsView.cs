using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Ekranın 4 kenarında (Yukarı/Aşağı/Sol/Sağ) o an hangi istasyonun durduğunu gösterir.
/// StationAssignmentManager karıştırma yaptığında, etiketleri günceller ve küçük bir
/// "pulse" animasyonuyla oyuncunun dikkatini değişime çeker.
///
/// Ayrıca GameManager'ın OnValidSwipe/OnInvalidSwipe event'lerini dinleyerek, oyuncu bir
/// yöne kart attığında o yönün arka planında anında "doğru" (yeşil flaş + pop) veya
/// "yanlış" (kırmızı flaş + sallanma) geri bildirimi oynatır.
///
/// Sahne kurulumu: Canvas'ta kartın etrafına 4 tane TMP_Text (ve her birinin arka plan
/// Image'ı) yerleştir (üstte, altta, solda, sağda) ve bunları aşağıdaki alanlara sürükle-bırak.
/// </summary>
public class StationLabelsView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private StationAssignmentManager stationAssignmentManager;

    [Header("Yön Etiketleri (UI)")]
    [SerializeField] private TMP_Text upLabel;
    [SerializeField] private TMP_Text downLabel;
    [SerializeField] private TMP_Text leftLabel;
    [SerializeField] private TMP_Text rightLabel;

    [Header("Yön Arka Planları (UI)")]
    [SerializeField] private Image upBackground;
    [SerializeField] private Image downBackground;
    [SerializeField] private Image leftBackground;
    [SerializeField] private Image rightBackground;

    [Header("Değişim Animasyonu (istasyon karıştığında)")]
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.2f;

    [Header("Doğru Swipe Geri Bildirimi")]
    [SerializeField] private Color correctFlashColor = new Color(0.49f, 0.82f, 0.48f, 0.95f); // yeşil
    [SerializeField] private float correctPunchScale = 1.3f;
    [SerializeField] private float flashInDuration = 0.08f;
    [SerializeField] private float flashOutDuration = 0.35f;

    [Header("Yanlış Swipe Geri Bildirimi")]
    [SerializeField] private Color wrongFlashColor = new Color(0.88f, 0.4f, 0.4f, 0.95f); // kırmızı
    [SerializeField] private float wrongShakeStrength = 18f;
    [SerializeField] private float wrongShakeDuration = 0.3f;

    private IReadOnlyDictionary<SwipeDirection, StationData> _currentAssignment;

    private Dictionary<SwipeDirection, TMP_Text> _labelsByDirection;
    private Dictionary<SwipeDirection, Image> _backgroundsByDirection;
    private Dictionary<SwipeDirection, Color> _baseBackgroundColor;

    private void Awake()
    {
        _labelsByDirection = new Dictionary<SwipeDirection, TMP_Text>
        {
            { SwipeDirection.Up, upLabel },
            { SwipeDirection.Down, downLabel },
            { SwipeDirection.Left, leftLabel },
            { SwipeDirection.Right, rightLabel },
        };

        _backgroundsByDirection = new Dictionary<SwipeDirection, Image>
        {
            { SwipeDirection.Up, upBackground },
            { SwipeDirection.Down, downBackground },
            { SwipeDirection.Left, leftBackground },
            { SwipeDirection.Right, rightBackground },
        };

        // Flaş animasyonlarının geri döneceği "dinlenme" rengini sahnedeki mevcut
        // değerden okuyoruz - Inspector'da renk değiştirilirse kod değişmeden uyum sağlar.
        _baseBackgroundColor = new Dictionary<SwipeDirection, Color>();
        foreach (KeyValuePair<SwipeDirection, Image> pair in _backgroundsByDirection)
        {
            if (pair.Value != null) _baseBackgroundColor[pair.Key] = pair.Value.color;
        }
    }

    private void OnEnable()
    {
        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled += HandleStationsShuffled;

        if (gameManager != null)
        {
            gameManager.OnValidSwipe += HandleValidSwipe;
            gameManager.OnInvalidSwipe += HandleInvalidSwipe;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled -= HandleStationsShuffled;

        if (gameManager != null)
        {
            gameManager.OnValidSwipe -= HandleValidSwipe;
            gameManager.OnInvalidSwipe -= HandleInvalidSwipe;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleStationsShuffled(IReadOnlyDictionary<SwipeDirection, StationData> assignment)
    {
        _currentAssignment = assignment;

        UpdateLabel(upLabel, assignment, SwipeDirection.Up, animate: true);
        UpdateLabel(downLabel, assignment, SwipeDirection.Down, animate: true);
        UpdateLabel(leftLabel, assignment, SwipeDirection.Left, animate: true);
        UpdateLabel(rightLabel, assignment, SwipeDirection.Right, animate: true);
    }

    private void HandleLanguageChanged(LocalizationManager.Language language)
    {
        if (_currentAssignment == null) return;

        UpdateLabel(upLabel, _currentAssignment, SwipeDirection.Up, animate: false);
        UpdateLabel(downLabel, _currentAssignment, SwipeDirection.Down, animate: false);
        UpdateLabel(leftLabel, _currentAssignment, SwipeDirection.Left, animate: false);
        UpdateLabel(rightLabel, _currentAssignment, SwipeDirection.Right, animate: false);
    }

    private void UpdateLabel(TMP_Text label, IReadOnlyDictionary<SwipeDirection, StationData> assignment, SwipeDirection direction, bool animate)
    {
        if (label == null) return;

        string newText = assignment.TryGetValue(direction, out StationData station) && station != null
            ? GameLocalization.GetStationName(station)
            : string.Empty;

        // Metin gerçekten değiştiyse pulse animasyonu oyna - her karışmada
        // aynı istasyon aynı yönde kalmışsa gereksiz animasyon oynatma.
        bool changed = label.text != newText;
        label.text = newText;

        if (animate && changed)
        {
            label.transform.DOKill();
            label.transform.localScale = Vector3.one;
            label.transform.DOPunchScale(Vector3.one * (pulseScale - 1f), pulseDuration, vibrato: 1, elasticity: 0.5f);
        }
    }

    /// <summary>Oyuncu doğru istasyona attı: o yönün arka planı yeşile çalıp zıplar.</summary>
    private void HandleValidSwipe(SwipeDirection direction, StationData station)
    {
        if (!_backgroundsByDirection.TryGetValue(direction, out Image background) || background == null) return;
        if (!_baseBackgroundColor.TryGetValue(direction, out Color baseColor)) baseColor = background.color;

        background.DOKill();
        background.transform.DOKill();
        background.transform.localScale = Vector3.one;

        background.color = correctFlashColor;
        background.DOColor(baseColor, flashOutDuration).SetDelay(flashInDuration);
        background.transform.DOPunchScale(Vector3.one * (correctPunchScale - 1f), flashInDuration + flashOutDuration, vibrato: 1, elasticity: 0.6f);
    }

    /// <summary>Oyuncu yanlış istasyona attı: o yönün arka planı kızarıp sallanır.</summary>
    private void HandleInvalidSwipe(SwipeDirection direction, StationData station)
    {
        if (!_backgroundsByDirection.TryGetValue(direction, out Image background) || background == null) return;
        if (!_baseBackgroundColor.TryGetValue(direction, out Color baseColor)) baseColor = background.color;

        background.DOKill();
        background.rectTransform.DOKill();

        background.color = wrongFlashColor;
        background.DOColor(baseColor, flashOutDuration).SetDelay(flashInDuration);
        background.rectTransform.DOShakeAnchorPos(wrongShakeDuration, wrongShakeStrength, vibrato: 12, randomness: 90, snapping: false, fadeOut: true);
    }
}
