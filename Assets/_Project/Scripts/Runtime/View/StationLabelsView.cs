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

    private void OnEnable()
    {
        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled += HandleStationsShuffled;
    }

    private void OnDisable()
    {
        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled -= HandleStationsShuffled;
    }

    private void HandleStationsShuffled(IReadOnlyDictionary<SwipeDirection, StationData> assignment)
    {
        UpdateLabel(upLabel, assignment, SwipeDirection.Up);
        UpdateLabel(downLabel, assignment, SwipeDirection.Down);
        UpdateLabel(leftLabel, assignment, SwipeDirection.Left);
        UpdateLabel(rightLabel, assignment, SwipeDirection.Right);
    }

    private void UpdateLabel(TMP_Text label, IReadOnlyDictionary<SwipeDirection, StationData> assignment, SwipeDirection direction)
    {
        if (label == null) return;

        string newText = assignment.TryGetValue(direction, out StationData station) && station != null
            ? station.displayName
            : string.Empty;

        // Metin gerçekten değiştiyse pulse animasyonu oyna - her karışmada
        // aynı istasyon aynı yönde kalmışsa gereksiz animasyon oynatma.
        bool changed = label.text != newText;
        label.text = newText;

        if (changed)
        {
            label.transform.DOKill();
            label.transform.localScale = Vector3.one;
            label.transform.DOPunchScale(Vector3.one * (pulseScale - 1f), pulseDuration, vibrato: 1, elasticity: 0.5f);
        }
    }
}
