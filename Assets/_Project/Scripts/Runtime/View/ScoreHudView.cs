using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Oynanış sırasında puanı ve combo çarpanını ekranda gösterir. ScoreManager'ın
/// event'lerini dinler, kuralları BİLMEZ - AudioTriggerView'ın GameManager'a
/// bakışıyla birebir aynı mantık.
///
/// Sahnede genelde üst UI şeridine (SafeArea'nın doküman yorumunda geçen "TopUI")
/// eklenir. Combo sisteminin görünür olması ÖNEMLİ: çarpan görünmezse oyuncu neden
/// puan kazandığını anlamaz ve mekanik boşa gider.
/// </summary>
public class ScoreHudView : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Metinler")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("Combo çarpanı (örn. 'x2.50'). Zorunlu değil - boş bırakabilirsin.")]
    [SerializeField] private TMP_Text multiplierText;

    [Header("Puan Sayım Animasyonu")]
    [Tooltip("Puan değiştiğinde sayının eski değerden yeni değere akma süresi.")]
    [SerializeField] private float countUpDuration = 0.35f;
    [SerializeField] private Ease countUpEase = Ease.OutCubic;

    [Header("Çarpan Vurgusu")]
    [Tooltip("Çarpan YÜKSELDİĞİNDE metnin büyüyüp geri döneceği oran.")]
    [SerializeField] private float multiplierPunchScale = 1.25f;
    [SerializeField] private float multiplierPunchDuration = 0.15f;
    [Tooltip("Çarpan 1.00'a döndüğünde çarpan metnini tamamen gizle - " +
             "ekranda sürekli 'x1.00' durması gereksiz gürültü yaratıyor.")]
    [SerializeField] private bool hideMultiplierAtBase = true;

    [Header("Ses")]
    [Tooltip("Seri kilometre taşına ulaşıldığında çalınır (ScoreManager'daki aralığa göre).")]
    [SerializeField] private AudioClip comboMilestoneClip;

    [Header("Tutorial")]
    [Tooltip("Tutorial level'ında HUD'ı tamamen gizle. Varsayılan kapalı: combo sistemini " +
             "öğretmenin en iyi yeri zaten tutorial.")]
    [SerializeField] private bool hideOnTutorial = false;

    private Tween _countUpTween;
    private Sequence _punchSequence;
    private int _displayedScore;
    private float _lastMultiplier = 1f;

    private void OnEnable()
    {
        if (scoreManager != null)
        {
            scoreManager.OnScoreChanged += HandleScoreChanged;
            scoreManager.OnMultiplierChanged += HandleMultiplierChanged;
            scoreManager.OnComboMilestone += HandleComboMilestone;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;

        // Script çalışma sırası yüzünden ilk değerleri kaçırmamak için bir kere
        // manuel senkronize et (LevelTimerView.OnEnable ile aynı gerekçe).
        if (scoreManager != null)
        {
            _displayedScore = scoreManager.TotalScore;
            _lastMultiplier = scoreManager.CurrentMultiplier;
        }

        RedrawScore();
        RedrawMultiplier();
    }

    private void OnDisable()
    {
        if (scoreManager != null)
        {
            scoreManager.OnScoreChanged -= HandleScoreChanged;
            scoreManager.OnMultiplierChanged -= HandleMultiplierChanged;
            scoreManager.OnComboMilestone -= HandleComboMilestone;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;

        _countUpTween?.Kill();
        _punchSequence?.Kill();
    }

    private void Start()
    {
        if (hideOnTutorial && gameManager != null && gameManager.ActiveLevel != null &&
            gameManager.ActiveLevel.isTutorial)
        {
            gameObject.SetActive(false);
        }
    }

    private void HandleScoreChanged(int newTotal)
    {
        // Level bittikten sonraki TEK puan değişimi süre bonusudur ve onu LevelResultView
        // kendi sayım animasyonunda zaten gösteriyor. Burada da saymak, aynı sayının
        // ekranda iki yerde yarışmasına yol açar.
        // (EndLevel, _levelEnded = true'yu FinalizeScore'dan ÖNCE yaptığı için bu kontrol
        //  tam olarak süre bonusunu yakalar.)
        if (gameManager != null && gameManager.IsLevelEnded) return;

        _countUpTween?.Kill();
        _countUpTween = DOVirtual.Int(_displayedScore, newTotal, countUpDuration, value =>
            {
                _displayedScore = value;
                RedrawScore();
            })
            .SetEase(countUpEase);
    }

    private void HandleMultiplierChanged(float multiplier)
    {
        bool increased = multiplier > _lastMultiplier;
        _lastMultiplier = multiplier;

        RedrawMultiplier();

        // Sadece YÜKSELİŞTE vurgula. Sönüm (decay) sırasında çarpan her karede
        // biraz düşüyor - orada da vurgulamak metni titretirdi.
        if (!increased || multiplierText == null) return;

        _punchSequence?.Kill();
        multiplierText.transform.localScale = Vector3.one;

        _punchSequence = DOTween.Sequence();
        _punchSequence.Append(multiplierText.transform
            .DOScale(multiplierPunchScale, multiplierPunchDuration).SetEase(Ease.OutQuad));
        _punchSequence.Append(multiplierText.transform
            .DOScale(1f, multiplierPunchDuration).SetEase(Ease.InQuad));
    }

    private void HandleComboMilestone(int streak)
    {
        AudioManager.Instance?.PlaySFX(comboMilestoneClip);
    }

    private void HandleLanguageChanged(LocalizationManager.Language language)
    {
        RedrawScore();
        RedrawMultiplier();
    }

    private void RedrawScore()
    {
        if (scoreText == null) return;
        scoreText.text = GameLocalization.GetUIString("ui_score_value", _displayedScore.ToString("N0"));
    }

    private void RedrawMultiplier()
    {
        if (multiplierText == null) return;

        float multiplier = scoreManager != null ? scoreManager.CurrentMultiplier : 1f;

        if (hideMultiplierAtBase && multiplier <= 1f)
        {
            multiplierText.text = string.Empty;
            return;
        }

        multiplierText.text = GameLocalization.GetUIString("ui_combo_multiplier", multiplier.ToString("0.00"));
    }
}
