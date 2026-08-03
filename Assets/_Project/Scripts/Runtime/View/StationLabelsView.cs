using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// Ekranın 4 kenarında (Yukarı/Aşağı/Sol/Sağ) o an hangi istasyonun durduğunu gösterir.
/// StationAssignmentManager karıştırma yaptığında, etiketleri günceller ve küçük bir
/// "pulse" animasyonuyla oyuncunun dikkatini değişime çeker.
///
/// Sahne kurulumu: Canvas'ta kartın etrafına 4 tane TMP_Text yerleştir
/// (üstte, altta, solda, sağda) ve bunları aşağıdaki 4 alana sürükle-bırak.
/// </summary>
public class StationLabelsView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private StationAssignmentManager stationAssignmentManager;

    [Header("Yön Etiketleri (UI)")]
    [SerializeField] private TMP_Text upLabel;
    [SerializeField] private TMP_Text downLabel;
    [SerializeField] private TMP_Text leftLabel;
    [SerializeField] private TMP_Text rightLabel;

    [Header("Değişim Animasyonu")]
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.2f;

    private IReadOnlyDictionary<SwipeDirection, StationData> _currentAssignment;

    private void OnEnable()
    {
        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled += HandleStationsShuffled;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled -= HandleStationsShuffled;

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
}
