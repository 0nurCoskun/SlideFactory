using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// LevelTimerManager'ın kalan süresini ekranda gösterir. Süre kritik eşiğin altına
/// düşünce text'i kırmızıya çevirip yavaşça yanıp söner - oyuncuya "acele et" hissi verir.
///
/// Sahne kurulumu: Canvas altına bir TMP_Text ekle (örn. "TimerText"),
/// bu script'i o objeye ekle, referansları bağla.
/// </summary>
public class LevelTimerView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private LevelTimerManager levelTimerManager;

    [Header("Görsel Referans")]
    [SerializeField] private TMP_Text timerText;

    [Header("Renkler")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.55f, 0f); // turuncu
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Eşikler (saniye)")]
    [Tooltip("Kalan süre bu değerin altına düşünce turuncuya döner.")]
    [SerializeField] private float warningThreshold = 15f;
    [Tooltip("Kalan süre bu değerin altına düşünce kırmızıya döner ve yanıp sönmeye başlar.")]
    [SerializeField] private float criticalThreshold = 5f;

    [Header("Kritik Yanıp Sönme Animasyonu")]
    [SerializeField] private float criticalPulseScale = 1.2f;
    [SerializeField] private float criticalPulseDuration = 0.4f;

    private bool _isInCriticalState;
    private Sequence _criticalPulseSequence;

    private void OnEnable()
    {
        if (levelTimerManager != null)
            levelTimerManager.OnTimeTick += HandleTimeTick;

        // Sahne yüklendiğinde timer zaten başlamışsa (örn. script script execution
        // order farkıyla geç abone olunduysa) ilk değeri kaçırmamak için bir kere
        // manuel senkronize et.
        if (levelTimerManager != null)
        {
            HandleTimeTick(levelTimerManager.RemainingTime);
        }
    }

    private void OnDisable()
    {
        if (levelTimerManager != null)
            levelTimerManager.OnTimeTick -= HandleTimeTick;

        _criticalPulseSequence?.Kill();
    }

    private void HandleTimeTick(float remainingSeconds)
    {
        if (timerText == null) return;

        timerText.text = FormatTime(remainingSeconds);

        bool shouldBeCritical = remainingSeconds <= criticalThreshold;

        if (shouldBeCritical && !_isInCriticalState)
        {
            EnterCriticalState();
        }
        else if (!shouldBeCritical && _isInCriticalState)
        {
            ExitCriticalState();
        }

        if (!shouldBeCritical)
        {
            // Kritik değilken renk anlık olarak güncellenir (pulse yokken).
            timerText.color = remainingSeconds <= warningThreshold ? warningColor : normalColor;
        }
    }

    private void EnterCriticalState()
    {
        _isInCriticalState = true;
        timerText.color = criticalColor;

        _criticalPulseSequence?.Kill();
        _criticalPulseSequence = DOTween.Sequence();
        _criticalPulseSequence.Append(timerText.transform.DOScale(criticalPulseScale, criticalPulseDuration).SetEase(Ease.InOutSine));
        _criticalPulseSequence.Append(timerText.transform.DOScale(1f, criticalPulseDuration).SetEase(Ease.InOutSine));
        _criticalPulseSequence.SetLoops(-1);
    }

    private void ExitCriticalState()
    {
        _isInCriticalState = false;
        _criticalPulseSequence?.Kill();
        timerText.transform.localScale = Vector3.one;
    }

    private static string FormatTime(float remainingSeconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
