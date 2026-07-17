using System;
using UnityEngine;

/// <summary>
/// Bölümün genel süresini (örn. 90 saniye) yönetir. Bu süre içinde oyuncu
/// desteyi bitirmeye çalışır; süre biterse ve deste hâlâ doluysa bölüm kaybedilir.
///
/// StationAssignmentManager'dan bilinçli olarak ayrı tutuldu: biri "istasyonlar
/// ne zaman karışır" sorusuna, diğeri "bölüm ne zaman biter" sorusuna cevap verir.
/// İkisi de bağımsız timer'lar ama aynı "bölüm süresi" içinde çalışırlar.
/// </summary>
public class LevelTimerManager : MonoBehaviour
{
    [Header("Bölüm Süresi")]
    [SerializeField] private float levelDuration = 90f;

    private float _remainingTime;
    private bool _isRunning;

    public float RemainingTime => _remainingTime;
    public float LevelDuration => levelDuration;

    /// <summary>Her frame kalan süreyi bildirir. UI (geri sayım text/bar) bunu dinler.</summary>
    public event Action<float> OnTimeTick;

    /// <summary>Süre sıfıra ulaştığında (deste bitmemiş olsa bile) bir kere tetiklenir.</summary>
    public event Action OnTimeExpired;

    /// <summary>
    /// GameManager, seçilen LevelData'daki süreyle bu sistemi çalıştırmadan
    /// önce yapılandırır. StartTimer()'dan ÖNCE çağrılmalıdır.
    /// </summary>
    public void Configure(float duration)
    {
        levelDuration = duration;
    }

    public void StartTimer()
    {
        _remainingTime = levelDuration;
        _isRunning = true;
    }

    public void StopTimer()
    {
        _isRunning = false;
    }

    private void Update()
    {
        if (!_isRunning) return;

        _remainingTime -= Time.deltaTime;

        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            _isRunning = false;
            OnTimeTick?.Invoke(_remainingTime);
            OnTimeExpired?.Invoke();
            return;
        }

        OnTimeTick?.Invoke(_remainingTime);
    }
}